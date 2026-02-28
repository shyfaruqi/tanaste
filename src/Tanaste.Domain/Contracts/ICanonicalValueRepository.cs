using Tanaste.Domain.Entities;

namespace Tanaste.Domain.Contracts;

/// <summary>
/// Persistence contract for <see cref="CanonicalValue"/> records.
///
/// Canonical values are the scoring engine's current best answer for each
/// metadata field of an entity. They are upserted on every re-score and
/// keyed by (EntityId, Key) — there is exactly one canonical value per
/// field per entity at any given time.
///
/// Implementations live in <c>Tanaste.Storage</c>.
/// </summary>
public interface ICanonicalValueRepository
{
    /// <summary>
    /// Upserts a batch of canonical values.
    /// For each value, if a row with the same (EntityId, Key) already exists
    /// it is replaced; otherwise a new row is inserted.
    /// </summary>
    /// <param name="values">The canonical values to upsert. May be empty; no-op if so.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertBatchAsync(
        IReadOnlyList<CanonicalValue> values,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all canonical values for a given entity, ordered by key ascending.
    /// </summary>
    /// <param name="entityId">The entity whose canonical values to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<CanonicalValue>> GetByEntityAsync(
        Guid entityId,
        CancellationToken ct = default);
}
