using Stikling.Core.Away;
using Strings = Stikling.Core.Resources;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Problems;

namespace Stikling.Core.Today;

/// <summary>A pest group on Today: what it is called and the cases in it.</summary>
public sealed record DuePestGroup(string Heading, IReadOnlyList<PestCaseView> Cases);

/// <summary>The day of the next round and how many wait for it, for the "Next round" lines.</summary>
public sealed record NextRoundCount(DateOnly Day, int Count);

/// <summary>What the to-do column on Today shows for one day.</summary>
public sealed record TodaySections
{
    public DateOnly Day { get; init; }
    public AwaySpan? AwayNow { get; init; }
    public DateOnly? BackDay { get; init; }
    public IReadOnlyList<BackRow> BackRows { get; init; } = [];

    /// <summary>The list of what to look at first is up, so Water and Worth a look are held back.</summary>
    public bool Holding { get; init; }

    public DailyRoundSuggestion? RoundSuggestion { get; init; }
    public bool WateringOn { get; init; }
    public IReadOnlyList<PestCaseView> DueTreatments { get; init; } = [];
    public IReadOnlyList<PestCaseView> DueChecks { get; init; } = [];
    public IReadOnlyList<PestCaseView> ReadyNow { get; init; } = [];
    public IReadOnlyList<PestCaseView> PlanDoneNow { get; init; } = [];
    public IReadOnlyList<AttentionItem> FlaggedNow { get; init; } = [];
    public IReadOnlyList<LookAgain> LookAgainNow { get; init; } = [];
    public IReadOnlyList<Plant> QuarantineNow { get; init; } = [];
    public IReadOnlyList<Plant> TransitionNow { get; init; } = [];
    public IReadOnlyList<(Plant Plant, DateOnly WindowStart)> RestNow { get; init; } = [];
    public OuterPotReminders OuterPot { get; init; } = OuterPotReminders.None;
    public IReadOnlyList<WateringDue> WateringDue { get; init; } = [];
    public IReadOnlyList<WateringDue> WaterNow { get; init; } = [];
    public IReadOnlyList<WateringDue> WaterComing { get; init; } = [];
    public NextRoundCount? NextRound { get; init; }
    public IReadOnlyList<IntervalItem> CareItems { get; init; } = [];
    public IReadOnlyList<IntervalGroup> CareNow { get; init; } = [];
    public IReadOnlyList<IntervalGroup> CareComing { get; init; } = [];
    public NextRoundCount? CareNextRound { get; init; }
    public IReadOnlyList<PropagationCheck> Checks { get; init; } = [];
    public IReadOnlyList<PropagationCheck> ChecksNow { get; init; } = [];
    public NextRoundCount? NextRoundChecks { get; init; }
    public IReadOnlyList<PhotoDue> PhotosDue { get; init; } = [];
    public bool PhotoRoundShows { get; init; }

    /// <summary>Everything listed as due, except care rows already ticked off, which the page knows about.</summary>
    public int DueCount { get; init; }

    public bool Away => AwayNow is not null;

    /// <summary>Treatments first, then the cases only being watched, each under its own heading when there are any.</summary>
    public IReadOnlyList<DuePestGroup> DuePests =>
        new[] { new DuePestGroup(Strings.Care.TreatmentDue, DueTreatments), new DuePestGroup(Strings.Care.CheckForPests, DueChecks) }
            .Where(g => g.Cases.Count > 0).ToList();

