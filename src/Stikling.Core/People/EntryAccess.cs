using Stikling.Core.Models;

namespace Stikling.Core.People;

/// <summary>
/// What a person may add to or change among the entries of a plant or propagation, by kind. The screens use
/// it to decide which controls to draw, and it follows what a helper may send (<see cref="HelperSync"/>):
/// care entries, photos and notes, and only what they added themselves.
/// </summary>
public static class EntryAccess
{
    /// <summary>Whether this person may add a timeline entry of this kind. A helper adds notes, and photos, which are not timeline entries of their own.</summary>
    public static bool CanAdd(this MemberAccess access, TimelineKind kind) =>
        access.CanEdit || (access.CanLog && kind == TimelineKind.Note);

    /// <summary>
    /// Whether this person may correct or remove a timeline entry. An editor changes any. A helper changes
    /// their own notes only: the other kinds are written by the app when something about the plant changes.
    /// </summary>
    public static bool CanChangeEntry(this MemberAccess access, TimelineEntry entry) =>
        access.CanEdit || (entry.Kind == TimelineKind.Note && access.CanChangeEntry(entry.CreatedBy));

    /// <summary>Whether this person may correct or remove a care entry they can see.</summary>
    public static bool CanChangeEntry(this MemberAccess access, CareLog entry) => access.CanChangeEntry(entry.CreatedBy);

    /// <summary>Whether this person may correct or remove a propagation's care entry.</summary>
    public static bool CanChangeEntry(this MemberAccess access, PropagationCareLog entry) => access.CanChangeEntry(entry.CreatedBy);

    /// <summary>Whether this person may remove or edit a photo.</summary>
    public static bool CanChangeEntry(this MemberAccess access, Photo photo) => access.CanChangeEntry(photo.CreatedBy);
}
