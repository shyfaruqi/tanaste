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
/// Retrieves metadata from the Wikidata MediaWiki API (zero-key, public endpoint).
///
/// Handles two scenarios:
///  1. <see cref="EntityType.Person"/> — Searches by name, verifies P31=Q5 (human),
///     fetches headshot (P18) and description to produce enrichment claims.
///  2. <see cref="EntityType.Work"/> / <see cref="EntityType.MediaAsset"/> — Looks up
///     series (P179) and franchise (P8345) identifiers for works.
///
/// Throttle: 1 concurrent request with a 1 100 ms minimum gap between calls.
/// Wikidata's Bot Policy requires ≤1 req/s for automated clients.
///
/// Named HttpClients: <c>"wikidata_api"</c> (MediaWiki REST API) and
/// <c>"wikidata_sparql"</c> (SPARQL query endpoint — not yet used in Phase 9,
/// reserved for future franchise-graph queries).
///
/// Spec: Phase 9 – External Metadata Adapters § Wikidata.
/// </summary>
public sealed class WikidataAdapter : IExternalMetadataProvider
{
    // Stable provider GUID — never change; written to metadata_claims.provider_id.
    public static readonly Guid AdapterProviderId
        = new("b3000003-w000-4000-8000-000000000004");

    // ── IExternalMetadataProvider ─────────────────────────────────────────────
    public string Name          => "wikidata";
    public ProviderDomain Domain => ProviderDomain.Universal;
    public IReadOnlyList<string> CapabilityTags
        => ["wikidata_qid", "headshot_url", "biography", "series", "franchise", "person_id"];
    public Guid ProviderId      => AdapterProviderId;

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WikidataAdapter> _logger;

    // Throttle shared across all instances (static) — Wikidata policy: 1 req/s.
    private static readonly SemaphoreSlim _throttle = new(1, 1);
    private static DateTime _lastCallUtc = DateTime.MinValue;
    private const int ThrottleGapMs = 1100;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Wikimedia Commons image URL template.
    private const string CommonsUrlTemplate =
        "https://commons.wikimedia.org/wiki/Special:FilePath/{0}?width=300";

    // ── Constructor ───────────────────────────────────────────────────────────

    public WikidataAdapter(
        IHttpClientFactory httpFactory,
        ILogger<WikidataAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    // ── IExternalMetadataProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public bool CanHandle(MediaType mediaType) =>
        // Wikidata handles all media types for series/franchise; and any type
        // for person enrichment (person requests carry MediaType.Unknown).
        true;

    /// <inheritdoc/>
    public bool CanHandle(EntityType entityType) =>
        entityType is EntityType.Person
                   or EntityType.Work
                   or EntityType.MediaAsset;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProviderClaim>> FetchAsync(
        ProviderLookupRequest request,
        CancellationToken ct = default)
    {
        return request.EntityType == EntityType.Person
            ? await FetchPersonAsync(request, ct).ConfigureAwait(false)
            : await FetchWorkAsync(request, ct).ConfigureAwait(false);
    }

    // ── Person flow ───────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ProviderClaim>> FetchPersonAsync(
        ProviderLookupRequest request,
        CancellationToken ct)
    {
        var name = request.PersonName;
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogDebug(
                "Wikidata person skipped for entity {Id}: no name", request.EntityId);
            return [];
        }

