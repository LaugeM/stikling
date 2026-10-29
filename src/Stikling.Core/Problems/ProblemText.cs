using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Problems;

/// <summary>
/// The words for problems, kept out of the pages so they can be tested. They only say what the
/// person saw and did, never what to do.
/// </summary>
public static class ProblemText
{
    public static string Sign(ProblemSign sign) => sign switch
    {
        ProblemSign.YellowLeaves => Strings.Pests.SignYellowLeaves,
        ProblemSign.BrownTips => Strings.Pests.SignBrownTips,
        ProblemSign.Drooping => Strings.Pests.SignDrooping,
        ProblemSign.Spots => Strings.Pests.SignSpots,
        ProblemSign.LeafDrop => Strings.Pests.SignLeafDrop,
        ProblemSign.RootRot => Strings.Pests.SignRootRot,
        ProblemSign.MushyStem => Strings.Pests.SignMushyStem,
        ProblemSign.Sunburn => Strings.Pests.SignSunburn,
        ProblemSign.Leggy => Strings.Pests.SignLeggy,
        ProblemSign.Edema => Strings.Pests.SignEdema,
        _ => Strings.Pests.SignOther
    };

    public static string Action(ProblemAction action) => action switch
    {
        ProblemAction.CutOffLeaves => Strings.Pests.ActionCutOffLeaves,
        ProblemAction.MovedIt => Strings.Pests.ActionMovedIt,
        ProblemAction.WateredLess => Strings.Pests.ActionWateredLess,
        ProblemAction.WateredMore => Strings.Pests.ActionWateredMore,
        ProblemAction.Repotted => Strings.Pests.ActionRepotted,
        ProblemAction.CutAwayRoots => Strings.Pests.ActionCutAwayRoots,
        ProblemAction.MoreLight => Strings.Pests.ActionMoreLight,
        _ => Strings.Pests.ActionLessLight
    };

    public static string Trend(ProblemTrend trend) => trend switch
    {
        ProblemTrend.Better => Strings.Pests.TrendBetter,
        ProblemTrend.Same => Strings.Pests.TrendSame,
        _ => Strings.Pests.TrendWorse
    };

    public static string Outcome(ProblemOutcome outcome) => outcome switch
    {
        ProblemOutcome.GotBetter => Strings.Pests.OutcomeGotBetter,
        ProblemOutcome.WentAway => Strings.Pests.OutcomeWentAway,
        ProblemOutcome.DidntHelp => Strings.Pests.OutcomeDidntHelp,
        _ => Strings.Pests.OutcomeDied
    };

    /// <summary>"Yellow leaves, drooping, root rot": the first as written, the rest in lower case.</summary>
    public static string Signs(IEnumerable<ProblemSign> signs) => Join(signs.Select(Sign));

    /// <summary>"Moved it, watered less".</summary>
    public static string Actions(IEnumerable<ProblemAction> actions) => Join(actions.Select(Action));

    /// <summary>What to fill in for why a plant died, when a problem ends that way.</summary>
    public static string CauseOfDeath(Problem problem) => Signs(problem.KnownSigns);

    // The history lines below are saved in the timeline as lines that any language can show (see HistoryText),
    // and as English text. Text saved as data stays the same whichever language the device that wrote it was using.

    private static string English(IEnumerable<HistoryLine> lines) => HistoryText.English(lines, e => e.ToString());

    /// <summary>The history line for a problem being opened.</summary>
    public static string OpenedLine(Problem problem) => English(OpenedLines(problem));

    /// <summary>The history line for an update.</summary>
    public static string UpdateLine(ProblemUpdate update) => English(UpdateLines(update));

    public static string ClosedLine(ProblemOutcome outcome) => English(ClosedLines(outcome));

    public static string ReopenedLine => English(ReopenedLines);

    public static IReadOnlyList<HistoryLine> OpenedLines(Problem problem) =>
        WithNote(HistoryText.Line("ProblemOpened", HistoryArg.OfAll(problem.KnownSigns)), problem.Note);

    public static IReadOnlyList<HistoryLine> UpdateLines(ProblemUpdate update)
    {
        var did = update.KnownDid;
        var line = (update.Trend, did.Count) switch
        {
            ({ } trend, > 0) => HistoryText.Line("ProblemUpdateTrendActions", HistoryArg.Of(trend), HistoryArg.OfAll(did)),
            ({ } trend, _) => HistoryText.Line("ProblemUpdateTrend", HistoryArg.Of(trend)),
            (null, > 0) => HistoryText.Line("ProblemUpdateActions", HistoryArg.OfAll(did)),
            _ => HistoryText.Line("ProblemUpdateEmpty")
        };
        return WithNote(line, update.Note);
    }

