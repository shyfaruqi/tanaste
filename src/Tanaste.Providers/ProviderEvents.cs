namespace Tanaste.Providers;

/// <summary>
/// Broadcast when an external metadata provider successfully updates at least
/// one canonical field for an entity.
///
/// SignalR method name: <c>"MetadataHarvested"</c>
///
/// The Blazor Dashboard handles this event to invalidate its cached universe
/// state and trigger a card re-render (cover-art pop-in effect).
/// </summary>
/// <param name="EntityId">The entity whose metadata was updated.</param>
/// <param name="ProviderName">The adapter that produced the new claims (e.g. <c>"apple_books_ebook"</c>).</param>
/// <param name="UpdatedFields">The claim keys that were written (e.g. <c>["cover", "description"]</c>).</param>
public sealed record MetadataHarvestedEvent(
    Guid EntityId,
    string ProviderName,
    IReadOnlyList<string> UpdatedFields);

/// <summary>
/// Broadcast when the Wikidata adapter successfully enriches a person entity
/// with a headshot URL and/or biography.
///
/// SignalR method name: <c>"PersonEnriched"</c>
///
/// The Blazor Dashboard handles this event to update author/narrator cards
/// with the newly acquired headshot and Wikidata identifier.
/// </summary>
/// <param name="PersonId">The person entity that was enriched.</param>
/// <param name="Name">The person's display name.</param>
/// <param name="HeadshotUrl">Wikimedia Commons image URL, or <c>null</c> if not found.</param>
/// <param name="WikidataQid">The Wikidata Q-identifier (e.g. <c>"Q42"</c>), or <c>null</c>.</param>
public sealed record PersonEnrichedEvent(
    Guid PersonId,
    string Name,
    string? HeadshotUrl,
    string? WikidataQid);