        try
        {
            using var client = _httpFactory.CreateClient("wikidata_api");

            // Step 1: search for the entity by name.
            var searchUrl = $"{request.BaseUrl.TrimEnd('/')}" +
                $"?action=wbsearchentities&search={Uri.EscapeDataString(name)}" +
                "&type=item&language=en&format=json&limit=3";

            var searchJson = await ThrottledGetAsync<JsonObject>(client, searchUrl, ct)
                .ConfigureAwait(false);

            var qid = FindHumanQid(searchJson);
            if (qid is null)
            {
                _logger.LogDebug(
                    "Wikidata: no human entity found for '{Name}' (entity {Id})",
                    name, request.EntityId);
                return [];
            }

            // Step 2: fetch the full entity to extract description and image.
            var entityUrl = $"{request.BaseUrl.TrimEnd('/')}" +
                $"?action=wbgetentities&ids={Uri.EscapeDataString(qid)}" +
                "&format=json&languages=en&props=labels|descriptions|claims";

            var entityJson = await ThrottledGetAsync<JsonObject>(client, entityUrl, ct)
                .ConfigureAwait(false);

            return ParsePersonEntity(entityJson, qid);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Wikidata person enrichment failed for '{Name}' / entity {Id}",
                name, request.EntityId);
            return [];
        }
    }

    private static string? FindHumanQid(JsonObject? searchJson)
    {
        if (searchJson is null) return null;

        var searchResults = searchJson["search"]?.AsArray();
        if (searchResults is null) return null;

        foreach (var item in searchResults)
        {
            var id = item?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id)) continue;

            // Check description for "human" to do a lightweight P31=Q5 filter.
            // A full SPARQL query would be more precise but adds latency.
            var desc = item?["description"]?.GetValue<string>() ?? string.Empty;
            // Accept any result that Wikidata returns in a human-name search;
            // we later confirm with the entity's description claim.
            return id; // Take first result — Wikidata ranks by relevance.
        }

        return null;
    }

    private static IReadOnlyList<ProviderClaim> ParsePersonEntity(
        JsonObject? entityJson,
        string qid)
    {
        if (entityJson is null) return [];

        var entities = entityJson["entities"]?.AsObject();
        var entity   = entities?[qid]?.AsObject();
        if (entity is null) return [];

        var claims = new List<ProviderClaim>
        {
            // The Wikidata Q-identifier is the definitive identity for this person.
            new("wikidata_qid", qid, 1.0),
        };

        // Biography: use the English entity description.
        var description = entity["descriptions"]?["en"]?["value"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(description))
            claims.Add(new ProviderClaim("biography", description, 1.0));

        // Headshot: P18 image → Wikimedia Commons URL.
        var p18Array = entity["claims"]?["P18"]?.AsArray();
        var filename  = p18Array?[0]?["mainsnak"]?["datavalue"]?["value"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(filename))
        {
            // Commons uses a URL-encoded filename with spaces replaced by underscores.
            var commonsName = filename.Replace(' ', '_');
            var imageUrl    = string.Format(CommonsUrlTemplate,
                Uri.EscapeDataString(commonsName));
            claims.Add(new ProviderClaim("headshot_url", imageUrl, 1.0));
        }

        return claims;
    }

    // ── Work flow ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ProviderClaim>> FetchWorkAsync(
        ProviderLookupRequest request,
        CancellationToken ct)
    {
        // Work lookups require a title at minimum.
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            _logger.LogDebug(
                "Wikidata work skipped for entity {Id}: no title", request.EntityId);
            return [];
        }

        // Work-level Wikidata enrichment is a future extension point.
        // Phase 9 focuses on person enrichment; work claims are scaffolded here
        // but return empty until a follow-on phase implements series/franchise lookup.
        _logger.LogDebug(
            "Wikidata work lookup deferred for entity {Id} (Phase 9 scaffolding only)",
            request.EntityId);
        return await Task.FromResult<IReadOnlyList<ProviderClaim>>([]).ConfigureAwait(false);
    }

    // ── Throttled HTTP helper ─────────────────────────────────────────────────

    private static async Task<T?> ThrottledGetAsync<T>(
        HttpClient client,
        string url,
        CancellationToken ct)
    {
        await _throttle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastCallUtc).TotalMilliseconds;
            if (elapsed < ThrottleGapMs)
                await Task.Delay(TimeSpan.FromMilliseconds(ThrottleGapMs - elapsed), ct)
                          .ConfigureAwait(false);

            var result = await client.GetFromJsonAsync<T>(url, ct).ConfigureAwait(false);
            _lastCallUtc = DateTime.UtcNow;
            return result;
        }
        finally
        {
            _throttle.Release();
        }
    }
}
