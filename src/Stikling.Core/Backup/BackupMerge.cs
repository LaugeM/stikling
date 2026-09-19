using Stikling.Core.Models;

namespace Stikling.Core.Backup;

/// <summary>What a restore does to one kind of entity.</summary>
/// <param name="ToSave">The items to write: the ones missing here, newer in the backup, or deleted here since.</param>
/// <param name="BroughtBack">Items deleted on this device that the backup still had, so they return.</param>
public sealed record MergeResult<T>(IReadOnlyList<T> ToSave, int Added, int Updated, int BroughtBack, int Skipped)
    where T : Entity;

/// <summary>
/// Restoring merges rather than replaces: what's already on the device is kept unless the backup
/// holds a newer version of it. A backup can then be restored onto a device that has been used
/// since, without losing what was added in between.
///
/// Deleting is the exception. A delete here is not treated as the newer change, because the
/// point of restoring a backup is to get back what was deleted after it was made.
/// </summary>
public static class BackupMerge
{
    public static MergeResult<T> Merge<T>(IEnumerable<T> existing, IEnumerable<T> incoming) where T : Entity
    {
        var mine = existing.ToDictionary(e => e.Id);
        var save = new List<T>();
        int added = 0, updated = 0, broughtBack = 0, skipped = 0;

        // The same id can appear more than once in a hand-edited file; the newest one wins
        foreach (var item in incoming.GroupBy(i => i.Id).Select(g => g.MaxBy(i => i.UpdatedAt)!))
        {
            if (!mine.TryGetValue(item.Id, out var current))
                added++;
            else if (current.IsDeleted && !item.IsDeleted)
                // Deleted here after the backup was made. The backup still has it, so it comes back
                broughtBack++;
            else if (item.UpdatedAt > current.UpdatedAt)
                updated++;
            else
            {
                skipped++;
                continue;
            }

            save.Add(item);
        }

        return new MergeResult<T>(save, added, updated, broughtBack, skipped);
    }
}
