using Tanaste.Web.Models.ViewDTOs;

namespace Tanaste.Web.Services.Integration;

/// <summary>
/// Scoped state container (one per Blazor Server circuit) that caches the
/// current Library view and surfaces real-time event data received from
/// the Tanaste API Intercom SignalR hub.
///
/// <para>
/// <b>Thread safety:</b> SignalR event handlers run on a background thread.
/// Components that subscribe to <see cref="OnStateChanged"/> MUST dispatch
/// re-renders via <c>InvokeAsync(StateHasChanged)</c> — never bare
/// <c>StateHasChanged()</c>.
/// </para>
/// </summary>
public sealed class UniverseStateContainer
{
    private List<HubViewModel>         _hubs                       = [];
    private HubViewModel?              _selected;
    private UniverseViewModel?         _universe;
    private bool                       _loaded;
    private IngestionProgressEvent?    _ingestionProgress;
    private WatchFolderActiveEvent?    _latestWatchFolderActivation;
    private readonly List<PersonEnrichedEvent> _personUpdates = [];

    // ── Read-only surface ─────────────────────────────────────────────────────

    public IReadOnlyList<HubViewModel> Hubs              => _hubs;
    public HubViewModel?               Selected          => _selected;

    /// <summary>
    /// Flattened cross-media-type view built by <see cref="UniverseMapper"/>.
    /// Null until the first successful hub load; components should guard with
    /// <c>@if (State.Universe is { } u)</c>.
    /// </summary>
    public UniverseViewModel?          Universe          => _universe;

    public bool                        IsLoaded          => _loaded;

    /// <summary>
    /// Latest ingestion progress snapshot pushed via SignalR.
    /// Null when no ingestion is in progress or the circuit is freshly created.
    /// </summary>
    public IngestionProgressEvent?          IngestionProgress           => _ingestionProgress;
    public IReadOnlyList<PersonEnrichedEvent> RecentPersonUpdates        => _personUpdates;

    /// <summary>
    /// The most recent <c>"WatchFolderActive"</c> event received via SignalR.
    /// Null until the watch folder has been configured or changed in the current circuit.
    /// </summary>
    public WatchFolderActiveEvent?          LatestWatchFolderActivation => _latestWatchFolderActivation;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires whenever the hub list, selected hub, universe view, or
    /// ingestion progress changes.  May fire from a SignalR background thread —
    /// use <c>InvokeAsync(StateHasChanged)</c> in component handlers.
    /// </summary>
    public event Action? OnStateChanged;

    // ── Hub-list mutations ────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the cached hub list and rebuilds the flattened
    /// <see cref="UniverseViewModel"/> via <see cref="UniverseMapper"/>.
    /// </summary>
    public void SetHubs(IEnumerable<HubViewModel> hubs)
    {
        _hubs     = hubs.ToList();
        _universe = UniverseMapper.MapFromHubs(_hubs);
        _loaded   = true;
        OnStateChanged?.Invoke();
    }

    public void SelectHub(HubViewModel? hub)
    {
        _selected = hub;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears all cached data.  The next call to
    /// <c>UIOrchestratorService.GetHubsAsync()</c> will trigger a fresh API fetch.
    /// </summary>
    public void Invalidate()
    {
        _hubs              = [];
        _selected          = null;
        _universe          = null;
        _loaded            = false;
        _ingestionProgress = null;
        OnStateChanged?.Invoke();
    }

    // ── Real-time event sinks (called by UIOrchestratorService) ───────────────

    /// <summary>
    /// Called when an <c>"IngestionProgress"</c> event arrives on the Intercom hub.
    /// Updates the progress indicator and notifies subscribed components.
    /// </summary>
    public void PushIngestionProgress(IngestionProgressEvent ev)
    {
        _ingestionProgress = ev;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Called when a <c>"MediaAdded"</c> event arrives on the Intercom hub.
    /// Invalidates the hub cache so the next navigation triggers a fresh load
    /// with the new Work included.
    /// </summary>
    public void PushMediaAdded(MediaAddedEvent ev) => Invalidate();

    /// <summary>
    /// Called when a <c>"PersonEnriched"</c> event arrives on the Intercom hub.
    /// Keeps a rolling buffer of the 50 most recent person updates.
    /// </summary>
    public void PushPersonEnriched(PersonEnrichedEvent ev)
    {
        _personUpdates.Add(ev);
        // Keep only the 50 most recent person updates to avoid unbounded growth.
        if (_personUpdates.Count > 50)
            _personUpdates.RemoveAt(0);
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Called when a <c>"WatchFolderActive"</c> event arrives on the Intercom hub.
    /// Updates <see cref="LatestWatchFolderActivation"/> so components can react
    /// to a watch folder change without a page reload.
    /// </summary>
    public void PushWatchFolderActive(WatchFolderActiveEvent ev)
    {
        _latestWatchFolderActivation = ev;
        OnStateChanged?.Invoke();
    }
}
