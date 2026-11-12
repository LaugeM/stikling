using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// A kind of measurement the app doesn't have, like "Nodes" or "Aerial root", that someone added
/// themselves. A measurement points at it by id and keeps a copy of its name and whether it is a
/// length, so removing the kind leaves the measurements readable.
/// </summary>
public sealed class OwnMeasureKind : Entity
{
    // Must never change. The id of a new kind is made from its name with this, so that two devices
    // that both add "Nodes" make the same record. Changing it would make them two again.
    public static readonly Guid IdNamespace = new("a5202a7b-8512-455e-90e4-6eb1d3b4097f");

    public string Name { get; set; } = "";

    /// <summary>True for a length in cm, false for a count.</summary>
    public bool IsLength { get; set; }

    /// <summary>The id a new kind with this name gets. The same for "Nodes" and " nodes ".</summary>
    public static Guid IdFor(string name) => NameBasedId.For(IdNamespace, name.Trim().ToLowerInvariant());

    public OwnMeasureKind Copy() => (OwnMeasureKind)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(Strings.Measures.KindNeedsName);
        return errors;
    }
}
