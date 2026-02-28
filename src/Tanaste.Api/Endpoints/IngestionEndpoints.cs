using Microsoft.Extensions.Options;
using Tanaste.Api.Models;
using Tanaste.Ingestion.Contracts;
using Tanaste.Ingestion.Models;

namespace Tanaste.Api.Endpoints;

public static class IngestionEndpoints
{
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/ingestion")
                       .WithTags("Ingestion");

        group.MapPost("/scan", async (
            ScanRequest? request,
            IIngestionEngine engine,
            IOptions<IngestionOptions> opts,
            CancellationToken ct) =>
        {
            var rootPath = request?.RootPath
                ?? opts.Value.WatchDirectory;

            if (string.IsNullOrWhiteSpace(rootPath))
                return Results.BadRequest(
                    "No root_path provided and Ingestion:WatchDirectory is not configured.");

            if (!Directory.Exists(rootPath))
                return Results.BadRequest($"Directory does not exist: {rootPath}");

            var operations = await engine.DryRunAsync(rootPath, ct);
            var response = new ScanResponse
            {
                Operations = operations
                    .Select(PendingOperationDto.FromDomain)
                    .ToList(),
            };

            return Results.Ok(response);
        })
        .WithName("TriggerScan")
        .WithSummary("Simulate a library scan and return pending operations without mutating files.")
        .Produces<ScanResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // ── POST /ingestion/library-scan ──────────────────────────────────────────

        group.MapPost("/library-scan", async (
            ILibraryScanner            scanner,
            IOptions<IngestionOptions> opts,
            CancellationToken          ct) =>
        {
            var root = opts.Value.LibraryRoot;

            if (string.IsNullOrWhiteSpace(root))
                return Results.BadRequest(
                    "LibraryRoot is not configured. Set Ingestion:LibraryRoot in appsettings.json.");

            if (!Directory.Exists(root))
                return Results.BadRequest($"Library root does not exist: {root}");

            var result = await scanner.ScanAsync(root, ct);

            return Results.Ok(new LibraryScanResponse
            {
                HubsUpserted     = result.HubsUpserted,
                EditionsUpserted = result.EditionsUpserted,
                Errors           = result.Errors,
                ElapsedMs        = (long)result.Elapsed.TotalMilliseconds,
            });
        })
        .WithName("TriggerLibraryScan")
        .WithSummary(
            "Reads tanaste.xml sidecars in the Library Root and hydrates the database. " +
            "XML always wins on conflict (Great Inhale).")
        .Produces<LibraryScanResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        return app;
    }
}
