namespace Stikling.Core.Models;

/// <summary>
/// A kind of care the app doesn't have, like "Misted" or "Bottom watered", that someone added
/// themselves. It says what it counts as, so a bottom watering reaches the watering reminder, and
/// whether its entries get a line on the plant's history. A care entry points at it by id and keeps
/// a copy of its name, so removing the kind leaves the entries readable.
/// </summary>
public sealed class OwnCareKind : Entity
{
    // Must never change. The id of a new kind is made from its name with this, so that two devices
    // that both add "Misted" make the same record. Changing it would make them two again.
    public static readonly Guid IdNamespace = new("b1e4a7d2-5c39-4f86-a0d1-7e92c3f58b64");

    public string Name { get; set; } = "";

    /// <summary>Only <see cref="CareKind.Watered"/> or <see cref="CareKind.Fertilised"/>, or null for neither.</summary>
    public CareKind? CountsAs { get; set; }

    /// <summary>Whether its entries are also written on the plant's history.</summary>
    public bool OnHistory { get; set; }

    /// <summary>The id a new kind with this name gets. The same for "Misted" and " misted ".</summary>
    public static Guid IdFor(string name) => NameBasedId.For(IdNamespace, name.Trim().ToLowerInvariant());

    /// <summary>
    /// The kind written on a care entry of this kind: what it counts as, or Rotated for neither.
    /// Nothing reads Rotated apart from its label, so an older version shows such an entry as
    /// "Rotated" and counts it as nothing. An entry with an own kind is never shown as Rotated here.
    /// </summary>
    public CareKind LogKind => CountsAs ?? CareKind.Rotated;

    public OwnCareKind Copy() => (OwnCareKind)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the kind of care a name.");
        if (CountsAs is not (null or CareKind.Watered or CareKind.Fertilised))
            errors.Add("A kind of care can count as a watering, a feed or neither.");
        return errors;
    }
}
