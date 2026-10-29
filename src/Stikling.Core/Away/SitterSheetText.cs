using System.Globalization;
using System.Text;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Away;

/// <summary>The words on the plant-sitter sheet, and the sheet written out as plain text for sharing.</summary>
public static class SitterSheetText
{
    /// <summary>"From the bottom", the short name on a card.</summary>
    public static string MethodName(WaterMethod method) => method switch
    {
        WaterMethod.Top => Strings.Care.MethodTop,
        WaterMethod.Soak => Strings.Care.MethodSoak,
        _ => Strings.Care.MethodBottom
    };

    /// <summary>One line on what a way of watering means, for choosing it. Null is the plant with no way said.</summary>
    public static string MethodHint(WaterMethod? method) => method switch
    {
        null => Strings.Care.MethodHintNone,
        WaterMethod.Top => Strings.Care.MethodHintTop,
        WaterMethod.Soak => Strings.Care.MethodHintSoak,
        _ => Strings.Care.MethodHintBottom
    };

    /// <summary>The steps for a way of watering. They only apply once the plant has been checked.</summary>
    public static string MethodSteps(WaterMethod method) => method switch
    {
        WaterMethod.Top => Strings.Care.MethodStepsTop,
        WaterMethod.Soak => Strings.Care.MethodStepsSoak,
        _ => Strings.Care.MethodStepsBottom
    };

    /// <summary>"23 December".</summary>
    public static string Day(DateOnly date) => date.ToString(Strings.Care.SitterDayFormat, TextCulture.ForDates);

    /// <summary>"Tuesday 23 December".</summary>
    public static string LongDay(DateOnly date) => date.ToString(Strings.Care.SitterLongDayFormat, TextCulture.ForDates);

    /// <summary>"Tue 23".</summary>
    public static string ShortDay(DateOnly date) => date.ToString(Strings.Care.SitterShortDayFormat, TextCulture.ForDates);

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
                text.AppendLine(CareText.F(Strings.Care.SitterTextVisit, LongDay(visit.Date), visit.Count));
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
                    text.AppendLine(CareText.F(Strings.Care.SitterTextWhere, place));
                if (subject.How is { } how)
                    text.AppendLine(CareText.F(Strings.Care.SitterTextHow, how));
                if (subject.Meter is { } meter)
                    text.AppendLine(meter);
                if (subject.Pace is { } pace)
                    text.AppendLine(pace);
                if (subject.Note is { } subjectNote)
                    text.AppendLine(CareText.F(Strings.Care.SitterTextNote, subjectNote));

                if (subject.CheckNote is { } check)
                    text.AppendLine(check);
                else if (sheet.Layout == SheetLayout.Labels)
                    text.AppendLine(CareText.F(Strings.Care.SitterTextCheckOn, string.Join(", ", subject.Visits.Select(LongDay))));
            }
        }

        return text.ToString().TrimEnd() + Environment.NewLine;
    }
}
