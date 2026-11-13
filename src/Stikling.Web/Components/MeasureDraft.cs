using System.Globalization;
using Stikling.Core.Measurements;
using Stikling.Core.Models;
using static Stikling.Core.Text.Format;
using Strings = Stikling.Web.Resources;

namespace Stikling.Web.Components;

/// <summary>One box in the measurement fields: a built-in kind or an own kind.</summary>
public sealed record MeasureField(string Key, string Label, bool IsLength, MeasureKind? Kind, Guid? OwnKindId)
{
    public static MeasureField Of(MeasureKind kind) =>
        new(kind.ToString(), MeasureText.KindName(kind), MeasureKinds.IsLength(kind), kind, null);

    public static MeasureField Of(OwnMeasureKind kind) =>
        new(kind.Id.ToString(), kind.Name, kind.IsLength, null, kind.Id);

    /// <summary>The box for a measurement already on an entry, from the name and unit the measurement kept. It stands in for an own kind that has been removed.</summary>
    public static MeasureField Of(Measurement measurement) => measurement.Kind is { } kind
        ? Of(kind)
        : new(measurement.OwnKindId.ToString()!, measurement.OwnKindName ?? "", measurement.IsLength, null, measurement.OwnKindId);
}

/// <summary>
/// What has been typed in the measurement fields of a note sheet or of a correction, kept by the page so it can be
/// saved with the entry. <see cref="MeasureFields"/> draws it and fills <see cref="Own"/>.
/// </summary>
public sealed class MeasureDraft
{
    private readonly Dictionary<string, string?> texts = [];
    private IReadOnlyList<MeasureField> existing = [];

    // The values already on the entry, as stored, and the boxes that have been typed in since. A box that was not touched
    // keeps its stored value exactly, since what the box shows is rounded and, in inches, would not always come back the same.
    private readonly Dictionary<string, decimal> stored = [];
    private readonly HashSet<string> typedIn = [];

    /// <summary>Whether the block of fields is open.</summary>
    public bool Open { get; set; }

    /// <summary>The kinds people added themselves that are still there. Filled by <see cref="MeasureFields"/>.</summary>
    public IReadOnlyList<OwnMeasureKind> Own { get; set; } = [];

    /// <summary>A draft with the values of an entry already in it, for correcting it. It starts open when there are any.</summary>
    public static MeasureDraft For(IEnumerable<Measurement> measurements)
    {
        var draft = new MeasureDraft();
        var list = MeasureText.InOrder(measurements);
        draft.existing = [.. list.Select(MeasureField.Of)];
        foreach (var m in list)
        {
            var key = MeasureField.Of(m).Key;
            draft.Set(key, Shown(m.IsLength, m.Value));
            draft.stored[key] = m.Value;
        }
        draft.Open = list.Count > 0;
        return draft;
    }

    /// <summary>The boxes: the built-in kinds, then the kinds of your own, then the removed kinds the entry still has a value for.</summary>
    public IReadOnlyList<MeasureField> Fields()
    {
        List<MeasureField> fields = [.. MeasureText.BuiltIn.Select(MeasureField.Of), .. Own.Select(MeasureField.Of)];
        fields.AddRange(existing.Where(e => e.OwnKindId is { } id && Own.All(o => o.Id != id)));
        return fields;
    }

    public string? Get(string key) => texts.GetValueOrDefault(key);

    public void Set(string key, string? text) => texts[key] = text;

    /// <summary>Sets a box from what the person typed in it.</summary>
    public void Type(string key, string? text)
    {
        texts[key] = text;
        typedIn.Add(key);
    }

    /// <summary>True when any box has something in it.</summary>
    public bool HasInput => texts.Values.Any(t => !string.IsNullOrWhiteSpace(t));

    public void Clear()
    {
        texts.Clear();
        stored.Clear();
        typedIn.Clear();
        existing = [];
        Open = false;
    }

    /// <summary>
    /// The values that were typed, in cm for a length. An empty box is skipped. Returns null with the error when a box does not hold a number.
    /// </summary>
    public IReadOnlyList<MeasureValue>? Values(out string? error)
    {
        error = null;
        var values = new List<MeasureValue>();
        foreach (var field in Fields())
        {
            var text = Get(field.Key)?.Trim();
            if (string.IsNullOrEmpty(text))
                continue;

            if (!typedIn.Contains(field.Key) && stored.TryGetValue(field.Key, out var kept))
            {
                values.Add(field.Kind is { } own ? MeasureValue.Of(own, kept) : MeasureValue.OfOwn(field.OwnKindId!.Value, kept));
                continue;
            }

            // A number box gives the number with a point, whatever the language is
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed))
            {
                error = F(Strings.Measurements.FieldError, field.Label);
                return null;
            }

            var value = field.IsLength ? MeasureText.FromInput(typed) : typed;
            values.Add(field.Kind is { } kind ? MeasureValue.Of(kind, value) : MeasureValue.OfOwn(field.OwnKindId!.Value, value));
        }
        return values;
    }

    // A length in the unit chosen in Settings, to 1 decimal, and a count as it is
    private static string Shown(bool isLength, decimal value) =>
        (isLength ? MeasureText.ToInput(value) : value).ToString("0.#", CultureInfo.InvariantCulture);
}
