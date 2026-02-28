using Microsoft.Data.Sqlite;
using Tanaste.Domain.Contracts;
using Tanaste.Domain.Entities;
using Tanaste.Storage.Contracts;

namespace Tanaste.Storage;

/// <summary>
/// ORM-less SQLite implementation of <see cref="ICanonicalValueRepository"/>.
///
/// Canonical values use a composite primary key (entity_id, key), so each
/// upsert replaces the previous winner for a given field.  The full scoring
/// history is preserved in <c>metadata_claims</c>; only the current winner
/// lives here.
///
/// Spec: Phase 4 – Canonical Integrity invariant;
///       Phase 9 – External Metadata Adapters § Canonical Persistence.
/// </summary>
public sealed class CanonicalValueRepository : ICanonicalValueRepository
{
    private readonly IDatabaseConnection _db;

    public CanonicalValueRepository(IDatabaseConnection db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // -------------------------------------------------------------------------
    // ICanonicalValueRepository
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public Task UpsertBatchAsync(
        IReadOnlyList<CanonicalValue> values,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (values.Count == 0)
            return Task.CompletedTask;

        var conn = _db.Open();

        // Single transaction: atomicity + significant write-performance gain.
        using var tx = conn.BeginTransaction();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            // INSERT OR REPLACE honours the (entity_id, key) PRIMARY KEY:
            // if a row already exists it is deleted then re-inserted with the
            // new value and timestamp.
            cmd.CommandText = """
                INSERT OR REPLACE INTO canonical_values
                    (entity_id, key, value, last_scored_at)
                VALUES
                    (@entity_id, @key, @value, @last_scored_at);
                """;

            var pEntityId      = cmd.Parameters.Add("@entity_id",      SqliteType.Text);
            var pKey           = cmd.Parameters.Add("@key",            SqliteType.Text);
            var pValue         = cmd.Parameters.Add("@value",          SqliteType.Text);
            var pLastScoredAt  = cmd.Parameters.Add("@last_scored_at", SqliteType.Text);

            foreach (var cv in values)
            {
                ct.ThrowIfCancellationRequested();

                pEntityId.Value     = cv.EntityId.ToString();
                pKey.Value          = cv.Key;
                pValue.Value        = cv.Value;
                pLastScoredAt.Value = cv.LastScoredAt.ToString("o"); // ISO-8601 round-trip

                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<CanonicalValue>> GetByEntityAsync(
        Guid entityId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT entity_id, key, value, last_scored_at
            FROM   canonical_values
            WHERE  entity_id = @entity_id
            ORDER  BY key ASC;
            """;
        cmd.Parameters.AddWithValue("@entity_id", entityId.ToString());

        var results = new List<CanonicalValue>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            results.Add(MapRow(reader));
        }

        return Task.FromResult<IReadOnlyList<CanonicalValue>>(results);
    }

    // -------------------------------------------------------------------------
    // Private row mapper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps the current reader row to a <see cref="CanonicalValue"/>.
    /// Column ordinals match the SELECT list used in every query in this class:
    ///   0=entity_id, 1=key, 2=value, 3=last_scored_at
    /// </summary>
    private static CanonicalValue MapRow(SqliteDataReader r) => new()
    {
        EntityId      = Guid.Parse(r.GetString(0)),
        Key           = r.GetString(1),
        Value         = r.GetString(2),
        LastScoredAt  = DateTimeOffset.Parse(r.GetString(3)),
    };
}