    public static IReadOnlyList<HistoryLine> ClosedLines(ProblemOutcome outcome) =>
        [HistoryText.Line("ProblemClosed", HistoryArg.Of(outcome))];

    public static IReadOnlyList<HistoryLine> ReopenedLines => [HistoryText.Line("ProblemReopened")];

    /// <summary>"better", "the same", "worse", for reading a row of trends aloud.</summary>
    public static string TrendWord(ProblemTrend trend) => trend switch
    {
        ProblemTrend.Better => Strings.Pests.TrendWordBetter,
        ProblemTrend.Same => Strings.Pests.TrendWordSame,
        _ => Strings.Pests.TrendWordWorse
    };

    /// <summary>"Updates: worse, the same, better", or null when none has a trend.</summary>
    public static string? TrendsLabel(IEnumerable<ProblemTrend> trends) =>
        trends.Select(TrendWord).ToList() is { Count: > 0 } words
            ? string.Format(CultureInfo.CurrentCulture, Strings.Pests.TrendsLabel, string.Join(", ", words))
            : null;

    /// <summary>"12 Oct".</summary>
    public static string ShortDay(DateOnly day) => day.ToString(Strings.Pests.DayMonthFormat, TextCulture.ForDates);

    /// <summary>"Mar 2026".</summary>
    public static string MonthYear(DateOnly day) => day.ToString(Strings.Pests.ShortMonthYearFormat, TextCulture.ForDates);

    /// <summary>"1 day", "9 days", "3 weeks", "2 months", "1 year".</summary>
    public static string Duration(int days) => days switch
    {
        < 14 => Plural.Of(Math.Max(days, 0), Strings.Pests.DurationDay_One, Strings.Pests.DurationDay_Other),
        < 60 => Plural.Of(days / 7, Strings.Pests.DurationWeek_One, Strings.Pests.DurationWeek_Other),
        < 730 => Plural.Of(days / 30, Strings.Pests.DurationMonth_One, Strings.Pests.DurationMonth_Other),
        _ => Plural.Of(days / 365, Strings.Pests.DurationYear_One, Strings.Pests.DurationYear_Other)
    };

    /// <summary>The days a problem has lasted: since it started, or until it was closed.</summary>
    public static int DaysLasted(Problem problem, DateOnly today) =>
        Math.Max(0, (problem.ClosedOn ?? today).DayNumber - problem.StartedOn.DayNumber);

    /// <summary>"Over after 23 days: got better", or without the outcome when a newer version wrote one this version doesn't know.</summary>
    public static string OverAfter(Problem problem) =>
        problem.Outcome is { } outcome
            ? string.Format(CultureInfo.CurrentCulture, Strings.Pests.OverAfterOutcome, Duration(DaysLasted(problem, problem.StartedOn)), LowerFirst(Outcome(outcome)))
            : string.Format(CultureInfo.CurrentCulture, Strings.Pests.OverAfter, Duration(DaysLasted(problem, problem.StartedOn)));

    /// <summary>What the problem page calls it: the signs, or "A problem" when none are known to this version.</summary>
    public static string Title(Problem problem) =>
        problem.KnownSigns.Count > 0 ? Signs(problem.KnownSigns) : Strings.Pests.ProblemFallbackTitle;

    /// <summary>"Mar 2026 · 3 weeks · got better", for a problem that is over.</summary>
    public static string PastLine(Problem problem) =>
        $"{MonthYear(problem.ClosedOn ?? problem.StartedOn)} · {Duration(DaysLasted(problem, problem.StartedOn))}"
        + (problem.Outcome is { } outcome ? " · " + LowerFirst(Outcome(outcome)) : "");

    /// <summary>"Mar 2026 · Moved it, watered less · got better": when, what was done then and how it ended.</summary>
    public static string PastOfSameKindLine(PastProblem past)
    {
        var parts = new List<string> { MonthYear(past.Problem.ClosedOn ?? past.Problem.StartedOn) };
        if (past.Did.Count > 0)
            parts.Add(Actions(past.Did));
        parts.Add(LowerFirst(Outcome(past.Outcome)));
        return string.Join(" · ", parts);
    }

    private static string Join(IEnumerable<string> labels) =>
        string.Join(", ", labels.Select((label, i) => i == 0 ? label : LowerFirst(label)));

    private static string LowerFirst(string text) =>
        text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static IReadOnlyList<HistoryLine> WithNote(HistoryLine line, string? note) =>
        string.IsNullOrWhiteSpace(note) ? [line] : [line, HistoryText.Line("TextLine", HistoryArg.Of(note.Trim()))];


}
