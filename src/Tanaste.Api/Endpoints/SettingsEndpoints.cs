using Tanaste.Api.Models;
using Tanaste.Domain.Contracts;
using Tanaste.Domain.Events;
using Tanaste.Ingestion.Contracts;
using Tanaste.Storage.Contracts;

namespace Tanaste.Api.Endpoints;

/// <summary>
/// Settings endpoints for folder configuration, path testing, and provider status.
/// All routes are grouped under <c>/settings</c>.
///
/// <list type="bullet">
///   <item><c>GET    /settings/folders</c>   — current Watch Folder + Library Folder</item>
///   <item><c>PUT    /settings/folders</c>   — save paths to manifest + hot-swap FileSystemWatcher</item>
///   <item><c>POST   /settings/test-path</c> — probe a path for existence / read / write access</item>
///   <item><c>GET    /settings/providers</c> — enabled state + async reachability for each provider</item>
/// </list>
/// </summary>
public static class SettingsEndpoints
{
    // Maps provider name → human-readable display label.
    private static readonly IReadOnlyDictionary<string, string> _displayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["apple_books_ebook"]     = "Apple Books (Ebooks)",
            ["apple_books_audiobook"] = "Apple Books (Audiobooks)",
            ["audnexus"]              = "Audnexus",
            ["wikidata"]              = "Wikidata",
        };

    // Maps provider name → key in manifest.ProviderEndpoints for the reachability probe.
    private static readonly IReadOnlyDictionary<string, string> _endpointKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["apple_books_ebook"]     = "apple_books",
            ["apple_books_audiobook"] = "apple_books",
            ["audnexus"]              = "audnexus",
            ["wikidata"]              = "wikidata_api",
        };

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/settings").WithTags("Settings");

        // ── GET /settings/folders ──────────────────────────────────────────────

        grp.MapGet("/folders", (IStorageManifest storageManifest) =>
        {
            var m = storageManifest.Load();
            return Results.Ok(new FolderSettingsResponse
            {
                WatchDirectory = m.WatchDirectory,
                LibraryRoot    = m.LibraryRoot,
            });
        })
        .WithName("GetFolderSettings")
        .WithSummary("Returns the currently configured Watch Folder and Library Folder paths.")
        .Produces<FolderSettingsResponse>(StatusCodes.Status200OK);

        // ── PUT /settings/folders ──────────────────────────────────────────────

        grp.MapPut("/folders", async (
            UpdateFoldersRequest request,
            IStorageManifest     storageManifest,
            IFileWatcher         fileWatcher,
            IEventPublisher      publisher,
            CancellationToken    ct) =>
        {
            var manifest = storageManifest.Load();

            if (!string.IsNullOrWhiteSpace(request.WatchDirectory))
                manifest.WatchDirectory = request.WatchDirectory;

            if (!string.IsNullOrWhiteSpace(request.LibraryRoot))
                manifest.LibraryRoot = request.LibraryRoot;

            storageManifest.Save(manifest);

            // Hot-swap the FileSystemWatcher when the watch directory is provided and accessible.
            // Wrapped in try/catch because the watcher may not have been started yet in the
            // API process — the manifest save is the durable side-effect that matters.
            if (!string.IsNullOrWhiteSpace(request.WatchDirectory)
                && Directory.Exists(request.WatchDirectory))
            {
                try { fileWatcher.UpdateDirectory(request.WatchDirectory); }
                catch (Exception) { /* non-fatal: watcher swap failed; path is persisted to manifest. */ }
            }

            // Broadcast the new active watch path to all connected Dashboard circuits.
            await publisher.PublishAsync(
                "WatchFolderActive",
                new WatchFolderActiveEvent(manifest.WatchDirectory, DateTimeOffset.UtcNow),
                ct);

            return Results.Ok();
        })
        .WithName("UpdateFolderSettings")
        .WithSummary("Saves Watch Folder + Library Folder paths and hot-swaps the FileSystemWatcher.")
        .Produces(StatusCodes.Status200OK);

        // ── POST /settings/test-path ────────────────────────────────────────────

        grp.MapPost("/test-path", (TestPathRequest request) =>
        {
            var path   = request.Path ?? string.Empty;
            var exists = Directory.Exists(path);
            bool hasRead  = false;
            bool hasWrite = false;

            if (exists)
            {
                // Read probe: attempt to enumerate at least one entry.
                try
                {
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed — intentional probe.
                    Directory.EnumerateFileSystemEntries(path).Any();
                    hasRead = true;
                }
                catch { /* access denied or I/O error */ }

                // Write probe: create and immediately delete a sentinel file.
                try
                {
                    var probe = Path.Combine(path, $".tanaste_probe_{Guid.NewGuid():N}");
                    File.WriteAllText(probe, string.Empty);
                    File.Delete(probe);
                    hasWrite = true;
                }
                catch { /* read-only file system or access denied */ }
            }

            return Results.Ok(new TestPathResponse
            {
                Path     = path,
                Exists   = exists,
                HasRead  = hasRead,
                HasWrite = hasWrite,
            });
        })
        .WithName("TestPath")
        .WithSummary("Probes a directory path for existence, read access, and write access.")
        .Produces<TestPathResponse>(StatusCodes.Status200OK);

        // ── GET /settings/providers ─────────────────────────────────────────────

        grp.MapGet("/providers", async (
            IStorageManifest   storageManifest,
            IHttpClientFactory httpFactory,
            CancellationToken  ct) =>
        {
            var manifest = storageManifest.Load();
            var http     = httpFactory.CreateClient("settings_probe");

            // Check each provider's reachability concurrently.
            var statusTasks = manifest.Providers.Select(async provider =>
            {
                var name        = provider.Name;
                var displayName = _displayNames.TryGetValue(name, out var dn) ? dn : name;
                bool isReachable = false;

                if (provider.Enabled
                    && _endpointKeys.TryGetValue(name, out var epKey)
                    && manifest.ProviderEndpoints.TryGetValue(epKey, out var baseUrl)
                    && !string.IsNullOrWhiteSpace(baseUrl))
                {
                    using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    probeCts.CancelAfter(TimeSpan.FromSeconds(3));
                    try
                    {
                        using var req  = new HttpRequestMessage(HttpMethod.Head, baseUrl);
                        using var resp = await http.SendAsync(
                            req, HttpCompletionOption.ResponseHeadersRead, probeCts.Token);
                        // Any response (even 4xx) confirms the server is reachable.
                        // Only connection-level failures mean unreachable.
                        isReachable = (int)resp.StatusCode < 500;
                    }
                    catch { /* timeout / DNS failure / network error — isReachable stays false */ }
                }

                return new ProviderStatusResponse
                {
                    Name        = name,
                    DisplayName = displayName,
                    Enabled     = provider.Enabled,
                    IsZeroKey   = true, // All current providers are zero-key (no API credentials needed).
                    IsReachable = isReachable,
                };
            });

            return Results.Ok(await Task.WhenAll(statusTasks));
        })
        .WithName("GetProviderStatus")
        .WithSummary("Returns enabled/reachability status for all registered metadata providers.")
        .Produces<ProviderStatusResponse[]>(StatusCodes.Status200OK);

        return app;
    }
}
