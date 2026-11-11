using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Core.Backup;

/// <summary>What restoring one kind of record did, added up across kinds for the line shown afterwards.</summary>
public readonly record struct MergeCounts(int Added, int Updated, int BroughtBack, int Skipped)
{
    public static MergeCounts operator +(MergeCounts a, MergeCounts b) =>
        new(a.Added + b.Added, a.Updated + b.Updated, a.BroughtBack + b.BroughtBack, a.Skipped + b.Skipped);
}

/// <summary>
/// One kind of record paired with its list in <see cref="BackupData"/>, so saving and restoring a
/// backup can go through every kind without naming each one.
/// </summary>
public abstract class BackupList
{
    public abstract RecordKind Kind { get; }

    /// <summary>Reads every record of the kind on the device, deleted ones too, into the backup.</summary>
    public abstract Task ExportAsync(BackupData data, IRecordStore store);

    /// <summary>Merges the backup's records of the kind into the device (see <see cref="BackupMerge"/>).</summary>
    public abstract Task<MergeCounts> RestoreAsync(BackupData data, IRecordStore store);
}

public sealed class BackupList<T>(RecordKind<T> kind, Func<BackupData, List<T>> get, Action<BackupData, List<T>> set)
    : BackupList where T : Entity
{
    public override RecordKind<T> Kind => kind;

    /// <summary>The backup's records of this kind.</summary>
    public List<T> In(BackupData data) => get(data);

    public override async Task ExportAsync(BackupData data, IRecordStore store) =>
        set(data, await store.GetAllAsync(kind));

    public override async Task<MergeCounts> RestoreAsync(BackupData data, IRecordStore store) =>
        (await MergeAsync(data, store)).Counts;

    /// <summary>Like <see cref="RestoreAsync"/>, with what was written, for a kind that has more to do afterwards.</summary>
    public async Task<MergeResult<T>> MergeAsync(BackupData data, IRecordStore store)
    {
        var result = BackupMerge.Merge(await store.GetAllAsync(kind), get(data));
        foreach (var item in result.ToSave)
            await store.PutAsync(kind, item);
        return result;
    }
}
