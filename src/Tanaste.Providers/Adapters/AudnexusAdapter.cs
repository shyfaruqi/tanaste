using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Tanaste.Domain.Enums;
using Tanaste.Providers.Contracts;
using Tanaste.Providers.Models;
using Tanaste.Storage.Models;

namespace Tanaste.Providers.Adapters;

/// <summary>
/// Retrieves audiobook metadata from the Audnexus API (zero-key, public endpoint).
///
/// Audnexus is ASIN-based: there is no search-by-title endpoint, so a request
/// without an ASIN is skipped immediately rather than attempting a degraded
/// lookup.  Cover art, narrator, series, and series-position data from Audnexus
/// are among the highest-quality sources for Audible-sourced audiobooks.
///
/// Named HttpClient: <c>"audnexus"</c> (pre-configured with User-Agent header
/// and 10 s timeout in Program.cs).
///
/// Spec: Phase 9 – External Metadata Adapters § Audnexus.
/// </summary>
public sealed class AudnexusAdapter : IExternalMetadataProvider
{
    // Stable provider GUID — never change; written to metadata_claims.provider_id.
    public static readonly Guid AdapterProviderId
        = new("b2000002-a000-4000-8000-000000000003");

    // ── IExternalMetadataProvider ─────────────────────────────────────────────
    public string Name          => "audnexus";
    public ProviderDomain Domain => ProviderDomain.Audiobook;
    public IReadOnlyList<string> CapabilityTags => ["narrator", "series", "series_position", "cover", "author"];
    public Guid ProviderId      => AdapterProviderId;

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AudnexusAdapter> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    public AudnexusAdapter(
        IHttpClientFactory httpFactory,
        ILogger<AudnexusAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    // ── IExternalMetadataProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public bool CanHandle(MediaType mediaType) => mediaType == MediaType.Audiobook;

    /// <inheritdoc/>
    public bool CanHandle(EntityType entityType) =>
        entityType == EntityType.Work || entityType == EntityType.MediaAsset;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProviderClaim>> FetchAsync(
        ProviderLookupRequest request,
        CancellationToken ct = default)
    {
        // Audnexus has no search-by-title API; skip gracefully without ASIN.
        if (string.IsNullOrWhiteSpace(request.Asin))
        {
            _logger.LogDebug(
                "Audnexus skipped for entity {Id}: no ASIN in request", request.EntityId);
            return [];
        }

        var url = $"{request.BaseUrl.TrimEnd('/')}/books/{Uri.EscapeDataString(request.Asin)}";

        try
        {
            using var client = _httpFactory.CreateClient("audnexus");
            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);

            // 404 means the ASIN is not in Audnexus yet — not an error.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug(
                    "Audnexus: ASIN {Asin} not found (404) for entity {Id}",
                    request.Asin, request.EntityId);
                return [];
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content
                .ReadFromJsonAsync<JsonObject>(_jsonOptions, ct)
                .ConfigureAwait(false);

            return ParseBook(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex,
                "Audnexus adapter failed for ASIN {Asin} / entity {Id}",
                request.Asin, request.EntityId);
            return [];
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<ProviderClaim> ParseBook(JsonObject? json)
    {
        if (json is null)
            return [];

        var claims = new List<ProviderClaim>();

        // Narrators array — join multiple narrators as a comma-separated string.
        var narratorsArr = json["narrators"]?.AsArray();
        if (narratorsArr is { Count: > 0 })
        {
            var names = narratorsArr
                .Select(n => n?["name"]?.GetValue<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
            if (names.Count > 0)
                claims.Add(new ProviderClaim("narrator", string.Join(", ", names!), 0.9));
        }

        // Series name.
        var seriesName = json["seriesName"]?.GetValue<string>()
                      ?? json["series"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(seriesName))
            claims.Add(new ProviderClaim("series", seriesName, 0.9));

        // Series position (e.g. "1", "2.5").
        var seriesPosition = json["seriesPosition"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(seriesPosition))
            claims.Add(new ProviderClaim("series_position", seriesPosition, 0.9));

        // Cover image URL.
        var coverUrl = json["image"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(coverUrl))
            claims.Add(new ProviderClaim("cover", coverUrl, 0.9));

        // Authors array — join as comma-separated string.
        var authorsArr = json["authors"]?.AsArray();
        if (authorsArr is { Count: > 0 })
        {
            var names = authorsArr
                .Select(a => a?["name"]?.GetValue<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
            if (names.Count > 0)
                claims.Add(new ProviderClaim("author", string.Join(", ", names!), 0.75));
        }

        return claims;
    }
}
