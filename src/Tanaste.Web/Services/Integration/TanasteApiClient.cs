using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Tanaste.Web.Models.ViewDTOs;
using System.Net;

namespace Tanaste.Web.Services.Integration;

/// <summary>
/// Strongly-typed HTTP client for the Tanaste API.
/// Registered via <c>AddHttpClient&lt;TanasteApiClient&gt;</c> in Program.cs so the
/// base address and X-Api-Key header are injected once at startup.
/// </summary>
public sealed class TanasteApiClient : ITanasteApiClient
{
    private readonly HttpClient _http;

    public TanasteApiClient(HttpClient http) => _http = http;

    // ── GET /system/status ────────────────────────────────────────────────────

    public async Task<SystemStatusViewModel?> GetSystemStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var raw = await _http.GetFromJsonAsync<StatusRaw>("/system/status", ct);
            return raw is null ? null : new SystemStatusViewModel
            {
                Status  = raw.Status,
                Version = raw.Version,
            };
        }
        catch { return null; }
    }

    // ── GET /hubs ─────────────────────────────────────────────────────────────

    public async Task<List<HubViewModel>> GetHubsAsync(CancellationToken ct = default)
    {
        try
        {
            var raw = await _http.GetFromJsonAsync<List<HubRaw>>("/hubs", ct);
            return raw?.Select(MapHub).ToList() ?? [];
        }
        catch { return []; }
    }

    // ── POST /ingestion/scan ──────────────────────────────────────────────────

    public async Task<ScanResultViewModel?> TriggerScanAsync(
        string? rootPath = null,
        CancellationToken ct = default)
    {
        try
        {
            var body    = new { root_path = rootPath };
            var resp    = await _http.PostAsJsonAsync("/ingestion/scan", body, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var raw     = await resp.Content.ReadFromJsonAsync<ScanRaw>(ct);
            return raw is null ? null : new ScanResultViewModel
            {
                Operations = raw.Operations.Select(o => new PendingOperationViewModel
                {
                    SourcePath      = o.SourcePath,
                    DestinationPath = o.DestinationPath,
                    OperationKind   = o.OperationKind,
                    Reason          = o.Reason,
                }).ToList(),
            };
        }
        catch { return null; }
    }

    // ── POST /ingestion/library-scan ─────────────────────────────────────────

    public async Task<LibraryScanResultViewModel?> TriggerLibraryScanAsync(
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/ingestion/library-scan", new { }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<LibraryScanResultViewModel>(ct);
        }
        catch { return null; }
    }

    // ── PATCH /metadata/resolve ───────────────────────────────────────────────

    public async Task<bool> ResolveMetadataAsync(
        Guid entityId, string claimKey, string chosenValue, CancellationToken ct = default)
    {
        try
        {
            var body = new { entity_id = entityId, claim_key = claimKey, chosen_value = chosenValue };
            using var req = new HttpRequestMessage(new HttpMethod("PATCH"), "/metadata/resolve")
            {
                Content = JsonContent.Create(body),
            };
            var resp = await _http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── GET /hubs/search ─────────────────────────────────────────────────────

    public async Task<List<SearchResultViewModel>> SearchWorksAsync(
        string query,
        CancellationToken ct = default)
    {
        try
        {
            var encoded = WebUtility.UrlEncode(query);
            var raw = await _http.GetFromJsonAsync<List<SearchRawResult>>(
                $"/hubs/search?q={encoded}", ct);
            return raw?.Select(r => new SearchResultViewModel
            {
                WorkId         = r.WorkId,
                HubId          = r.HubId,
                Title          = r.Title,
                Author         = r.Author,
                MediaType      = r.MediaType,
                HubDisplayName = r.HubDisplayName,
            }).ToList() ?? [];
        }
        catch { return []; }
    }

    // ── /admin/api-keys ───────────────────────────────────────────────────────

    public async Task<List<ApiKeyViewModel>> GetApiKeysAsync(CancellationToken ct = default)
    {
        try
        {
            var raw = await _http.GetFromJsonAsync<List<ApiKeyRaw>>("/admin/api-keys", ct);
            return raw?.Select(r => new ApiKeyViewModel
            {
                Id        = r.Id,
                Label     = r.Label,
                CreatedAt = r.CreatedAt,
            }).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<NewApiKeyViewModel?> CreateApiKeyAsync(
        string label,
        CancellationToken ct = default)
    {
        try
        {
            var body = new { label };
            var resp = await _http.PostAsJsonAsync("/admin/api-keys", body, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var raw  = await resp.Content.ReadFromJsonAsync<NewApiKeyRaw>(ct);
            return raw is null ? null : new NewApiKeyViewModel
            {
                Id        = raw.Id,
                Label     = raw.Label,
                Key       = raw.Key,
                CreatedAt = raw.CreatedAt,
            };
        }
        catch { return null; }
    }

    public async Task<bool> RevokeApiKeyAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/admin/api-keys/{id}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── /settings ─────────────────────────────────────────────────────────────

    public async Task<FolderSettingsDto?> GetFolderSettingsAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<FolderSettingsDto>("/settings/folders", ct);
        }
        catch { return null; }
    }

    public async Task<bool> UpdateFolderSettingsAsync(
        FolderSettingsDto settings,
        CancellationToken ct = default)
    {
        try
        {
            var body = new { watch_directory = settings.WatchDirectory, library_root = settings.LibraryRoot };
            var resp = await _http.PutAsJsonAsync("/settings/folders", body, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<PathTestResultDto?> TestPathAsync(
        string            path,
        CancellationToken ct = default)
    {
        try
        {
            var body = new { path };
            var resp = await _http.PostAsJsonAsync("/settings/test-path", body, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PathTestResultDto>(ct);
        }
        catch { return null; }
    }

    public async Task<IReadOnlyList<ProviderStatusDto>> GetProviderStatusAsync(
        CancellationToken ct = default)
    {
        try
        {
            var raw = await _http.GetFromJsonAsync<ProviderStatusDto[]>("/settings/providers", ct);
            return raw ?? [];
        }
        catch { return []; }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    private static HubViewModel MapHub(HubRaw h) => HubViewModel.FromApiDto(
        h.Id,
        h.UniverseId,
        h.CreatedAt,
        h.Works.Select(w => new WorkViewModel
        {
            Id              = w.Id,
            HubId           = w.HubId,
            MediaType       = w.MediaType,
            SequenceIndex   = w.SequenceIndex,
            CanonicalValues = w.CanonicalValues.Select(cv => new CanonicalValueViewModel
            {
                Key          = cv.Key,
                Value        = cv.Value,
                LastScoredAt = cv.LastScoredAt,
            }).ToList(),
        }));

    // ── Raw response shapes (mirror API Dtos.cs) ──────────────────────────────

    private sealed record StatusRaw(
        [property: JsonPropertyName("status")]  string Status,
        [property: JsonPropertyName("version")] string Version);

    private sealed record HubRaw(
        [property: JsonPropertyName("id")]           Guid                 Id,
        [property: JsonPropertyName("universe_id")]  Guid?                UniverseId,
        [property: JsonPropertyName("created_at")]   DateTimeOffset       CreatedAt,
        [property: JsonPropertyName("works")]        List<WorkRaw>        Works);

    private sealed record WorkRaw(
        [property: JsonPropertyName("id")]               Guid                      Id,
        [property: JsonPropertyName("hub_id")]           Guid                      HubId,
        [property: JsonPropertyName("media_type")]       string                    MediaType,
        [property: JsonPropertyName("sequence_index")]   int?                      SequenceIndex,
        [property: JsonPropertyName("canonical_values")] List<CanonicalValueRaw>   CanonicalValues);

    private sealed record CanonicalValueRaw(
        [property: JsonPropertyName("key")]            string        Key,
        [property: JsonPropertyName("value")]          string        Value,
        [property: JsonPropertyName("last_scored_at")] DateTimeOffset LastScoredAt);

    private sealed record ScanRaw(
        [property: JsonPropertyName("operations")] List<OperationRaw> Operations);

    private sealed record OperationRaw(
        [property: JsonPropertyName("source_path")]      string  SourcePath,
        [property: JsonPropertyName("destination_path")] string  DestinationPath,
        [property: JsonPropertyName("operation_kind")]   string  OperationKind,
        [property: JsonPropertyName("reason")]           string? Reason);

    private sealed record SearchRawResult(
        [property: JsonPropertyName("work_id")]          Guid    WorkId,
        [property: JsonPropertyName("hub_id")]           Guid    HubId,
        [property: JsonPropertyName("title")]            string  Title,
        [property: JsonPropertyName("author")]           string? Author,
        [property: JsonPropertyName("media_type")]       string  MediaType,
        [property: JsonPropertyName("hub_display_name")] string  HubDisplayName);

    private sealed record ApiKeyRaw(
        [property: JsonPropertyName("id")]         Guid           Id,
        [property: JsonPropertyName("label")]      string         Label,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

    private sealed record NewApiKeyRaw(
        [property: JsonPropertyName("id")]         Guid           Id,
        [property: JsonPropertyName("label")]      string         Label,
        [property: JsonPropertyName("key")]        string         Key,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
}
