using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>Finds the propagation to offer "Start like last time" from, and tells when it's already what's filled in.</summary>
public static class LastTime
{
    /// <summary>
    /// The newest propagation from the same parent, else the newest of the same genus. Deleted ones
    /// are skipped. Null when there's neither a match nor anything to match on.
    /// </summary>
    public static Propagation? Find(IEnumerable<Propagation> propagations, Guid? parentId, string? genus)
    {
        var live = propagations.Where(p => !p.IsDeleted).ToList();
        if (parentId is { } parent && Newest(live.Where(p => p.ParentPlantId == parent)) is { } fromParent)
            return fromParent;

        var name = genus?.Trim();
        return string.IsNullOrEmpty(name)
            ? null
            : Newest(live.Where(p => string.Equals(p.Genus?.Trim(), name, StringComparison.OrdinalIgnoreCase)));
    }

    private static Propagation? Newest(IEnumerable<Propagation> propagations) =>
        propagations.OrderByDescending(p => p.StartedOn).ThenByDescending(p => p.CreatedAt).FirstOrDefault();

    /// <summary>True when the two have the same setup: type, medium, container, rooting aids and drying.</summary>
    public static bool SameSetup(Propagation a, Propagation b) =>
        a.Type == b.Type
        && a.Medium == b.Medium
        && string.Equals(a.Container?.Trim() ?? "", b.Container?.Trim() ?? "", StringComparison.Ordinal)
        && a.RootingAids.Order().SequenceEqual(b.RootingAids.Order())
        && a.DriedForHours == b.DriedForHours;

    /// <summary>Copies the setup of <paramref name="from"/> onto <paramref name="to"/>. Nothing else changes.</summary>
    public static void Apply(Propagation from, Propagation to)
    {
        to.Type = from.Type;
        to.Medium = from.Medium;
        to.Container = from.Container;
        to.ContainerPotId = from.ContainerPotId;
        to.RootingAids = [.. from.RootingAids];
        to.DriedForHours = from.DriedForHours;
        to.ClearMixUnlessSoil();
    }
}
