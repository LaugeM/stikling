using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// One value measured on a plant or cutting, like its height or the number of its leaves. It is written
/// down together with a history entry (<see cref="EntryId"/>), and an entry with two values has two of
/// these. Either <see cref="Kind"/> is one of the built-in kinds, or <see cref="OwnKindId"/> is a kind
/// somebody added themselves, never both.
/// </summary>
public sealed class Measurement : Entity
{
    public SubjectType SubjectType { get; set; }

    /// <summary>The plant or propagation it was measured on.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>The history entry it was written down with.</summary>
    public Guid EntryId { get; set; }

    /// <summary>When it was measured, the same as the entry's day. A correction of the entry moves it too.</summary>
    public DateTimeOffset MeasuredAt { get; set; }

    /// <summary>
    /// A built-in kind. Read with the tolerant converter, so a kind added in a later version reads as null
    /// here; a measurement with neither this nor <see cref="OwnKindId"/> is left out everywhere.
    /// </summary>
    [JsonConverter(typeof(TolerantNullableEnumConverter<MeasureKind>))]
    public MeasureKind? Kind { get; set; }

    public Guid? OwnKindId { get; set; }

    /// <summary>The own kind's name as it was when this was written, since the server keeps nothing but the id and dates of a deleted kind.</summary>
    public string? OwnKindName { get; set; }

    /// <summary>Whether the own kind is a length (in cm) and not a count, copied like the name.</summary>
    public bool? OwnKindIsLength { get; set; }

    /// <summary>A length in cm rounded to 1 decimal, or a count as a whole number.</summary>
    public decimal Value { get; set; }

    /// <summary>True when the value is a length in cm. An own kind without the copy counts as a count.</summary>
    [JsonIgnore]
    public bool IsLength => Kind is { } kind ? MeasureKinds.IsLength(kind) : OwnKindIsLength ?? false;

    /// <summary>True when a version of the app can tell what this measures.</summary>
    [JsonIgnore]
    public bool HasKind => Kind is not null || OwnKindId is not null;

    public Measurement Copy() => (Measurement)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (SubjectType is not (SubjectType.Plant or SubjectType.Propagation) || SubjectId == Guid.Empty)
            errors.Add(Strings.Measures.NeedsSubject);
        if (EntryId == Guid.Empty)
            errors.Add(Strings.Measures.NeedsEntry);
        if ((Kind is null) == (OwnKindId is null))
            errors.Add(Strings.Measures.NeedsOneKind);
        if (OwnKindId is not null && string.IsNullOrWhiteSpace(OwnKindName))
            errors.Add(Strings.Measures.OwnKindNeedsName);
        if (Value < 0)
            errors.Add(Strings.Measures.ValueNegative);
        else if (!IsLength && Value != decimal.Truncate(Value))
            errors.Add(Strings.Measures.CountWhole);
        return errors;
    }
}
