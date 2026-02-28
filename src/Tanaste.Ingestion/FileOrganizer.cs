using Microsoft.Extensions.Logging;
using Tanaste.Domain.Enums;
using Tanaste.Ingestion.Contracts;
using Tanaste.Ingestion.Models;

namespace Tanaste.Ingestion;

/// <summary>
/// Calculates organised destination paths from tokenized templates and
/// performs collision-safe, retry-backed file moves.
///
/// ──────────────────────────────────────────────────────────────────
/// Template tokens (spec: Phase 7 – Extension Points § Organization Templates)
/// ──────────────────────────────────────────────────────────────────
///  Tokens are surrounded by curly braces and resolved from the candidate's
///  claims at the time of organization.  Unresolved tokens are replaced with
///  the literal string "Unknown".
///
///  Built-in tokens:
///   {Title}      — title claim (confidence-winner)
///   {Author}     — author claim
///   {Year}       — year claim (4-digit integer)
///   {MediaType}  — MediaType enum name (Movie, Epub, Comic, …)
///   {Extension}  — file extension WITHOUT leading dot (e.g. "mp4", "epub")
///   {Series}     — series claim
///   {Publisher}  — publisher claim
///
///  Custom tokens may be injected via the <c>IReadOnlyDictionary</c> overload
///  of <see cref="CalculatePath"/>.
///
/// ──────────────────────────────────────────────────────────────────
/// Collision handling
/// ──────────────────────────────────────────────────────────────────
///  When the computed destination already exists, a numeric suffix is appended:
///  <c>Title (2).epub</c>, <c>Title (3).epub</c>, etc.
///  Spec: "If a naming conflict occurs … MUST append a unique suffix."
///
/// ──────────────────────────────────────────────────────────────────
/// Move retry
/// ──────────────────────────────────────────────────────────────────
///  On <see cref="IOException"/>, the move is retried with exponential
///  back-off up to <c>MaxMoveAttempts</c>.
///  Spec: Phase 7 – Lock handling § Retry Exponential Backoff.
/// </summary>
public sealed class FileOrganizer : IFileOrganizer
{
    private const int MaxMoveAttempts = 5;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<FileOrganizer> _logger;

    // Characters that are illegal in file/directory names on Windows and most POSIX systems.
    private static readonly char[] InvalidPathChars =
        Path.GetInvalidFileNameChars()
            .Concat(Path.GetInvalidPathChars())
            .Distinct()
            .ToArray();

    public FileOrganizer(ILogger<FileOrganizer> logger)
    {
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // IFileOrganizer
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public string CalculatePath(IngestionCandidate candidate, string template)
        => CalculatePath(candidate, template, extraTokens: null);

    /// <summary>
    /// Overload that accepts additional caller-supplied token values.
    /// </summary>
    public string CalculatePath(
        IngestionCandidate candidate,
        string template,
        IReadOnlyDictionary<string, string>? extraTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(candidate);

        // Build the token dictionary from the candidate's metadata claims.
        var tokens = BuildTokens(candidate, extraTokens);

        // Replace each {Token} in the template with the resolved value.
        string resolved = template;
        foreach (var (key, value) in tokens)
            resolved = resolved.Replace($"{{{key}}}", Sanitize(value), StringComparison.OrdinalIgnoreCase);

        // Replace any remaining un-resolved tokens with "Unknown".
        resolved = System.Text.RegularExpressions.Regex.Replace(
            resolved, @"\{[^}]+\}", "Unknown");

        return resolved;
    }

    /// <inheritdoc/>
    public async Task<bool> ExecuteMoveAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (!File.Exists(sourcePath))
        {
            _logger.LogWarning("Move skipped — source file not found: {Source}", sourcePath);
            return false;
        }

        // Ensure the destination directory exists.
        string? destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        // Resolve collision: if destination exists, find a free name.
        string finalDest = ResolveCollision(destinationPath);

        var delay = InitialRetryDelay;
        for (int attempt = 1; attempt <= MaxMoveAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                File.Move(sourcePath, finalDest);
                _logger.LogInformation("Moved {Source} → {Destination}", sourcePath, finalDest);
                return true;
            }
            catch (IOException ex) when (attempt < MaxMoveAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Move attempt {Attempt}/{Max} failed for {Source}; retrying in {Delay}ms.",
                    attempt, MaxMoveAttempts, sourcePath, (int)delay.TotalMilliseconds);

                await Task.Delay(delay, ct).ConfigureAwait(false);
                delay *= 2; // exponential back-off
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move {Source} → {Destination}.", sourcePath, finalDest);
                return false;
            }
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Token resolution
    // -------------------------------------------------------------------------

    private static Dictionary<string, string> BuildTokens(
        IngestionCandidate candidate,
        IReadOnlyDictionary<string, string>? extra)
    {
        // candidate.Metadata is a flat KV bag populated by the processor and scorer.
        // We try known claim keys; fall back to "Unknown" for absent keys.
        var meta = candidate.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"]     = meta.GetValueOrDefault("title",     "Unknown"),
            ["Author"]    = meta.GetValueOrDefault("author",    "Unknown"),
            ["Year"]      = meta.GetValueOrDefault("year",      "Unknown"),
            ["MediaType"] = candidate.DetectedMediaType?.ToString() ?? "Unknown",
            ["Extension"] = Path.GetExtension(candidate.Path).TrimStart('.'),
            ["Series"]    = meta.GetValueOrDefault("series",    "Unknown"),
            ["Publisher"] = meta.GetValueOrDefault("publisher", "Unknown"),
            // ── Phase 7: Hub-First template tokens ───────────────────────────────
            ["Category"]  = ResolveCategoryFromMediaType(candidate.DetectedMediaType),
            ["HubName"]   = meta.GetValueOrDefault("title",   "Unknown"),
            ["Format"]    = candidate.DetectedMediaType?.ToString() ?? "Unknown",
            ["Edition"]   = meta.GetValueOrDefault("edition", "Standard"),
        };

        // Merge caller-supplied extras (allow overriding built-ins).
        if (extra is not null)
            foreach (var (k, v) in extra)
                tokens[k] = v;

        return tokens;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Removes characters that are illegal in a path segment.
    /// Collapses multiple spaces and trims the result.
    /// </summary>
    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (Array.IndexOf(InvalidPathChars, c) >= 0)
                sb.Append('_');
            else
                sb.Append(c);
        }

        // Collapse sequences of spaces and trim.
        string result = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s{2,}", " ").Trim();
        return string.IsNullOrEmpty(result) ? "Unknown" : result;
    }

    /// <summary>
    /// Maps a <see cref="MediaType"/> to a broad human-readable category
    /// used as the top-level directory in the Hub-first organisation template.
    /// </summary>
    private static string ResolveCategoryFromMediaType(MediaType? mt) => mt switch
    {
        MediaType.Epub      => "Books",
        MediaType.Comic     => "Comics",
        MediaType.Movie     => "Videos",
        MediaType.Audiobook => "Audio",
        _                   => "Other",
    };

    /// <summary>
    /// If <paramref name="path"/> does not exist, returns it unchanged.
    /// Otherwise appends " (2)", " (3)", … until a free path is found.
    /// </summary>
    private static string ResolveCollision(string path)
    {
        if (!File.Exists(path)) return path;

        string dir  = Path.GetDirectoryName(path) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(path);
        string ext  = Path.GetExtension(path);

        for (int i = 2; i < 10_000; i++)
        {
            string candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }

        // Extremely unlikely: fall back to a GUID suffix.
        return Path.Combine(dir, $"{stem} ({Guid.NewGuid():N}){ext}");
    }
}
