namespace Tanaste.Ingestion.Models;

/// <summary>
/// Data written to (and read from) the Hub-level <c>tanaste.xml</c> sidecar.
/// The Hub-level sidecar lives at <c>{LibraryRoot}/{Category}/{HubName} ({Year})/tanaste.xml</c>.
/// It records the identity of the creative work so the library can be reconstructed
/// from the filesystem alone (Great Inhale).
/// </summary>
public sealed class HubSidecarData
{
    /// <summary>Human-readable Hub name — typically the work's title claim.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Publication year as a four-digit string, e.g. "1937". Null if unknown.</summary>
    public string? Year { get; init; }

    /// <summary>
    /// Wikidata Q-identifier for the creative work, e.g. "Q74287".
    /// Populated by the WikidataAdapter once enrichment completes.
    /// Null until then.
    /// </summary>
    public string? WikidataQid { get; init; }

    /// <summary>
    /// Franchise or series name, e.g. "Tolkien Legendarium".
    /// Null if the work is a standalone title.
    /// </summary>
    public string? Franchise { get; init; }

    /// <summary>UTC timestamp of the last organization pass that wrote this file.</summary>
    public DateTimeOffset LastOrganized { get; init; }
}
