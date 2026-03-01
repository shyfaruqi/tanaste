using System.Text.Json.Serialization;
using Tanaste.Domain.Aggregates;
using Tanaste.Domain.Entities;
using Tanaste.Ingestion.Contracts;

namespace Tanaste.Api.Models;

// ── GET /system/status ─────────────────────────────────────────────────────────

public sealed class SystemStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}

// ── /admin/api-keys ────────────────────────────────────────────────────────────

public sealed class ApiKeyDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    public static ApiKeyDto FromDomain(ApiKey key) => new()
    {
        Id        = key.Id,
        Label     = key.Label,
        CreatedAt = key.CreatedAt,
    };
}

public sealed class CreateApiKeyRequest
{
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;
}

public sealed class CreateApiKeyResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// The API key plaintext. Shown exactly once — store it now; it cannot be retrieved again.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}

// ── /admin/provider-configs ────────────────────────────────────────────────────

public sealed class ProviderConfigDto
{
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; init; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    /// <summary>Secret values are returned as '********'; non-secret values are returned as-is.</summary>
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("is_secret")]
    public bool IsSecret { get; init; }

    public static ProviderConfigDto FromDomain(ProviderConfiguration cfg) => new()
    {
        ProviderId = cfg.ProviderId,
        Key        = cfg.Key,
        Value      = cfg.Value,
        IsSecret   = cfg.IsSecret,
    };
}

public sealed class UpsertProviderConfigRequest
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("is_secret")]
    public bool IsSecret { get; init; }
}

// ── GET /hubs/search ───────────────────────────────────────────────────────────

/// <summary>
/// A single work result from the hub search endpoint.
/// Carries enough information to render a command-palette result row:
/// the work's own title, the Hub it belongs to, and its media type for icon selection.
/// </summary>
public sealed class SearchResultDto
{
    [JsonPropertyName("work_id")]
    public Guid WorkId { get; init; }

    [JsonPropertyName("hub_id")]
    public Guid HubId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; init; } = string.Empty;

    [JsonPropertyName("hub_display_name")]
    public string HubDisplayName { get; init; } = string.Empty;
}

// ── GET /hubs ──────────────────────────────────────────────────────────────────

public sealed class HubDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("universe_id")]
    public Guid? UniverseId { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("works")]
    public List<WorkDto> Works { get; init; } = [];

    public static HubDto FromDomain(Hub hub) => new()
    {
        Id         = hub.Id,
        UniverseId = hub.UniverseId,
        CreatedAt  = hub.CreatedAt,
        Works      = hub.Works.Select(WorkDto.FromDomain).ToList(),
    };
}

public sealed class WorkDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("hub_id")]
    public Guid HubId { get; init; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; init; } = string.Empty;

    [JsonPropertyName("sequence_index")]
    public int? SequenceIndex { get; init; }

    [JsonPropertyName("canonical_values")]
    public List<CanonicalValueDto> CanonicalValues { get; init; } = [];

    public static WorkDto FromDomain(Work work) => new()
    {
        Id              = work.Id,
        HubId           = work.HubId,
        MediaType       = work.MediaType.ToString(),
        SequenceIndex   = work.SequenceIndex,
        CanonicalValues = work.CanonicalValues.Select(CanonicalValueDto.FromDomain).ToList(),
    };
}

public sealed class CanonicalValueDto
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("last_scored_at")]
    public DateTimeOffset LastScoredAt { get; init; }

    public static CanonicalValueDto FromDomain(CanonicalValue cv) => new()
    {
        Key          = cv.Key,
        Value        = cv.Value,
        LastScoredAt = cv.LastScoredAt,
    };
}

// ── POST /ingestion/scan ───────────────────────────────────────────────────────

public sealed class ScanRequest
{
    /// <summary>
    /// Optional root path to scan. When absent, the engine uses the configured
    /// WatchDirectory from IngestionOptions.
    /// </summary>
    [JsonPropertyName("root_path")]
    public string? RootPath { get; init; }
}

public sealed class ScanResponse
{
    [JsonPropertyName("operations")]
    public List<PendingOperationDto> Operations { get; init; } = [];

