using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Tanaste.Domain.Enums;
using Tanaste.Providers.Contracts;
using Tanaste.Providers.Models;
using Tanaste.Storage.Models;

namespace Tanaste.Providers.Adapters;

/// <summary>
/// Retrieves metadata from Apple's iTunes Search API (zero-key, public endpoint).
///
/// Handles both ebook and audiobook domains — the entity type query parameter
/// (<c>ebook</c> vs <c>audiobook</c>) differs between the two, which is why
/// <c>apple_books_ebook</c> and <c>apple_books_audiobook</c> are separate entries
/// in <c>tanaste_master.json</c> with independent field-weight profiles.
///
/// Throttle: 1 concurrent request + 300 ms minimum gap (Apple rate limits are
/// undocumented but conservative spacing avoids 503 responses).
///
/// Spec: Phase 9 – External Metadata Adapters § Apple Books.
/// </summary>
public sealed partial class AppleBooksAdapter : IExternalMetadataProvider
{
    // ── Stable provider GUIDs ─────────────────────────────────────────────────
    // These must never change; they are written to metadata_claims.provider_id.
    public static readonly Guid EbookProviderId
        = new("b1000001-e000-4000-8000-000000000001");
    public static readonly Guid AudiobookProviderId
        = new("b1000001-a000-4000-8000-000000000002");

    // ── IExternalMetadataProvider ─────────────────────────────────────────────
    public string Name => _mediaType == MediaType.Epub
        ? "apple_books_ebook"
        : "apple_books_audiobook";

    public ProviderDomain Domain => _mediaType == MediaType.Epub
        ? ProviderDomain.Ebook
        : ProviderDomain.Audiobook;

    public IReadOnlyList<string> CapabilityTags => _mediaType == MediaType.Epub
        ? ["cover", "description", "rating", "title"]
        : ["cover", "description", "rating", "title", "narrator"];

    public Guid ProviderId => _mediaType == MediaType.Epub
        ? EbookProviderId
        : AudiobookProviderId;

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly IHttpClientFactory _httpFactory;
    private readonly MediaType _mediaType;
    private readonly ILogger<AppleBooksAdapter> _logger;

    // Throttle: max 1 concurrent request, enforced globally across instances.
    // Stored as a static so all DI-injected instances share the same gate.
    private static readonly SemaphoreSlim _throttle = new(1, 1);
    private static DateTime _lastCallUtc = DateTime.MinValue;
    private const int ThrottleGapMs = 300;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="httpFactory">Provides the named <c>"apple_books"</c> client.</param>
    /// <param name="mediaType">
    /// Pass <see cref="MediaType.Epub"/> for the ebook variant or
    /// <see cref="MediaType.Audiobook"/> for the audiobook variant.
    /// One DI registration per variant.
    /// </param>
    /// <param name="logger">Logger for warning on network failures.</param>
    public AppleBooksAdapter(
        IHttpClientFactory httpFactory,
        MediaType mediaType,
        ILogger<AppleBooksAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _httpFactory = httpFactory;
        _mediaType   = mediaType;
        _logger      = logger;
    }

    // ── IExternalMetadataProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public bool CanHandle(MediaType mediaType) =>
        mediaType == MediaType.Epub || mediaType == MediaType.Audiobook;

    /// <inheritdoc/>
    public bool CanHandle(EntityType entityType) =>
        entityType == EntityType.Work || entityType == EntityType.MediaAsset;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProviderClaim>> FetchAsync(
        ProviderLookupRequest request,
        CancellationToken ct = default)
    {
        // This adapter only handles the media type it was constructed for.
        if (request.MediaType != _mediaType)
            return [];

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            _logger.LogDebug("Apple Books skipped: no title in request for entity {Id}",
                request.EntityId);
            return [];
        }

        var entityParam = _mediaType == MediaType.Epub ? "ebook" : "audiobook";
        var term = Uri.EscapeDataString(
            string.IsNullOrWhiteSpace(request.Author)
                ? request.Title
                : $"{request.Title} {request.Author}");
        var url = $"{request.BaseUrl.TrimEnd('/')}/search?term={term}&entity={entityParam}&limit=5";

        try
        {
            await _throttle.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Enforce minimum inter-call gap.
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastCallUtc).TotalMilliseconds;
                if (elapsed < ThrottleGapMs)
                    await Task.Delay(TimeSpan.FromMilliseconds(ThrottleGapMs - elapsed), ct)
                              .ConfigureAwait(false);

                using var client = _httpFactory.CreateClient("apple_books");
                using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
                _lastCallUtc = DateTime.UtcNow;
                response.EnsureSuccessStatusCode();

                var json = await response.Content
                    .ReadFromJsonAsync<JsonObject>(_jsonOptions, ct)
                    .ConfigureAwait(false);

                return ParseResults(json);
            }
            finally
            {
                _throttle.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation; don't swallow it.
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex,
                "Apple Books adapter failed for entity {Id} (URL: {Url})", request.EntityId, url);
            return [];
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<ProviderClaim> ParseResults(JsonObject? json)
    {
        if (json is null)
            return [];

        var results = json["results"]?.AsArray();
        if (results is null || results.Count == 0)
            return [];

        // Use the first result — best match by Apple's relevance ranking.
        var item = results[0]?.AsObject();
        if (item is null)
            return [];

        var claims = new List<ProviderClaim>();

        // Cover art: Apple returns 100×100; upgrade to 600×600.
        var artworkUrl = item["artworkUrl100"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(artworkUrl))
        {
            var highRes = artworkUrl.Replace("100x100bb", "600x600bb",
                StringComparison.OrdinalIgnoreCase);
            claims.Add(new ProviderClaim("cover", highRes, 0.85));
        }

        // Description (may contain HTML — strip tags).
        var description = item["description"]?.GetValue<string>()
                       ?? item["shortDescription"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(description))
        {
            var stripped = HtmlTagRegex().Replace(description, string.Empty).Trim();
            if (stripped.Length > 0)
                claims.Add(new ProviderClaim("description", stripped, 0.85));
        }

        // Average user rating (stored as a numeric string).
        var rating = item["averageUserRating"]?.GetValue<double>();
        if (rating.HasValue)
            claims.Add(new ProviderClaim("rating", rating.Value.ToString("F2"), 0.8));

        // Title (lower confidence than file's own OPF title).
        var title = item["trackName"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(title))
            claims.Add(new ProviderClaim("title", title, 0.7));

        return claims;
    }

    // Compiled regex: strip all HTML tags from description strings.
    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();
}
