using Stikling.Core.Models;

namespace Stikling.Core.Backup;

/// <summary>
/// Everything the app stores, as it is written to data.json inside a backup ZIP.
/// The photo files sit next to it in the same ZIP, named after the photo ids.
/// </summary>
public sealed class BackupData
{
    /// <summary>Raised when the format changes in a way older versions can't read.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Written so a stray data.json can be recognised as Stikling's.</summary>
    public string App { get; set; } = "Stikling";

    public DateTimeOffset ExportedAt { get; set; }

    public List<Plant> Plants { get; set; } = [];
    public List<Propagation> Propagations { get; set; } = [];
    public List<TimelineEntry> Timeline { get; set; } = [];
    public List<Photo> Photos { get; set; } = [];
    public List<CareLog> CareLogs { get; set; } = [];
    public List<PestCase> PestCases { get; set; } = [];
    public List<PestTreatment> PestTreatments { get; set; } = [];
    public List<Pot> Pots { get; set; } = [];
    public List<SoilMix> SoilMixes { get; set; } = [];
    public List<Product> Products { get; set; } = [];

    /// <summary>
    /// What the file contains, for the line shown before restoring. Deleted items are in the
    /// file too, so a restore can carry a delete over, but they aren't counted as content.
    /// </summary>
    public BackupCounts Counts => new(
        Plants.Count(p => !p.IsDeleted),
        Propagations.Count(p => !p.IsDeleted),
        Timeline.Count(e => !e.IsDeleted),
        Photos.Count(p => !p.IsDeleted),
        CareLogs.Count(l => !l.IsDeleted),
        PestCases.Count(c => !c.IsDeleted),
        PestTreatments.Count(t => !t.IsDeleted),
        Pots.Count(p => !p.IsDeleted),
        SoilMixes.Count(m => !m.IsDeleted),
        Products.Count(p => !p.IsDeleted));
}

public sealed record BackupCounts(
    int Plants,
    int Propagations,
    int Entries,
    int Photos,
    int CareEntries,
    int PestCases,
    int Treatments,
    int Pots,
    int SoilMixes,
    int Products);
