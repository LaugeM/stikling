using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Core.Problems;

/// <summary>
/// The words for problems, kept out of the pages so they can be tested. They only say what the
/// person saw and did, never what to do.
/// </summary>
public static class ProblemText
{
    public static string Sign(ProblemSign sign) => sign switch
    {
        ProblemSign.YellowLeaves => "Yellow leaves",
        ProblemSign.BrownTips => "Brown or crispy tips",
        ProblemSign.Drooping => "Drooping",
        ProblemSign.Spots => "Spots on leaves",
        ProblemSign.LeafDrop => "Dropping leaves",
        ProblemSign.RootRot => "Root rot",
        ProblemSign.MushyStem => "Mushy stem",
        ProblemSign.Sunburn => "Sunburn",
        ProblemSign.Leggy => "Leggy or pale",
        ProblemSign.Edema => "Bumps under the leaves",
        _ => "Something else"
    };

    public static string Action(ProblemAction action) => action switch
    {
        ProblemAction.CutOffLeaves => "Cut off leaves",
        ProblemAction.MovedIt => "Moved it",
        ProblemAction.WateredLess => "Watered less",
        ProblemAction.WateredMore => "Watered more",
        ProblemAction.Repotted => "Repotted",
        ProblemAction.CutAwayRoots => "Cut away roots",
        ProblemAction.MoreLight => "More light",
        _ => "Less light"
    };

    public static string Trend(ProblemTrend trend) => trend switch
    {
        ProblemTrend.Better => "Better",
        ProblemTrend.Same => "About the same",
        _ => "Worse"
    };

    public static string Outcome(ProblemOutcome outcome) => outcome switch
    {
        ProblemOutcome.GotBetter => "Got better",
        ProblemOutcome.WentAway => "Went away by itself",
        ProblemOutcome.DidntHelp => "Nothing helped",
        _ => "It died"
    };

    /// <summary>"Yellow leaves, drooping, root rot": the first as written, the rest in lower case.</summary>
    public static string Signs(IEnumerable<ProblemSign> signs) => Join(signs.Select(Sign));

    /// <summary>"Moved it, watered less".</summary>
    public static string Actions(IEnumerable<ProblemAction> actions) => Join(actions.Select(Action));

    /// <summary>What to fill in for why a plant died, when a problem ends that way.</summary>
    public static string CauseOfDeath(Problem problem) => Signs(problem.KnownSigns);

    /// <summary>The history line for a problem being opened.</summary>
    public static string OpenedLine(Problem problem) =>
        WithNote("Something's wrong: " + LowerFirst(Signs(problem.KnownSigns)), problem.Note);

    /// <summary>The history line for an update.</summary>
    public static string UpdateLine(ProblemUpdate update)
    {
        var parts = new List<string>();
        if (update.Trend is { } trend)
            parts.Add(Trend(trend));
        var did = Actions(update.KnownDid);
        if (did.Length > 0)
            parts.Add(did);
        var text = parts.Count == 0 ? "Update on the problem" : string.Join(" · ", parts);
        return WithNote(text, update.Note);
    }

    public static string ClosedLine(ProblemOutcome outcome) => "Problem over: " + LowerFirst(Outcome(outcome));

    public const string ReopenedLine = "The problem came back";

    /// <summary>"better", "the same", "worse", for reading a row of trends aloud.</summary>
    public static string TrendWord(ProblemTrend trend) => trend switch
    {
        ProblemTrend.Better => "better",
        ProblemTrend.Same => "the same",
        _ => "worse"
    };

    /// <summary>"Updates: worse, the same, better", or null when none has a trend.</summary>
    public static string? TrendsLabel(IEnumerable<ProblemTrend> trends) =>
        trends.Select(TrendWord).ToList() is { Count: > 0 } words ? "Updates: " + string.Join(", ", words) : null;

    /// <summary>"12 Oct".</summary>
    public static string ShortDay(DateOnly day) => day.ToString("d MMM", CultureInfo.InvariantCulture);

    /// <summary>"Mar 2026".</summary>
    public static string MonthYear(DateOnly day) => day.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"1 day", "9 days", "3 weeks", "2 months", "1 year".</summary>
    public static string Duration(int days) => days switch
    {
        < 14 => Count(Math.Max(days, 0), "day"),
        < 60 => Count(days / 7, "week"),
        < 730 => Count(days / 30, "month"),
        _ => Count(days / 365, "year")
    };

    /// <summary>The days a problem has lasted: since it started, or until it was closed.</summary>
    public static int DaysLasted(Problem problem, DateOnly today) =>
        Math.Max(0, (problem.ClosedOn ?? today).DayNumber - problem.StartedOn.DayNumber);

    /// <summary>"Over after 23 days: got better".</summary>
    public static string OverAfter(Problem problem) =>
        $"Over after {Duration(DaysLasted(problem, problem.StartedOn))}: "
        + LowerFirst(Outcome(problem.Outcome ?? ProblemOutcome.GotBetter));

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

    private static string Count(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";

    private static string Join(IEnumerable<string> labels) =>
        string.Join(", ", labels.Select((label, i) => i == 0 ? label : LowerFirst(label)));

    private static string LowerFirst(string text) =>
        text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string WithNote(string text, string? note) =>
        string.IsNullOrWhiteSpace(note) ? text : text + "\n" + note.Trim();
}