    [JsonPropertyName("total_count")]
    public int TotalCount => Operations.Count;
}

public sealed class PendingOperationDto
{
    [JsonPropertyName("source_path")]
    public string SourcePath { get; init; } = string.Empty;

    [JsonPropertyName("destination_path")]
    public string DestinationPath { get; init; } = string.Empty;

    [JsonPropertyName("operation_kind")]
    public string OperationKind { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    public static PendingOperationDto FromDomain(PendingOperation op) => new()
    {
        SourcePath      = op.SourcePath,
        DestinationPath = op.DestinationPath,
        OperationKind   = op.OperationKind,
        Reason          = op.Reason,
    };
}

// ── POST /ingestion/library-scan ───────────────────────────────────────────────

public sealed class LibraryScanResponse
{
    /// <summary>Number of Hub records created or updated in the database.</summary>
    [JsonPropertyName("hubs_upserted")]
    public int HubsUpserted { get; init; }

    /// <summary>Number of Edition/MediaAsset canonical value sets upserted.</summary>
    [JsonPropertyName("editions_upserted")]
    public int EditionsUpserted { get; init; }

    /// <summary>Number of sidecar files that could not be parsed or hydrated.</summary>
    [JsonPropertyName("errors")]
    public int Errors { get; init; }

    /// <summary>Wall-clock time taken for the full scan, in milliseconds.</summary>
    [JsonPropertyName("elapsed_ms")]
    public long ElapsedMs { get; init; }
}

// ── PATCH /metadata/resolve ────────────────────────────────────────────────────

public sealed class ResolveRequest
{
    /// <summary>The Work or Edition entity whose canonical value is being overridden.</summary>
    [JsonPropertyName("entity_id")]
    public Guid EntityId { get; init; }

    /// <summary>The metadata field key, e.g. "title", "release_year".</summary>
    [JsonPropertyName("claim_key")]
    public string ClaimKey { get; init; } = string.Empty;

    /// <summary>The human-chosen winning value to persist.</summary>
    [JsonPropertyName("chosen_value")]
    public string ChosenValue { get; init; } = string.Empty;
}

public sealed class ResolveResponse
{
    [JsonPropertyName("entity_id")]
    public Guid EntityId { get; init; }

    [JsonPropertyName("claim_key")]
    public string ClaimKey { get; init; } = string.Empty;

    [JsonPropertyName("chosen_value")]
    public string ChosenValue { get; init; } = string.Empty;

    [JsonPropertyName("resolved_at")]
    public DateTimeOffset ResolvedAt { get; init; }
}

// ── GET /settings/folders ──────────────────────────────────────────────────────

public sealed class FolderSettingsResponse
{
    [JsonPropertyName("watch_directory")]
    public string WatchDirectory { get; init; } = string.Empty;

    [JsonPropertyName("library_root")]
    public string LibraryRoot { get; init; } = string.Empty;
}

public sealed class UpdateFoldersRequest
{
    [JsonPropertyName("watch_directory")]
    public string? WatchDirectory { get; init; }

    [JsonPropertyName("library_root")]
    public string? LibraryRoot { get; init; }
}

// ── POST /settings/test-path ───────────────────────────────────────────────────

public sealed class TestPathRequest
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;
}

public sealed class TestPathResponse
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("exists")]
    public bool Exists { get; init; }

    [JsonPropertyName("has_read")]
    public bool HasRead { get; init; }

    [JsonPropertyName("has_write")]
    public bool HasWrite { get; init; }
}

// ── GET /settings/providers ────────────────────────────────────────────────────

public sealed class ProviderStatusResponse
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("is_zero_key")]
    public bool IsZeroKey { get; init; }

    [JsonPropertyName("is_reachable")]
    public bool IsReachable { get; init; }

    [JsonPropertyName("domain")]
    public string Domain { get; init; } = string.Empty;

    [JsonPropertyName("capability_tags")]
    public List<string> CapabilityTags { get; init; } = [];

    [JsonPropertyName("default_weight")]
    public double DefaultWeight { get; init; }

    [JsonPropertyName("field_weights")]
    public Dictionary<string, double> FieldWeights { get; init; } = [];
}
