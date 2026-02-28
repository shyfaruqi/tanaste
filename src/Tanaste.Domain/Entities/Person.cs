namespace Tanaste.Domain.Entities;

/// <summary>
/// An author, narrator, or other creative person linked to one or more media assets.
///
/// Persons are created when the ingestion engine extracts author/narrator names from
/// file metadata, and enriched asynchronously via the Wikidata adapter (Phase 9).
///
/// Maps 1:1 to a row in the <c>persons</c> table.
/// Spec: Phase 9 – Recursive Person Enrichment.
/// </summary>
public sealed class Person
{
    /// <summary>Stable row identifier (UUID → TEXT in SQLite).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The person's display name as extracted from file metadata.
    /// Example: <c>"Frank Herbert"</c>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The role this person plays in associated media assets.
    /// Valid values: <c>"Author"</c>, <c>"Narrator"</c>, <c>"Director"</c>.
    /// Enforced by a CHECK constraint in the <c>persons</c> table.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// The Wikidata Q-identifier for this person, if enriched.
    /// Example: <c>"Q42"</c> (Douglas Adams).
    /// Null until the Wikidata adapter has processed this person.
    /// </summary>
    public string? WikidataQid { get; set; }

    /// <summary>
    /// A URL to a headshot / portrait image for this person.
    /// Sourced from Wikimedia Commons via Wikidata P18 (image) claim.
    /// Null until enriched.
    /// </summary>
    public string? HeadshotUrl { get; set; }

    /// <summary>
    /// A short biography extracted from the Wikidata entity description.
    /// Null until enriched.
    /// </summary>
    public string? Biography { get; set; }

    /// <summary>
    /// When this person record was first created.
    /// Defaults to <see cref="DateTimeOffset.UtcNow"/> at construction time.
    /// Maps to <c>persons.created_at</c> (ISO-8601 TEXT in SQLite).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this person was last enriched from an external provider.
    /// <c>null</c> means the person has not yet been enriched.
    /// The <see cref="RecursiveIdentityService"/> uses this to decide whether
    /// to enqueue a Wikidata harvest request.
    /// Maps to <c>persons.enriched_at</c> (ISO-8601 TEXT in SQLite).
    /// </summary>
    public DateTimeOffset? EnrichedAt { get; set; }
}
