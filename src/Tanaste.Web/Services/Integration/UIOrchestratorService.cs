using Microsoft.AspNetCore.SignalR.Client;
using Tanaste.Web.Models.ViewDTOs;

namespace Tanaste.Web.Services.Integration;

/// <summary>
/// Scoped orchestrator: the single bridge between <see cref="ITanasteApiClient"/>,
/// the <see cref="UniverseStateContainer"/>, and the Tanaste API Intercom SignalR hub.
///
/// <para>
/// <b>Lifecycle:</b> one instance per Blazor Server circuit.  Components call
/// <see cref="StartSignalRAsync"/> during their first <c>OnInitializedAsync</c>
/// to activate the real-time channel for that circuit.
/// </para>
///
/// <para>
/// <b>SignalR events handled:</b>
/// <list type="bullet">
///   <item><c>"MediaAdded"</c> — invalidates the hub cache; next navigation triggers a fresh load.</item>
///   <item><c>"IngestionProgress"</c> — updates progress state in the container for live UI feedback.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Non-fatal connection failure:</b> if the API is offline when
/// <see cref="StartSignalRAsync"/> is called, the connection attempt is swallowed
/// and logged at Warning level.  The UI degrades gracefully to HTTP-only mode.
/// </para>
/// </summary>
public sealed class UIOrchestratorService : IAsyncDisposable
{
    private readonly ITanasteApiClient              _api;
    private readonly UniverseStateContainer         _state;
    private readonly IConfiguration                 _config;
    private readonly ILogger<UIOrchestratorService> _logger;

    private HubConnection? _hubConnection;

    public UIOrchestratorService(
        ITanasteApiClient              api,
        UniverseStateContainer         state,
        IConfiguration                 config,
        ILogger<UIOrchestratorService> logger)
    {
        _api    = api;
        _state  = state;
        _config = config;
        _logger = logger;
    }

    // ── Hubs ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the hub list, using the state container cache when available.
    /// Pass <paramref name="forceRefresh"/> = <see langword="true"/> to bypass the cache.
    /// </summary>
    public async Task<List<HubViewModel>> GetHubsAsync(
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (_state.IsLoaded && !forceRefresh)
            return [.. _state.Hubs];

        var hubs = await _api.GetHubsAsync(ct);
        _state.SetHubs(hubs);   // also rebuilds UniverseViewModel via UniverseMapper
        return hubs;
    }

    /// <summary>
    /// Returns the flattened <see cref="UniverseViewModel"/>, loading hub data
    /// from the API if not already cached.
    /// </summary>
    public async Task<UniverseViewModel> GetUniverseAsync(
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        // GetHubsAsync populates _state.Universe via UniverseMapper inside SetHubs.
        await GetHubsAsync(forceRefresh, ct);
        return _state.Universe ?? UniverseMapper.MapFromHubs([]);
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

    // ── Search ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches works across all hubs.  Returns an empty list on failure or when
    /// the query is shorter than 2 characters (enforced server-side too).
    /// </summary>
    public Task<List<SearchResultViewModel>> SearchWorksAsync(
        string query,
        CancellationToken ct = default)
        => _api.SearchWorksAsync(query, ct);

    // ── API Key Management ────────────────────────────────────────────────────

    /// <summary>Lists all issued Guest API Keys (id, label, created_at only).</summary>
    public Task<List<ApiKeyViewModel>> GetApiKeysAsync(CancellationToken ct = default)
        => _api.GetApiKeysAsync(ct);

    /// <summary>Generates a new Guest API Key. The returned plaintext is shown exactly once.</summary>
    public Task<NewApiKeyViewModel?> CreateApiKeyAsync(string label, CancellationToken ct = default)
        => _api.CreateApiKeyAsync(label, ct);

    /// <summary>Revokes a Guest API Key. Any session using the key receives 401 immediately.</summary>
    public Task<bool> RevokeApiKeyAsync(Guid id, CancellationToken ct = default)
        => _api.RevokeApiKeyAsync(id, ct);

    // ── Settings ──────────────────────────────────────────────────────────────

    /// <summary>Returns the current Watch Folder and Library Folder configuration.</summary>
    public Task<FolderSettingsDto?> GetFolderSettingsAsync(CancellationToken ct = default)
        => _api.GetFolderSettingsAsync(ct);

    /// <summary>Saves updated folder paths to the Engine manifest and hot-swaps the file watcher.</summary>
    public Task<bool> UpdateFolderSettingsAsync(FolderSettingsDto settings, CancellationToken ct = default)
        => _api.UpdateFolderSettingsAsync(settings, ct);

    /// <summary>Probes a directory path for existence, read, and write access.</summary>
    public Task<PathTestResultDto?> TestPathAsync(string path, CancellationToken ct = default)
        => _api.TestPathAsync(path, ct);

    /// <summary>Returns enabled state and live reachability for all registered metadata providers.</summary>
    public Task<IReadOnlyList<ProviderStatusDto>> GetProviderStatusAsync(CancellationToken ct = default)
        => _api.GetProviderStatusAsync(ct);

    // ── SignalR Intercom ───────────────────────────────────────────────────────

    /// <summary>
    /// Starts the SignalR connection to the Tanaste API Intercom hub at
    /// <c>{TanasteApi:BaseUrl}/hubs/intercom</c>.
    ///
    /// <para>Idempotent — calling this multiple times is safe; the connection
    /// is only created and started once per circuit lifetime.</para>
    ///
    /// <para>Connection failure is non-fatal: the warning is logged and the
    /// UI continues in HTTP-only mode.</para>
    /// </summary>
    public async Task StartSignalRAsync(CancellationToken ct = default)
    {
        if (_hubConnection is not null)
            return; // Already initialised for this circuit.

        var baseUrl = _config["TanasteApi:BaseUrl"] ?? "http://localhost:61495";
        var apiKey  = _config["TanasteApi:ApiKey"]  ?? string.Empty;
        var hubUrl  = $"{baseUrl.TrimEnd('/')}/hubs/intercom";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Pass the API key as a request header so ApiKeyMiddleware
                // accepts the WebSocket upgrade request.
                if (!string.IsNullOrEmpty(apiKey))
                    options.Headers.Add("X-Api-Key", apiKey);
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
            })
            .Build();