    /// <summary>The sections for the day, worked out from the records the way Today shows them.</summary>
    /// <param name="careItems">The care plan to use again, so a page that has changed nothing in it needn't plan twice.</param>
    public static TodaySections For(TodayData data, DateOnly day, TimeProvider time, IReadOnlyList<IntervalItem>? careItems = null)
    {
        var clock = DayClock.On(time, day);
        var settings = data.Settings;
        var putOffs = data.PutOffs;
        var plants = data.Plants;
        var propagations = data.Propagations;
        var places = data.Places;
        var roundDays = settings.RoundDays;
        var cleanWeeks = settings.CleanWeeks ?? PestService.DefaultCleanWeeks;
        var hemisphere = Seasons.HemisphereOf(clock.LocalTimeZone);
        Pot? PotOf(Guid id) => data.PotsById.GetValueOrDefault(id);

        var checks = TodayBoard.NeedsChecking(propagations, data.Latest, clock, careDays: data.PropagationCareDays, roundDays: roundDays, putOffs: putOffs);
        var nextRoundChecks = TodayBoard.NeedsCheckingNextRound(propagations, data.Latest, clock, roundDays, putOffs, careDays: data.PropagationCareDays);
        var flagged = TodayBoard.NeedsAttention(plants, propagations, day)
            .Where(i => !putOffs.IsPutOff(PutOffs.Flag(i.Subject.Id, i.Attention), day)).ToList();
        var lookAgain = ProblemService.LookAgainDue(data.Problems, data.ProblemUpdates, plants, day)
            .Where(i => !putOffs.IsPutOff(PutOffs.Problem(i.Problem.Id, i.Since), day)).ToList();

        careItems ??= data.CareIntervals.Count == 0
            ? []
            : CareIntervals.Plan(data.CareIntervals, plants, places, data.CareLogs, data.OwnCareKinds, data.Feeds, clock, roundDays, putOffs);
        var careNow = CareIntervals.Now(careItems, day, putOffs);
        var careComing = CareIntervals.Coming(careItems, day, putOffs);

        var photoRound = settings.PhotoReminder
            ? PhotoRound.Due(plants, propagations, data.NewestPhotos, places, clock)
            : [];
        var photoShows = photoRound.Count > 0 && !putOffs.IsPutOff(PutOffs.PhotoRound, day);

        var awayNow = AwayTime.Current(data.Stretches, day);
        var backDay = AwayTime.BackDay(data.Stretches, day);

        var roundSuggestion = settings.DailyRound
            ? DailyRound.Suggest(places, plants, propagations, data.CareLogs, data.LastActivity, putOffs, clock, roundDays)
            : null;

        var restAsk = RestSeason.AskNow(plants, p => DormantPeriods.Of(p, data.PlantChanges, clock), hemisphere, day)
            .Where(r => !putOffs.IsPutOff(PutOffs.Rest(r.Plant.Id, r.WindowStart), day)).ToList();

        var wateringDue = settings.WateringReminder
            ? WateringRound.Due(plants, data.CareLogs, data.PlantChanges, PotOf, data.Lights, places, data.Stretches, hemisphere, clock, putOffs, roundDays)
            : [];

        var described = PestService.Describe(data.PestCases, plants, data.PestTreatments, places, day, cleanWeeks, putOffs);
        var dueTreatments = PestService.Due(described);
        var readyNow = PestService.ReadyToResolve(described)
            .Where(v => !putOffs.IsPutOff(PutOffs.Resolve(v.Case.Id), day)).ToList();
        var planDoneNow = described.Where(v => v.Case.IsOpen && v.PlanDone).OrderBy(v => v.PlanDoneOn)
            .Where(v => !putOffs.IsPutOff(PutOffs.PlanDone(v.Case.Id), day)).ToList();

        IReadOnlyList<BackRow> backRows = [];
        if (backDay is { } backOn && awayNow is null)
        {
            var timeline = data.BackTimelines.GetValueOrDefault(backOn) ?? [];
            backRows = BackList.Of(backOn, plants, propagations, data.CareLogs, timeline, data.PestCases,
                PotOf, data.Lights, places, data.Stretches, hemisphere, clock, settings.BackListLength);
        }
        var holding = BackList.IsHolding(backDay, backRows, putOffs, day);

        var quarantineNow = TodayBoard.QuarantineUp(plants, day)
            .Where(p => !putOffs.IsPutOff(PutOffs.Quarantine(p.Id, p.QuarantinedSince!.Value), day)).ToList();
        var transitionNow = TodayBoard.TransitionQuiet(plants, day)
            .Where(p => !putOffs.IsPutOff(PutOffs.Transition(p.Id, p.TransitionLastNews!.Value), day)).ToList();

        // A propagation already under Needs attention isn't listed a second time
        var checksNow = checks
            .Where(c => !putOffs.IsPutOff(PutOffs.Check(c.Propagation.Id), day) && flagged.All(i => i.Subject.Id != c.Propagation.Id))
            .ToList();

        // A case that is ready to resolve is asked about under Ready to resolve instead of a weekly check
        var dueChecks = dueTreatments
            .Where(v => v.IsCheck && !(v.ReadyToResolve && !putOffs.IsPutOff(PutOffs.Resolve(v.Case.Id), day))).ToList();
        var dueTreatmentList = dueTreatments.Where(v => !v.IsCheck).ToList();

        var waterNow = wateringDue.Where(d => d.DueNow).ToList();
        // Plants gathered onto a round day are counted in the line on the next round, not listed as coming up
        var waterComing = wateringDue.Where(d => !d.DueNow && !d.Gathered).ToList();

        // On a day that isn't a round day: how many wait for the next one
        NextRoundCount? nextRound = null, careNextRound = null, checksNextRound = null;
        if (roundDays is { Count: > 0 } && !RoundSchedule.IsRoundDay(roundDays, day) && RoundSchedule.Next(roundDays, day) is { } next)
        {
            var careWaiting = CareIntervals.WaitingForRound(careItems, next, day, putOffs).Select(i => i.Plant.Id).ToHashSet();
            if (settings.WateringReminder)
            {
                var waiting = wateringDue.Where(d => d.Gathered && !d.DueNow && d.ShowOn == next).Select(d => d.Plant.Id).ToHashSet();
                if (waiting.Count > 0)
                {
                    // The plants waiting for care from an interval are on the same round
                    waiting.UnionWith(careWaiting);
                    nextRound = new(next, waiting.Count);
                }
            }

            if (nextRound is null && careWaiting.Count > 0)
                careNextRound = new(next, careWaiting.Count);

            var checkCount = nextRoundChecks.Count(c => !putOffs.IsPutOff(PutOffs.Check(c.Propagation.Id), day));
            if (checkCount > 0)
                checksNextRound = new(next, checkCount);
        }

        // While away nothing is asked for, and while the list on the day back is up it stands for Water and Worth a look
        var dueCount = awayNow is not null ? 0
            : dueTreatmentList.Count + dueChecks.Count + readyNow.Count + planDoneNow.Count + flagged.Count + lookAgain.Count
              + quarantineNow.Count + transitionNow.Count + restAsk.Count
              + (holding ? backRows.Count : checksNow.Count + wateringDue.Count(d => d.DueNow))
              + (holding ? 0 : careNow.Sum(g => g.Items.Count))
              + (roundSuggestion is not null ? 1 : 0)
              + (photoShows ? 1 : 0);

        return new TodaySections
        {
            Day = day,
            AwayNow = awayNow,
            BackDay = backDay,
            BackRows = backRows,
            Holding = holding,
            RoundSuggestion = roundSuggestion,
            WateringOn = settings.WateringReminder,
            DueTreatments = dueTreatmentList,
            DueChecks = dueChecks,
            ReadyNow = readyNow,
            PlanDoneNow = planDoneNow,
            FlaggedNow = flagged,
            LookAgainNow = lookAgain,
            QuarantineNow = quarantineNow,
            TransitionNow = transitionNow,
            RestNow = restAsk,
            OuterPot = OuterPotReminders.Find(plants, data.CareLogs, settings, clock),
            WateringDue = wateringDue,
            WaterNow = waterNow,
            WaterComing = waterComing,
            NextRound = nextRound,
            CareItems = careItems,
            CareNow = careNow,
            CareComing = careComing,
            CareNextRound = careNextRound,
            Checks = checks,
            ChecksNow = checksNow,
            NextRoundChecks = checksNextRound,
            PhotosDue = photoRound,
            PhotoRoundShows = photoShows,
            DueCount = dueCount
        };
    }
}
