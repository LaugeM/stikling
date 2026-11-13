using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Measurements;

/// <summary>
/// The history entry of measurements taken with no note and no photos: a change entry with the event
/// <see cref="TimelineEvent.Measured"/>. Its line is kept as a template and values, so each device shows
/// it in its own language and units, and as English text in cm for the code and the versions that read that.
/// </summary>
public static class MeasuredEntries
{
    /// <summary>The key of the line. It has any number of values, so it is put together in code like a care line, not from one template.</summary>
    public const string LineKey = "Measured";

    private const string LengthUnit = "MeasureUnit.Length";
    private const string CountUnit = "MeasureUnit.Count";
    private const string KindPrefix = "MeasureKind.";

    public static bool IsMeasured(TimelineEntry entry) =>
        entry.Kind == TimelineKind.Change && entry.Event == TimelineEvent.Measured;

    /// <summary>
    /// The line for these measurements. Each one is three values: its kind (an enum, or the name of an
    /// own kind), the number, and whether it is a length or a count.
    /// </summary>
    public static HistoryLine Line(IEnumerable<Measurement> measurements)
    {
        var line = new HistoryLine { Key = LineKey };
        foreach (var m in MeasureText.InOrder(measurements))
        {
            line.Args.Add(m.Kind is { } kind ? HistoryArg.Of(kind) : HistoryArg.Of(m.OwnKindName ?? ""));
            line.Args.Add(HistoryArg.Of(m.Value));
            line.Args.Add(new HistoryArg { Enum = m.IsLength ? LengthUnit : CountUnit });
        }
        return line;
    }

    /// <summary>Makes the entry the one-line entry of these measurements, with its line and its English text.</summary>
    public static void Apply(TimelineEntry entry, IEnumerable<Measurement> measurements)
    {
        entry.Kind = TimelineKind.Change;
        entry.Event = TimelineEvent.Measured;
        HistoryText.Set(entry, [Line(measurements)]);
    }

    /// <summary>The line in the given language. Null for a line this version can't read.</summary>
    /// <param name="saved">The English text that is saved with the entry, which is always in cm.</param>
    public static string? Render(HistoryLine line, CultureInfo culture, bool saved)
    {
        if (line.Args.Count == 0 || line.Args.Count % 3 != 0)
            return null;

        var phrases = new List<string>();
        for (var i = 0; i < line.Args.Count; i += 3)
        {
            var (kindArg, valueArg, unitArg) = (line.Args[i], line.Args[i + 1], line.Args[i + 2]);
            if (valueArg.Number is not { } value || unitArg.Enum is not (LengthUnit or CountUnit))
                return null;

            MeasureKind? kind = null;
            if (kindArg.Enum is { } text)
            {
                if (!text.StartsWith(KindPrefix, StringComparison.Ordinal)
                    || !Enum.TryParse<MeasureKind>(text[KindPrefix.Length..], out var known) || !Enum.IsDefined(known))
                    return null;
                kind = known;
            }
            else if (kindArg.Text is null)
            {
                return null;
            }

            phrases.Add(MeasureText.Phrase(kind, kindArg.Text, unitArg.Enum == LengthUnit, value, culture, saved));
        }

        var pattern = Strings.Measures.ResourceManager.GetString("MeasuredLine", culture);
        return pattern is null ? null : string.Format(culture, pattern, string.Join(", ", phrases));
    }
}
