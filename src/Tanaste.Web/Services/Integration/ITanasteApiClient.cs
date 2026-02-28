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

    /// <summary>PATCH /metadata/resolve — manually override a metadata canonical value.</summary>
    Task<bool> ResolveMetadataAsync(
        Guid   entityId,
        string claimKey,
        string chosenValue,
        CancellationToken ct = default);
}
