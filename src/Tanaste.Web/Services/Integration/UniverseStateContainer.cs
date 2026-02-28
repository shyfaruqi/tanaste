using Tanaste.Web.Models.ViewDTOs;

namespace Tanaste.Web.Services.Integration;

/// <summary>
/// Scoped state container (one per SignalR circuit) that caches the current
/// Library view.  Components subscribe to <see cref="OnStateChanged"/> to
/// re-render when the cache is refreshed.
/// </summary>
public sealed class UniverseStateContainer
{
    private List<HubViewModel>  _hubs    = [];
    private HubViewModel?       _selected;
    private bool                _loaded;

    public IReadOnlyList<HubViewModel> Hubs     => _hubs;
    public HubViewModel?               Selected => _selected;
    public bool                        IsLoaded => _loaded;

    /// <summary>Fires whenever the hub list or selected hub changes.</summary>
    public event Action? OnStateChanged;

    public void SetHubs(IEnumerable<HubViewModel> hubs)
    {
        _hubs   = hubs.ToList();
        _loaded = true;
        OnStateChanged?.Invoke();
    }

    public void SelectHub(HubViewModel? hub)
    {
        _selected = hub;
        OnStateChanged?.Invoke();
    }

    public void Invalidate()
    {
        _hubs    = [];
        _selected = null;
        _loaded  = false;
        OnStateChanged?.Invoke();
    }
}
