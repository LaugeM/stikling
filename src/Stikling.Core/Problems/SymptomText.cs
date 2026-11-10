using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Problems;

/// <summary>
/// The words of the symptom check, kept out of the page so they can be tested. They say what to
/// look at and never what to do.
/// </summary>
public static class SymptomText
{
    public static string Name(SymptomCauseKind cause) => cause switch
    {
        SymptomCauseKind.TooWet => Strings.Symptoms.CauseTooWet,
        SymptomCauseKind.TooDry => Strings.Symptoms.CauseTooDry,
        SymptomCauseKind.TooLittleLight => Strings.Symptoms.CauseTooLittleLight,
        SymptomCauseKind.TooMuchSun => Strings.Symptoms.CauseTooMuchSun,
        SymptomCauseKind.RecentChange => Strings.Symptoms.CauseRecentChange,
        SymptomCauseKind.Cold => Strings.Symptoms.CauseCold,
        SymptomCauseKind.DryAir => Strings.Symptoms.CauseDryAir,
        SymptomCauseKind.Salts => Strings.Symptoms.CauseSalts,
        SymptomCauseKind.Hungry => Strings.Symptoms.CauseHungry,
        SymptomCauseKind.RootsFillPot => Strings.Symptoms.CauseRootsFillPot,
        SymptomCauseKind.Pests => Strings.Symptoms.CausePests,
        SymptomCauseKind.LeafSpot => Strings.Symptoms.CauseLeafSpot,
        SymptomCauseKind.Ageing => Strings.Symptoms.CauseAgeing,
        _ => Strings.Symptoms.CauseRest
    };

    /// <summary>What to look at for a cause, one line each.</summary>
    public static IReadOnlyList<string> Checks(SymptomCauseKind cause) => cause switch
    {
        SymptomCauseKind.TooWet =>
            [Strings.Symptoms.CheckTooWet1, Strings.Symptoms.CheckTooWet2, Strings.Symptoms.CheckTooWet3],
        SymptomCauseKind.TooDry =>
            [Strings.Symptoms.CheckTooDry1, Strings.Symptoms.CheckTooDry2],
        SymptomCauseKind.TooLittleLight =>
            [Strings.Symptoms.CheckTooLittleLight1, Strings.Symptoms.CheckTooLittleLight2],
        SymptomCauseKind.TooMuchSun =>
            [Strings.Symptoms.CheckTooMuchSun1, Strings.Symptoms.CheckTooMuchSun2],
        SymptomCauseKind.RecentChange =>
            [Strings.Symptoms.CheckRecentChange1, Strings.Symptoms.CheckRecentChange2],
        SymptomCauseKind.Cold =>
            [Strings.Symptoms.CheckCold1],
        SymptomCauseKind.DryAir =>
            [Strings.Symptoms.CheckDryAir1, Strings.Symptoms.CheckDryAir2],
        SymptomCauseKind.Salts =>
            [Strings.Symptoms.CheckSalts1, Strings.Symptoms.CheckSalts2],
        SymptomCauseKind.Hungry =>
            [Strings.Symptoms.CheckHungry1, Strings.Symptoms.CheckHungry2],
        SymptomCauseKind.RootsFillPot =>
            [Strings.Symptoms.CheckRootsFillPot1, Strings.Symptoms.CheckRootsFillPot2],
        SymptomCauseKind.Pests =>
            [Strings.Symptoms.CheckPests1, Strings.Symptoms.CheckPests2],
        SymptomCauseKind.LeafSpot =>
            [Strings.Symptoms.CheckLeafSpot1, Strings.Symptoms.CheckLeafSpot2],
        SymptomCauseKind.Ageing =>
            [Strings.Symptoms.CheckAgeing1],
        _ =>
            [Strings.Symptoms.CheckRest1, Strings.Symptoms.CheckRest2]
    };

    /// <summary>What is worth a look whatever it is, shown when the answers don't point to one cause.</summary>
    public static IReadOnlyList<string> GeneralChecks =>
        [Strings.Symptoms.GeneralSoil, Strings.Symptoms.GeneralRoots, Strings.Symptoms.GeneralLight, Strings.Symptoms.GeneralLeaves];

    public static string SourceTitle(SymptomSource source) => source switch
    {
        SymptomSource.UcIpm => Strings.Symptoms.SourceUcIpm,
        SymptomSource.Rhs => Strings.Symptoms.SourceRhs,
        SymptomSource.Caes => Strings.Symptoms.SourceCaes,
        _ => Strings.Symptoms.SourceIsu
    };

    /// <summary>"today" or "5 days ago".</summary>
    public static string DaysAgo(int days) =>
        days <= 0
            ? Strings.Symptoms.FactToday
            : F(Strings.Symptoms.FactAgo, ProblemText.Duration(days));

    public static string Arrived(int days) => F(Strings.Symptoms.FactArrived, DaysAgo(days));

    public static string Repotted(int days) => F(Strings.Symptoms.FactRepotted, DaysAgo(days));

    public static string Moved(int days) => F(Strings.Symptoms.FactMoved, DaysAgo(days));

    public static string Meter(int reading, int days) =>
        F(Strings.Symptoms.FactMeter, reading, DaysAgo(days));

    public static string Watered(int days) => F(Strings.Symptoms.FactWateredRecently, DaysAgo(days));

    public static string LongerThanUsual(int daysSince, int usual) =>
        F(Strings.Symptoms.FactLongerThanUsual,
            ProblemText.Duration(daysSince), ProblemText.Duration(usual));

    public static string FedOften(int days) => F(Strings.Symptoms.FactFedOften, days);

    /// <summary>The text of a fact that needs no number.</summary>
    public static string Plain(SymptomFactKind kind) => kind switch
    {
        SymptomFactKind.DarkerThanItLikes => Strings.Symptoms.FactDarker,
        SymptomFactKind.BrighterThanItLikes => Strings.Symptoms.FactBrighter,
        SymptomFactKind.LikesDryOut => Strings.Symptoms.FactLikesDryOut,
        SymptomFactKind.LikesHumidity => Strings.Symptoms.FactLikesHumidity,
        SymptomFactKind.FedLastMonth => Strings.Symptoms.FactFedLastMonth,
        SymptomFactKind.NotFed => Strings.Symptoms.FactNotFed,
        SymptomFactKind.NotRepotted => Strings.Symptoms.FactNotRepotted,
        SymptomFactKind.PestCase => Strings.Symptoms.FactPestCase,
        SymptomFactKind.Resting => Strings.Symptoms.FactResting,
        SymptomFactKind.WaterCulture => Strings.Symptoms.FactWaterCulture,
        SymptomFactKind.DarkerHalf => Strings.Symptoms.FactDarkerHalf,
        SymptomFactKind.HeatingHalf => Strings.Symptoms.FactHeatingHalf,
        _ => kind.ToString()
    };

    public static string Dormant => Strings.Symptoms.FactDormant;

    /// <summary>"From the meter reading on 3 Nov".</summary>
    public static string FromMeter(DateOnly day) =>
        F(Strings.Symptoms.FromMeter, ProblemText.ShortDay(day));
}
