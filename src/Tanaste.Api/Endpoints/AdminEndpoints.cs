using Tanaste.Api.Models;
using Tanaste.Api.Services;
using Tanaste.Domain.Contracts;

namespace Tanaste.Api.Endpoints;

/// <summary>
/// Administration endpoints for API key management and provider configuration.
/// All routes are grouped under /admin.
///
/// API key endpoints:
///   GET    /admin/api-keys           — list all keys (id, label, created_at only)
///   POST   /admin/api-keys           — generate a new key (plaintext shown ONCE)
///   DELETE /admin/api-keys/{id}      — revoke a single key
///   DELETE /admin/api-keys           — revoke ALL keys
///
/// Provider configuration endpoints:
///   GET    /admin/provider-configs/{providerId}             — list configs (secrets masked)
///   PUT    /admin/provider-configs/{providerId}/{configKey} — set a config value
///   DELETE /admin/provider-configs/{providerId}/{configKey} — remove a config entry
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin").WithTags("Admin");

        // ── API Key Management ─────────────────────────────────────────────────

        group.MapGet("/api-keys", async (
            IApiKeyRepository repo,
            CancellationToken ct) =>
        {
            var keys = await repo.GetAllAsync(ct);
            var dtos = keys.Select(ApiKeyDto.FromDomain).ToList();
            return Results.Ok(dtos);
        })
        .WithName("ListApiKeys")
        .WithSummary("List all issued API keys. Key values are never included.")
        .Produces<List<ApiKeyDto>>(StatusCodes.Status200OK);

        group.MapPost("/api-keys", async (
            CreateApiKeyRequest request,
            ApiKeyService svc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Label))
                return Results.BadRequest("label must not be empty.");

            var (key, plaintext) = await svc.GenerateAsync(request.Label, ct);

            // The plaintext is returned exactly once in this response.
            // SECURITY: do not log, cache, or re-send the 'key' field.
            return Results.Ok(new CreateApiKeyResponse
            {
                Id        = key.Id,
                Label     = key.Label,
                Key       = plaintext,
                CreatedAt = key.CreatedAt,
            });
        })
        .WithName("CreateApiKey")
        .WithSummary("Generate a new API key. The key value is shown only in this response.")
        .Produces<CreateApiKeyResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapDelete("/api-keys/{id:guid}", async (
            Guid id,
            IApiKeyRepository repo,
            CancellationToken ct) =>
        {
            var deleted = await repo.DeleteAsync(id, ct);
            return deleted
                ? Results.NoContent()
                : Results.NotFound($"API key '{id}' not found.");
        })
        .WithName("RevokeApiKey")
        .WithSummary("Revoke an API key. Existing sessions using this key will immediately receive 401.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // Revoke ALL keys — no route parameter distinguishes this from single revoke.
        group.MapDelete("/api-keys", async (
            IApiKeyRepository repo,
            CancellationToken ct) =>
        {
            var count = await repo.DeleteAllAsync(ct);
            return Results.Ok(new RevokeAllKeysResponse { RevokedCount = count });
        })
        .WithName("RevokeAllApiKeys")
        .WithSummary("Revoke ALL issued API keys. Returns the count of revoked keys.")
        .Produces<RevokeAllKeysResponse>(StatusCodes.Status200OK);

        // ── Provider Configuration ─────────────────────────────────────────────

        group.MapGet("/provider-configs/{providerId}", async (
            string providerId,
            IProviderConfigurationRepository configRepo,
            CancellationToken ct) =>
        {
            var configs = await configRepo.GetAllMaskedAsync(providerId, ct);
            var dtos    = configs.Select(ProviderConfigDto.FromDomain).ToList();
            return Results.Ok(dtos);
        })
        .WithName("ListProviderConfigs")
        .WithSummary("List configuration entries for a provider. Secret values are masked as '********'.")
        .Produces<List<ProviderConfigDto>>(StatusCodes.Status200OK);

        group.MapMethods("/provider-configs/{providerId}/{configKey}", ["PUT"], async (
            string providerId,
            string configKey,
            UpsertProviderConfigRequest request,
            IProviderConfigurationRepository configRepo,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Value))
                return Results.BadRequest("value must not be empty.");

            // Guard: reject the masked sentinel — the caller must provide the real value.
            if (request.Value == "********")
                return Results.BadRequest(
                    "Cannot store '********'. Provide the actual plaintext value.");

            await configRepo.UpsertAsync(
                providerId, configKey, request.Value, request.IsSecret, ct);

            return Results.NoContent();
        })
        .WithName("UpsertProviderConfig")
        .WithSummary("Set a provider configuration value. Secret values are encrypted before storage.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapDelete("/provider-configs/{providerId}/{configKey}", async (
            string providerId,
            string configKey,
            IProviderConfigurationRepository configRepo,
            CancellationToken ct) =>
        {
            await configRepo.DeleteAsync(providerId, configKey, ct);
            return Results.NoContent();
        })
        .WithName("DeleteProviderConfig")
        .WithSummary("Remove a provider configuration entry.")
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
