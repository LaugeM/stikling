using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Care;

/// <summary>What a care entry is called when it is shown.</summary>
public static class CareLabels
{
    /// <summary>The live own kinds by id, for the lookup in <see cref="For"/>.</summary>
    public static IReadOnlyDictionary<Guid, OwnCareKind> Lookup(IEnumerable<OwnCareKind> kinds) =>
        kinds.Where(k => !k.IsDeleted).GroupBy(k => k.Id).ToDictionary(g => g.Key, g => g.First());

    /// <summary>
    /// The current name of the entry's own kind when it still exists, else the name copied onto the
    /// entry, else the built-in label. An entry with an own kind is never called Rotated, which is
    /// only what it is stored as.
    /// </summary>
    public static string For(CareLog entry, IReadOnlyDictionary<Guid, OwnCareKind>? ownKinds, Func<Enum, string> label)
    {
        if (entry.OwnKindId is { } id)
        {
            if (ownKinds is not null && ownKinds.TryGetValue(id, out var kind) && !kind.IsDeleted
                && !string.IsNullOrWhiteSpace(kind.Name))
                return kind.Name.Trim();
            if (!string.IsNullOrWhiteSpace(entry.OwnKindName))
                return entry.OwnKindName.Trim();
            if (entry.Kind == CareKind.Rotated)
                return Strings.Care.LabelCare;
        }
        return label(entry.Kind);
    }
}
