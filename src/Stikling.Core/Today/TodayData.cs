using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Today;

/// <summary>
/// Everything the to-do sections on Today are worked out from, read once. <see cref="TodaySections"/>
/// turns it into the sections for any day.
/// </summary>
public sealed record TodayData
{
    public IReadOnlyList<Plant> Plants { get; init; } = [];
    public IReadOnlyList<Propagation> Propagations { get; init; } = [];
    public UserSettings Settings { get; init; } = new();
    public PutOffs PutOffs { get; init; } = PutOffs.Empty();
    public IReadOnlyDictionary<Guid, TimelineEntry> Latest { get; init; } = new Dictionary<Guid, TimelineEntry>();
    public IReadOnlyDictionary<Guid, DateOnly> PropagationCareDays { get; init; } = new Dictionary<Guid, DateOnly>();
    public IReadOnlyList<Problem> Problems { get; init; } = [];
    public IReadOnlyList<ProblemUpdate> ProblemUpdates { get; init; } = [];
    public Places Places { get; init; } = Places.None;
    public IReadOnlyList<CareInterval> CareIntervals { get; init; } = [];
    public IReadOnlyList<CareLog> CareLogs { get; init; } = [];
    public IReadOnlyList<OwnCareKind> OwnCareKinds { get; init; } = [];
    public IReadOnlyList<Feed> Feeds { get; init; } = [];
    public IReadOnlyDictionary<Guid, Photo> NewestPhotos { get; init; } = new Dictionary<Guid, Photo>();
    public IReadOnlyList<AwayStretch> Stretches { get; init; } = [];
    public IReadOnlyDictionary<Guid, DateTimeOffset> LastActivity { get; init; } = new Dictionary<Guid, DateTimeOffset>();
    public IReadOnlyList<TimelineEntry> ComingBack { get; init; } = [];
    public IReadOnlyList<TimelineEntry> PlantChanges { get; init; } = [];
    public IReadOnlyList<GrowLight> Lights { get; init; } = [];
    public IReadOnlyDictionary<Guid, Pot> PotsById { get; init; } = new Dictionary<Guid, Pot>();
    public IReadOnlyList<PestCase> PestCases { get; init; } = [];
    public IReadOnlyList<PestTreatment> PestTreatments { get; init; } = [];

    /// <summary>The timeline the list on the day back reads, for each day back that is within reach.</summary>
    public IReadOnlyDictionary<DateOnly, IReadOnlyList<TimelineEntry>> BackTimelines { get; init; } =
        new Dictionary<DateOnly, IReadOnlyList<TimelineEntry>>();
}
