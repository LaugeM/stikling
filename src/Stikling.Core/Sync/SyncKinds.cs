namespace Stikling.Core.Sync;

/// <summary>
/// The kinds of record that sync, named after the stores they are kept in on the device, as
/// <see cref="RecordKinds"/> lists them. Everything belongs to a collection except
/// <see cref="Settings"/>, which belongs to the person.
/// </summary>
public static class SyncKinds
{
    public const string Settings = "settings";

    /// <summary>A photo's record. Its images travel separately, see <see cref="PhotoSyncService"/>.</summary>
    public const string Photos = "photos";

    public static readonly IReadOnlyList<string> Collection =
        [.. RecordKinds.All.Where(k => k.Owner == RecordOwner.Collection).Select(k => k.Name)];

    public static readonly IReadOnlyList<string> All =
        [.. Collection, .. RecordKinds.All.Where(k => k.Owner == RecordOwner.Person).Select(k => k.Name)];

    public static bool IsCollection(string kind) => Collection.Contains(kind, StringComparer.Ordinal);

    public static bool IsKnown(string kind) => All.Contains(kind, StringComparer.Ordinal);
}
