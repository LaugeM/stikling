namespace Stikling.Core.Sync;

/// <summary>
/// The kinds of record that sync, named after the stores they are kept in on the device.
/// Everything belongs to a collection except <see cref="Settings"/>, which belongs to the person.
/// </summary>
public static class SyncKinds
{
    public const string Settings = "settings";

    /// <summary>A photo's record. Its images travel separately, see <see cref="PhotoSyncService"/>.</summary>
    public const string Photos = "photos";

    public static readonly IReadOnlyList<string> Collection =
    [
        "plants",
        "propagations",
        "timeline",
        Photos,
        "careLogs",
        "pestCases",
        "pestTreatments",
        "pots",
        "soilMixes",
        "products",
        "feeds",
        "treatmentRecipes",
        "places",
        "putOffs",
    ];

    public static readonly IReadOnlyList<string> All = [.. Collection, Settings];

    public static bool IsCollection(string kind) => Collection.Contains(kind, StringComparer.Ordinal);

    public static bool IsKnown(string kind) => All.Contains(kind, StringComparer.Ordinal);
}
