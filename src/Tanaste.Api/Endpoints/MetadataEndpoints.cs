using Tanaste.Api.Hubs;
using Tanaste.Api.Models;
using Tanaste.Api.Security;
using Tanaste.Domain.Contracts;
using Tanaste.Domain.Entities;
using Tanaste.Storage.Contracts;

namespace Tanaste.Api.Endpoints;

public static class MetadataEndpoints
{
    /// <summary>Well-known provider GUID for user-manual metadata corrections.</summary>
    private static readonly Guid UserManualProviderId =
        new("d0000000-0000-4000-8000-000000000001");

    public static IEndpointRouteBuilder MapMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/metadata")
                       .WithTags("Metadata");

        // ── GET /metadata/claims/{entityId} ──────────────────────────────────
        group.MapGet("/claims/{entityId:guid}", async (
            Guid entityId,
            IMetadataClaimRepository claimRepo,
            CancellationToken ct) =>
        {
            var claims = await claimRepo.GetByEntityAsync(entityId, ct);
            var dtos = claims.Select(ClaimDto.FromDomain).ToList();
            return Results.Ok(dtos);
        })
        .WithName("GetClaimHistory")
        .WithSummary("Returns all metadata claims for a Work or Edition, ordered by claimed_at.")
        .Produces<List<ClaimDto>>(StatusCodes.Status200OK)
        .RequireAnyRole();

        // ── GET /metadata/conflicts ─────────────────────────────────────────
        group.MapGet("/conflicts", async (
            ICanonicalValueRepository canonicalRepo,
            CancellationToken ct) =>
        {
            var conflicted = await canonicalRepo.GetConflictedAsync(ct);
            var dtos = conflicted.Select(ConflictDto.FromDomain).ToList();
            return Results.Ok(dtos);
        })
        .WithName("GetConflicts")
        .WithSummary("Returns all canonical values with unresolved metadata conflicts.")
        .Produces<List<ConflictDto>>(StatusCodes.Status200OK)
        .RequireAdminOrCurator();

        // ── PATCH /metadata/lock-claim ───────────────────────────────────────
        group.MapMethods("/lock-claim", ["PATCH"], async (
            LockClaimRequest request,
            IMetadataClaimRepository claimRepo,
            IDatabaseConnection db,
            ITransactionJournal journal,
            IEventPublisher publisher,
            CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.ClaimKey))
                return Results.BadRequest("claim_key must not be empty.");
            if (string.IsNullOrWhiteSpace(request.ChosenValue))
                return Results.BadRequest("chosen_value must not be empty.");

            var lockedAt = DateTimeOffset.UtcNow;

            // 1. Insert a user-locked claim (confidence 1.0).
            var claim = new MetadataClaim
            {
                Id           = Guid.NewGuid(),
                EntityId     = request.EntityId,
                ProviderId   = UserManualProviderId,
                ClaimKey     = request.ClaimKey,
                ClaimValue   = request.ChosenValue,
                Confidence   = 1.0,
                ClaimedAt    = lockedAt,
                IsUserLocked = true,
            };
            await claimRepo.InsertBatchAsync([claim], ct);

            // 2. Upsert the canonical value so the Dashboard sees the change immediately.
            //    User-locked claims resolve any conflict, so is_conflicted is set to 0.
            var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO canonical_values (entity_id, key, value, last_scored_at, is_conflicted)
                VALUES (@entity_id, @key, @value, @last_scored_at, 0)
                ON CONFLICT(entity_id, key) DO UPDATE SET
                    value          = excluded.value,
                    last_scored_at = excluded.last_scored_at,
                    is_conflicted  = 0;
                """;
            cmd.Parameters.AddWithValue("@entity_id",      request.EntityId.ToString());
            cmd.Parameters.AddWithValue("@key",            request.ClaimKey);
            cmd.Parameters.AddWithValue("@value",          request.ChosenValue);
            cmd.Parameters.AddWithValue("@last_scored_at", lockedAt.ToString("O"));
            cmd.ExecuteNonQuery();

            // 3. Audit trail.
            journal.Log("CLAIM_USER_LOCKED", "MetadataClaim", request.EntityId.ToString());

            // 4. Broadcast so the Dashboard refreshes.
            await publisher.PublishAsync("MetadataHarvested", new
            {
                entity_id     = request.EntityId,
                provider_name = "user_manual",
                updated_fields = new[] { request.ClaimKey },
            });

            return Results.Ok(new LockClaimResponse
            {
                EntityId    = request.EntityId,
                ClaimKey    = request.ClaimKey,
                ChosenValue = request.ChosenValue,
                LockedAt    = lockedAt,
            });
        })
        .WithName("LockClaim")
        .WithSummary("Create a user-locked metadata claim and update the canonical value. Used by the Curator's Drawer.")
        .Produces<LockClaimResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAdminOrCurator();

        // ── PATCH /metadata/resolve (legacy) ─────────────────────────────────
        group.MapMethods("/resolve", ["PATCH"], async (
            ResolveRequest request,
            IDatabaseConnection db,
            ITransactionJournal journal,
            CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.ClaimKey))
                return Results.BadRequest("claim_key must not be empty.");

            if (string.IsNullOrWhiteSpace(request.ChosenValue))
                return Results.BadRequest("chosen_value must not be empty.");

            var resolvedAt = DateTimeOffset.UtcNow;

            var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO canonical_values (entity_id, key, value, last_scored_at, is_conflicted)
                VALUES (@entity_id, @key, @value, @last_scored_at, 0)
                ON CONFLICT(entity_id, key) DO UPDATE SET
                    value          = excluded.value,
                    last_scored_at = excluded.last_scored_at,
                    is_conflicted  = 0;
                """;
            cmd.Parameters.AddWithValue("@entity_id",      request.EntityId.ToString());
            cmd.Parameters.AddWithValue("@key",            request.ClaimKey);
            cmd.Parameters.AddWithValue("@value",          request.ChosenValue);
            cmd.Parameters.AddWithValue("@last_scored_at", resolvedAt.ToString("O"));
            cmd.ExecuteNonQuery();

            journal.Log(
                "CANONICAL_VALUE_MANUAL_RESOLVE",
                "CanonicalValue",
                request.EntityId.ToString());

            return Results.Ok(new ResolveResponse
            {
                EntityId    = request.EntityId,
                ClaimKey    = request.ClaimKey,
                ChosenValue = request.ChosenValue,
                ResolvedAt  = resolvedAt,
            });
        })
        .WithName("ResolveMetadataConflict")
        .WithSummary("Manually override a metadata canonical value, locking in the chosen value.")
        .Produces<ResolveResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAdminOrCurator();

        return app;
    }
}
