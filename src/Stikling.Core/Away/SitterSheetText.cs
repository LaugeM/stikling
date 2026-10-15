using System.Globalization;
using System.Text;
using Stikling.Core.Models;

namespace Stikling.Core.Away;

/// <summary>The words on the plant-sitter sheet, and the sheet written out as plain text for sharing.</summary>
public static class SitterSheetText
{
    /// <summary>"From the bottom", the short name on a card.</summary>
    public static string MethodName(WaterMethod method) => method switch
    {
        WaterMethod.Top => "From the top",
        WaterMethod.Soak => "Soak",
        _ => "From the bottom"
    };

    /// <summary>The steps for a way of watering. They only apply once the plant has been checked.</summary>
    public static string MethodSteps(WaterMethod method) => method switch
    {
        WaterMethod.Top =>
            "Water from the top until a little runs out below. After 15 to 30 minutes, look in the saucer. "
            + "If it's all been taken up, give a bit more and check again. Pour away what's left.",
        WaterMethod.Soak =>
            "Stand the whole pot in a bowl of water for 15 to 30 minutes, then let it drain well.",
        _ =>
            "Stand it in a saucer of water. After 15 to 30 minutes, see if it has taken it all up. "
            + "If it has, give it a little more and check again after another 15 to 30 minutes. "
            + "Once it stops taking water up, pour away what's left."
    };

    /// <summary>"23 December".</summary>
    public static string Day(DateOnly date) => date.ToString("d MMMM", CultureInfo.InvariantCulture);

    /// <summary>"Tuesday 23 December".</summary>
    public static string LongDay(DateOnly date) => date.ToString("dddd d MMMM", CultureInfo.InvariantCulture);

    /// <summary>"Tue 23".</summary>
    public static string ShortDay(DateOnly date) => date.ToString("ddd d", CultureInfo.InvariantCulture);

    /// <summary>The sheet as plain text, in the order it is printed.</summary>
    public static string Write(SitterSheetView sheet)
    {
        var text = new StringBuilder();
        text.AppendLine(sheet.Title);
        if (sheet.Header.Length > 0)
            text.AppendLine(sheet.Header);
        if (sheet.GeneralNote is { } note)
        {
            text.AppendLine();
            text.AppendLine(note);
        }

        text.AppendLine();
        text.AppendLine(SitterBox.Heading);
        foreach (var paragraph in sheet.Box.Check)
            text.AppendLine(paragraph);
        if (sheet.Box.Steps.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(SitterBox.StepsHeading);
            foreach (var step in sheet.Box.Steps)
                text.AppendLine(step.Name is null ? step.Text : $"{step.Name}: {step.Text}");
            if (sheet.Box.TopUp is { } topUp)
                text.AppendLine(topUp);
        }

        if (sheet.Layout != SheetLayout.Labels && sheet.HasDates)
        {
            foreach (var visit in sheet.ByVisit)
            {
                text.AppendLine();
                text.AppendLine($"{LongDay(visit.Date)}: check {visit.Count}");
                foreach (var group in visit.Groups)
                {
                    text.AppendLine(group.Heading);
                    foreach (var subject in group.Subjects)
                        text.AppendLine($"[ ] {subject.Label}{(subject.Spot is { } spot ? $" ({spot})" : "")}");
                }
            }
        }

        foreach (var group in sheet.Groups)
        {
            text.AppendLine();
            text.AppendLine(group.Heading.ToUpperInvariant());
            foreach (var subject in group.Subjects)
            {
                text.AppendLine();
                text.AppendLine(subject.Species is { } species ? $"{subject.Label} ({species})" : subject.Label);
                if (subject.Place is { } place)
                    text.AppendLine($"Where: {place}");
                if (subject.How is { } how)
                    text.AppendLine($"How: {how}");
                if (subject.Meter is { } meter)
                    text.AppendLine(meter);
                if (subject.Pace is { } pace)
                    text.AppendLine(pace);
                if (subject.Note is { } subjectNote)
                    text.AppendLine($"Note: {subjectNote}");

                if (subject.CheckNote is { } check)
                    text.AppendLine(check);
                else if (sheet.Layout == SheetLayout.Labels)
                    text.AppendLine($"Check on: {string.Join(", ", subject.Visits.Select(LongDay))}");
            }
        }

        return text.ToString().TrimEnd() + Environment.NewLine;
    }
}
