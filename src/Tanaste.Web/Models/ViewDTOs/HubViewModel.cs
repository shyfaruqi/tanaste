namespace Tanaste.Web.Models.ViewDTOs;

/// <summary>
/// UI representation of a Hub (media collection).
/// Maps from the API's HubDto; adds display-friendly helper properties.
/// </summary>
public sealed class HubViewModel
{
    public Guid                Id         { get; init; }
    public Guid?               UniverseId { get; init; }
    public DateTimeOffset      CreatedAt  { get; init; }
    public List<WorkViewModel> Works      { get; init; } = [];

    // ── Display helpers ───────────────────────────────────────────────────────

    /// <summary>Best title across all works, or a short ID fallback.</summary>
    public string DisplayName =>
        Works.Select(GetTitle).FirstOrDefault(t => !string.IsNullOrEmpty(t))
        ?? $"Hub {Id:N}"[..12];

    public int    WorkCount  => Works.Count;
    public string MediaTypes => string.Join(", ", Works.Select(w => w.MediaType).Distinct());

    public bool   HasWorks   => Works.Count > 0;

    // ── Factory ───────────────────────────────────────────────────────────────

    public static HubViewModel FromApiDto(
        Guid id, Guid? universeId, DateTimeOffset createdAt, IEnumerable<WorkViewModel> works)
        => new()
        {
            Id         = id,
            UniverseId = universeId,
            CreatedAt  = createdAt,
            Works      = works.ToList(),
        };

    private static string? GetTitle(WorkViewModel w) =>
        w.CanonicalValues.FirstOrDefault(cv => cv.Key == "title")?.Value;
}
