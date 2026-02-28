using Tanaste.Web.Models.ViewDTOs;

namespace Tanaste.Web.Services.Integration;

/// <summary>
/// Scoped orchestrator: the single bridge between <see cref="ITanasteApiClient"/>
/// and the UI components.  All business logic (caching, refresh, error shaping)
/// lives here so components remain stateless dispatchers.
/// </summary>
public sealed class UIOrchestratorService
{
    private readonly ITanasteApiClient     _api;
    private readonly UniverseStateContainer _state;

    public UIOrchestratorService(ITanasteApiClient api, UniverseStateContainer state)
    {
        _api   = api;
        _state = state;
    }

    // ── Hubs ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the hub list, using the state container cache when available.
    /// Pass <paramref name="forceRefresh"/> = true to bypass the cache.
    /// </summary>
    public async Task<List<HubViewModel>> GetHubsAsync(
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (_state.IsLoaded && !forceRefresh)
            return [.. _state.Hubs];

        var hubs = await _api.GetHubsAsync(ct);
        _state.SetHubs(hubs);
        return hubs;
    }

    // ── System status ─────────────────────────────────────────────────────────

    public Task<SystemStatusViewModel?> GetSystemStatusAsync(CancellationToken ct = default)
        => _api.GetSystemStatusAsync(ct);

    // ── Ingestion ─────────────────────────────────────────────────────────────

    /// <summary>Triggers a dry-run scan and invalidates the hub cache on success.</summary>
    public async Task<ScanResultViewModel?> ScanAndRefreshAsync(
        string? rootPath = null,
        CancellationToken ct = default)
    {
        var result = await _api.TriggerScanAsync(rootPath, ct);
        if (result is not null)
            _state.Invalidate();
        return result;
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    /// <summary>Resolves a metadata conflict and invalidates the hub cache so the UI reflects it.</summary>
    public async Task<bool> ResolveMetadataAsync(
        Guid entityId, string claimKey, string chosenValue,
        CancellationToken ct = default)
    {
        var ok = await _api.ResolveMetadataAsync(entityId, claimKey, chosenValue, ct);
        if (ok)
            _state.Invalidate();
        return ok;
    }
}