        // ── "MediaAdded" ──────────────────────────────────────────────────────
        // A new Work has been committed to the library.
        // Invalidate the cache so the grid refreshes on next render.
        _hubConnection.On<MediaAddedEvent>("MediaAdded", ev =>
        {
            _logger.LogInformation(
                "Intercom ← MediaAdded: WorkId={WorkId} Title=\"{Title}\" Type={MediaType}",
                ev.WorkId, ev.Title, ev.MediaType);
            _state.PushMediaAdded(ev);
        });

        // ── "IngestionProgress" ───────────────────────────────────────────────
        // Active ingestion tick — update the progress indicator.
        _hubConnection.On<IngestionProgressEvent>("IngestionProgress", ev =>
        {
            _logger.LogDebug(
                "Intercom ← IngestionProgress: [{Stage}] {Done}/{Total} — {File}",
                ev.Stage, ev.ProcessedCount, ev.TotalCount, ev.CurrentFile);
            _state.PushIngestionProgress(ev);
        });

        // ── "MetadataHarvested" ───────────────────────────────────────────────
        // An external provider updated cover art / description / narrator etc.
        // Invalidate the state cache so cards re-render with the new data.
        _hubConnection.On<MetadataHarvestedEvent>("MetadataHarvested", ev =>
        {
            _logger.LogDebug(
                "Intercom ← MetadataHarvested: EntityId={Id} Provider={Provider} Fields=[{Fields}]",
                ev.EntityId, ev.ProviderName, string.Join(",", ev.UpdatedFields));
            _state.Invalidate();
        });

        // ── "PersonEnriched" ──────────────────────────────────────────────────
        // Wikidata has enriched an author/narrator with a headshot + biography.
        _hubConnection.On<PersonEnrichedEvent>("PersonEnriched", ev =>
        {
            _logger.LogDebug(
                "Intercom ← PersonEnriched: PersonId={Id} Name={Name}",
                ev.PersonId, ev.Name);
            _state.PushPersonEnriched(ev);
        });

        // ── "WatchFolderActive" ───────────────────────────────────────────────
        // The Watch Folder has been updated; notify state container so interested
        // components (e.g. Settings page connection indicator) can react.
        _hubConnection.On<WatchFolderActiveEvent>("WatchFolderActive", ev =>
        {
            _logger.LogInformation(
                "Intercom ← WatchFolderActive: Dir={Dir} At={At}",
                ev.WatchDirectory, ev.ActivatedAt);
            _state.PushWatchFolderActive(ev);
        });

        // ── Connection lifecycle logging ──────────────────────────────────────
        _hubConnection.Reconnecting += ex =>
        {
            _logger.LogWarning("Intercom reconnecting: {Message}", ex?.Message);
            return Task.CompletedTask;
        };
        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Intercom reconnected (connectionId={Id})", connectionId);
            return Task.CompletedTask;
        };
        _hubConnection.Closed += ex =>
        {
            _logger.LogWarning("Intercom closed: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync(ct);
            _logger.LogInformation("Intercom connected → {Url}", hubUrl);
        }
        catch (Exception ex)
        {
            // Non-fatal: degrade gracefully to HTTP-only mode.
            _logger.LogWarning(ex,
                "Could not connect to Intercom hub at {Url} — real-time updates disabled.", hubUrl);
        }
    }

    /// <summary>
    /// Whether the SignalR connection is currently established.
    /// Useful for rendering a live-indicator badge in the app bar.
    /// </summary>
    public bool IsIntercomConnected =>
        _hubConnection?.State == HubConnectionState.Connected;

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}
