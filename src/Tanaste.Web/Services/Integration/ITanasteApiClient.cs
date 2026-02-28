using Tanaste.Web.Models.ViewDTOs;

namespace Tanaste.Web.Services.Integration;

/// <summary>
/// Strongly-typed HTTP client for the Tanaste API.
/// All methods are fire-and-forget safe: they return null / empty list on failure
/// rather than throwing, so callers control error display.
/// </summary>
public interface ITanasteApiClient
{
    /// <summary>GET /system/status — lightweight connectivity probe.</summary>
    Task<SystemStatusViewModel?> GetSystemStatusAsync(CancellationToken ct = default);

    /// <summary>GET /hubs — full hub list with works and canonical values.</summary>
    Task<List<HubViewModel>> GetHubsAsync(CancellationToken ct = default);

    /// <summary>POST /ingestion/scan — dry-run scan of a directory path.</summary>
    Task<ScanResultViewModel?> TriggerScanAsync(string? rootPath = null, CancellationToken ct = default);

    /// <summary>
    /// POST /ingestion/library-scan — Great Inhale: reads tanaste.xml sidecars in the
    /// Library Root and hydrates the database. XML always wins on conflict.
    /// Returns null on failure.
    /// </summary>
    Task<LibraryScanResultViewModel?> TriggerLibraryScanAsync(CancellationToken ct = default);

    /// <summary>PATCH /metadata/resolve — manually override a metadata canonical value.</summary>
    Task<bool> ResolveMetadataAsync(
        Guid   entityId,
        string claimKey,
        string chosenValue,
        CancellationToken ct = default);

    /// <summary>GET /hubs/search?q= — full-text search across all works (min 2 chars).</summary>
    Task<List<SearchResultViewModel>> SearchWorksAsync(
        string query,
        CancellationToken ct = default);

    // ── API key management (/admin/api-keys) ──────────────────────────────────

    /// <summary>GET /admin/api-keys — list all issued keys (id, label, created_at).</summary>
    Task<List<ApiKeyViewModel>> GetApiKeysAsync(CancellationToken ct = default);

    /// <summary>POST /admin/api-keys — generate a new key. Returns key + one-time plaintext.</summary>
    Task<NewApiKeyViewModel?> CreateApiKeyAsync(string label, CancellationToken ct = default);

    /// <summary>DELETE /admin/api-keys/{id} — revoke a key immediately.</summary>
    Task<bool> RevokeApiKeyAsync(Guid id, CancellationToken ct = default);

    // ── Settings (/settings) ──────────────────────────────────────────────────

    /// <summary>GET /settings/folders — current Watch Folder + Library Folder paths.</summary>
    Task<FolderSettingsDto?> GetFolderSettingsAsync(CancellationToken ct = default);

    /// <summary>PUT /settings/folders — save paths to manifest and hot-swap the FileSystemWatcher.</summary>
    Task<bool> UpdateFolderSettingsAsync(FolderSettingsDto settings, CancellationToken ct = default);

    /// <summary>POST /settings/test-path — probe a directory for existence, read, and write access.</summary>
    Task<PathTestResultDto?> TestPathAsync(string path, CancellationToken ct = default);

    /// <summary>GET /settings/providers — enabled state and live reachability for all providers.</summary>
    Task<IReadOnlyList<ProviderStatusDto>> GetProviderStatusAsync(CancellationToken ct = default);
}
