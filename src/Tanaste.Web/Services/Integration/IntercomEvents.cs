namespace Tanaste.Web.Services.Integration;

/// <summary>
/// Payload broadcast by the Tanaste API via SignalR when a new Work is
/// committed to the library.
///
/// SignalR method name: <c>"MediaAdded"</c>
///
/// Example handler:
/// <code>
/// hubConnection.On&lt;MediaAddedEvent&gt;("MediaAdded", ev => { ... });
/// </code>
/// </summary>
/// <param name="WorkId">The newly ingested Work's unique identifier.</param>
/// <param name="HubId">The Hub this Work was assigned to.</param>
/// <param name="MediaType">Domain media-type string (e.g. "Epub", "Video", "Cbz").</param>
/// <param name="Title">Best-available title for immediate display before a full hub refresh.</param>
public sealed record MediaAddedEvent(
    Guid   WorkId,
    Guid   HubId,
    string MediaType,
    string Title);

/// <summary>
/// Payload broadcast during an active ingestion run to report incremental progress.
///
/// SignalR method name: <c>"IngestionProgress"</c>
///
/// Example handler:
/// <code>
/// hubConnection.On&lt;IngestionProgressEvent&gt;("IngestionProgress", ev => { ... });
/// </code>
/// </summary>
/// <param name="CurrentFile">Short display name of the file currently being processed.</param>
/// <param name="ProcessedCount">Number of files processed so far in this run.</param>
/// <param name="TotalCount">Total files discovered for this run (0 if still scanning).</param>
/// <param name="Stage">
/// Human-readable stage label.  One of:
/// <c>"Scanning"</c> | <c>"Hashing"</c> | <c>"Processing"</c> | <c>"Complete"</c>
/// </param>
public sealed record IngestionProgressEvent(
    string CurrentFile,
    int    ProcessedCount,
    int    TotalCount,
    string Stage);
