using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Pests;

/// <summary>A case and what the screens need to know about it.</summary>
/// <param name="Plants">The plants the case covers right now.</param>
/// <param name="Last">The most recent treatment, or null when there hasn't been one.</param>
/// <param name="NextDue">When the next treatment is due, or null for a resolved case.</param>
/// <param name="Last">The most recent treatment or check, or null when there hasn't been one.</param>
/// <param name="NextDue">When the next treatment is due, or the next check on a case being watched.
/// Null for a resolved case.</param>
/// <param name="DaysUntilDue">Negative when it's overdue, 0 when it's due today.</param>
/// <param name="Room">The room or spot the case covers, "Living room", when it covers one.</param>
/// <param name="CleanDays">Days since the last treatment on a case being watched, null otherwise.</param>
/// <param name="CleanTarget">The days the count has to reach.</param>
/// <param name="ReadyToResolve">The target is reached and there's a check from the last week.</param>
/// <param name="Step">The step of the plan being followed that is up next, or null without a running plan.</param>
/// <param name="StepNumber">The 1-based number of <paramref name="Step"/>, 0 when there is none.</param>
/// <param name="StepCount">How many steps the plan on the case has.</param>
/// <param name="PlanDoneOn">When the question after the last step is due, once every step is done.</param>
/// <param name="PlanDone">Every step is done and <paramref name="PlanDoneOn"/> has come.</param>
public sealed record PestCaseView(
    PestCase Case,
    string? Room,
    IReadOnlyList<Plant> Plants,
    PestTreatment? Last,
    DateOnly? NextDue,
    int? DaysUntilDue,
    int? CleanDays = null,
    int CleanTarget = PestService.DefaultCleanWeeks * 7,
    bool ReadyToResolve = false,
    PlanStep? Step = null,
    int StepNumber = 0,
    int StepCount = 0,
    DateOnly? PlanDoneOn = null,
    bool PlanDone = false)
{
    /// <summary>The weeks the count has to reach.</summary>
    public int CleanWeeks => CleanTarget / 7;

    public bool IsDue => DaysUntilDue is <= 0;

    /// <summary>What's due is a look for pests rather than a treatment.</summary>
    public bool IsCheck => Case.Status == PestCaseStatus.Monitoring;

    /// <summary>An open case being treated that has nothing logged yet, so its first treatment is due.</summary>
    public bool Untreated => Case.IsOpen && !IsCheck && Last is null;
}

/// <summary>A plant next to a case, and when it was last looked over for pests.</summary>
public sealed record Neighbour(Plant Plant, DateOnly? LastChecked);

/// <summary>
/// Reading pest cases back: which plants a case covers, and when the next treatment is due.
/// The writing side is plain repository calls, so everything worth testing lives here.
/// </summary>
public static class PestService
{
    /// <summary>
    /// How often a case being watched is checked. Most sprays don't kill the eggs, so pests can
    /// come back weeks after the last treatment, and a weekly look is the usual way to catch that.
    /// </summary>
    public const int CheckEveryDays = 7;

    /// <summary>The clean weeks a case being watched needs when Settings doesn't say otherwise.</summary>
    public const int DefaultCleanWeeks = 4;

    /// <summary>
    /// The plants a case covers. Everywhere and Room are worked out from the plants as they
    /// are now, so a plant moved into the room mid-outbreak is covered from then on.
    /// Plants that are no longer in the collection are left out either way.
    /// </summary>
    public static IReadOnlyList<Plant> PlantsIn(PestCase item, IEnumerable<Plant> plants, Places places)
    {
        var here = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active);

        var covered = item.Scope switch
        {
            PestScope.Everywhere => here,
            PestScope.Room => here.Where(p => places.IsIn(p.PlaceId, item.PlaceId)),
            _ => here.Where(p => item.PlantIds.Contains(p.Id))
        };

        return covered
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The open cases covering one plant, so its card and page can say so.</summary>
    public static IReadOnlyList<PestCase> CasesFor(Plant plant, IEnumerable<PestCase> cases, Places places) =>
        cases
            .Where(c => !c.IsDeleted && c.IsOpen && Covers(c, plant, places))
            .OrderBy(c => c.StartedOn)
            .ToList();

    /// <summary>One case's treatments, newest first.</summary>
    public static IReadOnlyList<PestTreatment> TreatmentsFor(IEnumerable<PestTreatment> treatments, Guid caseId) =>
        treatments
            .Where(t => !t.IsDeleted && t.CaseId == caseId)
            .OrderByDescending(t => t.OccurredOn)
            .ThenByDescending(t => t.CreatedAt)
            .ToList();

    /// <summary>
    /// When the next treatment is due: the last treatment's own date if one was set, otherwise
    /// the last treatment plus the case's interval. Before anything is logged the first treatment
    /// is due from the day the pests were found, since waiting an interval before treating at all
    /// isn't what anyone means. A case being watched is due a check a week after the last
    /// treatment, check or trap count instead. A resolved case is never due.
    /// </summary>
    public static DateOnly? NextDue(PestCase item, IEnumerable<PestTreatment> treatments, PutOffs? putOffs = null)
    {
        if (!item.IsOpen)
            return null;

        if (item.PlanRunning && item.PlanSteps.Count > 0)
        {
            // The step up next is what's due. When the last one is done the question that follows
            // is shown on its own, so nothing is due as a treatment until it's answered.
            var list = treatments.ToList();
            var index = PlanProgress.CurrentIndex(item, list);
            return index < 0 ? null : PlanProgress.DueOn(item, list, index, putOffs);
        }

        var last = LastFor(item, treatments);

        if (item.Status == PestCaseStatus.Monitoring)
            return (last?.OccurredOn ?? item.StartedOn).AddDays(CheckEveryDays);

        if (last is null)
            return item.StartedOn;

        return last.NextDueOn ?? last.OccurredOn.AddDays(item.IntervalDays);
    }

    /// <summary>Everything a screen needs about one case.</summary>
    public static PestCaseView Describe(
        PestCase item,
        IEnumerable<Plant> plants,
        IEnumerable<PestTreatment> treatments,
        Places places,
        DateOnly today,
        int cleanWeeks = DefaultCleanWeeks,
        PutOffs? putOffs = null)
    {
        var list = treatments.ToList();
        var next = NextDue(item, list, putOffs);
        var index = item.PlanRunning ? PlanProgress.CurrentIndex(item, list) : -1;
        var doneOn = PlanProgress.DoneOn(item, list);
        var since = CleanSince(item, list);
        int? days = since is { } s ? today.DayNumber - s.DayNumber : null;
        var target = Math.Max(1, cleanWeeks) * 7;

        var recent = since is { } from && TreatmentsFor(list, item.Id).Any(t =>
            (t.Kind == PestTreatmentKind.Checked || (t.Kind == PestTreatmentKind.TrapCount && t.OnTrap is not null))
            && t.OccurredOn >= from
            && t.OccurredOn >= today.AddDays(-(CheckEveryDays - 1))
            && t.OccurredOn <= today);

        return new PestCaseView(
            item,
            item.Scope == PestScope.Room ? places.NameOf(item.PlaceId) : null,
            PlantsIn(item, plants, places),
            LastFor(item, list),
            next,
            next is { } due ? due.DayNumber - today.DayNumber : null,
            days,
            target,
            days >= target && recent,
            index >= 0 ? item.PlanSteps[index] : null,
            index + 1,
            item.PlanStartedOn is null ? 0 : item.PlanSteps.Count,
            doneOn,
            doneOn is { } d && today >= d);
    }

    /// <summary>
    /// The day a case being watched has been clean since: its latest treatment, or the day it
    /// started when nothing was treated, but never before it last went back to treating. Null for a case that isn't being watched.
    /// </summary>
    public static DateOnly? CleanSince(PestCase item, IEnumerable<PestTreatment> treatments)
    {
        if (item.Status != PestCaseStatus.Monitoring)
            return null;

        var last = TreatmentsFor(treatments, item.Id).FirstOrDefault(t => t.Kind == PestTreatmentKind.Treated && !t.Skipped);
        var since = last?.OccurredOn ?? item.StartedOn;
        return item.TreatingAgainOn is { } again && again > since ? again : since;
    }

    /// <summary>
    /// Notes a change of status made on a case that had <paramref name="from"/>: going back to
    /// treating, or opening a resolved case again, restarts the clean count from today.
    /// </summary>
    public static void StatusChanged(PestCase item, PestCaseStatus from, DateOnly today, IEnumerable<PestTreatment>? treatments = null)
    {
        if (item.PlanStartedOn is not null && item.PlanStoppedOn is null)
        {
            // Leaving treatment stops a plan that was still going, but not one that had finished,
            // which only stops running because the status changed. Coming back to treating after
            // that is the plan being over, so it doesn't ask again.
            if (from == PestCaseStatus.Active && item.Status != PestCaseStatus.Active
                && !PlanProgress.IsFinished(item, treatments ?? []))
                item.PlanStoppedOn = today;
            else if (from != PestCaseStatus.Active && item.Status == PestCaseStatus.Active)
                item.PlanStoppedOn = today;
        }

        var again = (item.Status == PestCaseStatus.Active && from != PestCaseStatus.Active)
            || (from == PestCaseStatus.Resolved && item.IsOpen);

        if (again)
            item.TreatingAgainOn = today;
    }

    /// <summary>The cases that have been clean long enough and checked lately, so they can be resolved.</summary>
    public static IReadOnlyList<PestCaseView> ReadyToResolve(IEnumerable<PestCaseView> cases) =>
        cases
            .Where(v => v.Case.IsOpen && v.ReadyToResolve)
            .OrderByDescending(v => v.CleanDays)
            .ToList();

    /// <summary>A check on a single plant outside any case, dated today.</summary>
    public static PestTreatment StartPlantCheck(Plant plant, DateOnly today) =>
        new() { PlantId = plant.Id, OccurredOn = today, Kind = PestTreatmentKind.Checked };

    /// <summary>A plant's own checks outside a case, newest first.</summary>
    public static IReadOnlyList<PestTreatment> ChecksFor(Guid plantId, IEnumerable<PestTreatment> treatments) =>
        treatments
            .Where(t => !t.IsDeleted && t.PlantId == plantId && t.Kind == PestTreatmentKind.Checked)
            .OrderByDescending(t => t.OccurredOn)
            .ThenByDescending(t => t.CreatedAt)
            .ToList();

    /// <summary>
    /// When the plant was last looked over for pests: its own checks, and checks and trap counts
    /// on any case that covers it now. Treatments don't count. Null when never.
    /// </summary>
    public static DateOnly? LastChecked(
        Plant plant,
        IEnumerable<PestCase> cases,
        IEnumerable<PestTreatment> treatments,
        Places places)
    {
        var logged = treatments.Where(t => !t.IsDeleted).ToList();
        DateOnly? latest = null;

        foreach (var t in logged)
        {
            if (t.PlantId == plant.Id && t.Kind == PestTreatmentKind.Checked)
                latest = Later(latest, t.OccurredOn);
        }

        foreach (var c in cases.Where(c => !c.IsDeleted && Covers(c, plant, places)))
        {
            foreach (var t in logged.Where(t => t.CaseId == c.Id))
            {
                if (t.Kind == PestTreatmentKind.Checked || (t.Kind == PestTreatmentKind.TrapCount && t.OnTrap is not null))
                    latest = Later(latest, t.OccurredOn);
            }
        }

        return latest;
    }

    /// <summary>
    /// The plants to look over while a case on picked plants is being treated: the others in the
    /// rooms of the picked ones, spots included. Never-checked first, then the longest ago.
    /// </summary>
    public static IReadOnlyList<Neighbour> Neighbours(
        PestCase item,
        IEnumerable<Plant> plants,
        IEnumerable<PestCase> cases,
        IEnumerable<PestTreatment> treatments,
        Places places)
    {
        if (item.Scope != PestScope.PickedPlants || item.Status != PestCaseStatus.Active)
            return [];

        var all = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();
        var caseList = cases.ToList();
        var logged = treatments.ToList();

        var rooms = all
            .Where(p => item.PlantIds.Contains(p.Id) && p.PlaceId is not null)
            .Select(p => places.RoomOf(p.PlaceId))
            .OfType<Place>()
            .Select(r => r.Id)
            .Distinct()
            .ToList();

        return all
            .Where(p => !item.PlantIds.Contains(p.Id)
                && p.PlaceId is not null
                && rooms.Any(r => places.IsIn(p.PlaceId, r)))
            .Select(p => new Neighbour(p, LastChecked(p, caseList, logged, places)))
            .OrderBy(n => n.LastChecked is null ? 0 : 1)
            .ThenBy(n => n.LastChecked)
            .ThenBy(n => n.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static DateOnly Later(DateOnly? a, DateOnly b) => a is { } x && x > b ? x : b;

    /// <summary>Every case described, newest first, so the list screen is one call.</summary>
    public static IReadOnlyList<PestCaseView> Describe(
        IEnumerable<PestCase> cases,
        IEnumerable<Plant> plants,
        IEnumerable<PestTreatment> treatments,
        Places places,
        DateOnly today,
        int cleanWeeks = DefaultCleanWeeks,
        PutOffs? putOffs = null)
    {
        var all = plants.ToList();
        var logged = treatments.ToList();

        return cases
            .Where(c => !c.IsDeleted)
            .Select(c => Describe(c, all, logged, places, today, cleanWeeks, putOffs))
            .OrderByDescending(v => v.Case.StartedOn)
            .ThenByDescending(v => v.Case.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// The cases wanting a treatment today, the most overdue first. This is what Today shows,
    /// and it's the reason the interval is worth filling in at all.
    /// </summary>
    public static IReadOnlyList<PestCaseView> Due(IEnumerable<PestCaseView> cases) =>
        cases
            .Where(v => v.Case.IsOpen && v.IsDue)
            .OrderBy(v => v.DaysUntilDue)
            .ToList();

    /// <summary>A blank case starting today, for the new-case form.</summary>
    public static PestCase Start(DateOnly today) =>
        new() { StartedOn = today, Scope = PestScope.PickedPlants, Status = PestCaseStatus.Active };

    /// <summary>
    /// A blank treatment for a case, dated today and carrying the last one forward, since the
    /// same spray is usually used again. A saved recipe comes back as it is now, so an edit to
    /// it since is picked up. One that has been deleted isn't offered at all.
    /// </summary>
    public static PestTreatment StartTreatment(
        PestCase item,
        IEnumerable<PestTreatment> treatments,
        DateOnly today,
        IEnumerable<TreatmentRecipe>? recipes = null)
    {
        var draft = new PestTreatment { CaseId = item.Id, OccurredOn = today };
        var last = TreatmentsFor(treatments, item.Id).FirstOrDefault(t => t.Kind == PestTreatmentKind.Treated && !t.Skipped);

        if (last?.RecipeId is { } id)
        {
            if (recipes?.FirstOrDefault(r => r.Id == id && !r.IsDeleted) is { } recipe)
                draft.Use(recipe);
        }
        else
        {
            draft.What = last?.What;
        }

        return draft;
    }

    /// <summary>A blank check for a case being watched, dated today.</summary>
    public static PestTreatment StartCheck(PestCase item, DateOnly today) =>
        new() { CaseId = item.Id, OccurredOn = today, Kind = PestTreatmentKind.Checked };

    /// <summary>A blank trap count for a case, dated today.</summary>
    public static PestTreatment StartTrapCount(PestCase item, DateOnly today) =>
        new() { CaseId = item.Id, OccurredOn = today, Kind = PestTreatmentKind.TrapCount };

    /// <summary>
    /// What each trap count caught, oldest first. A count is everything on the trap, so what's
    /// new is the count less what was on it last time, or all of it when a new trap went up in
    /// between. The days run from the last count or new trap, or from the day the case started
    /// when there hasn't been one. A count lower than the last one means the trap was swapped
    /// without saying so, and the whole count is taken as new.
    /// </summary>
    public static IReadOnlyList<TrapCatch> Catches(PestCase item, IEnumerable<PestTreatment> treatments)
    {
        var catches = new List<TrapCatch>();
        var since = item.StartedOn;
        var onTrap = 0;

        var counts = TreatmentsFor(treatments, item.Id)
            .Where(t => t.Kind == PestTreatmentKind.TrapCount)
            .Reverse();

        foreach (var count in counts)
        {
            if (count.OnTrap is { } total)
            {
                var caught = total >= onTrap ? total - onTrap : total;
                catches.Add(new TrapCatch(count, caught, Math.Max(0, count.OccurredOn.DayNumber - since.DayNumber)));
                onTrap = total;
            }

            if (count.NewTrap)
                onTrap = 0;

            since = count.OccurredOn;
        }

        return catches;
    }

    /// <summary>
    /// The latest treatment or check, which is what the next one is counted from. A trap count
    /// never moves a treatment along, but on a case being watched a count is a look for pests
    /// like any other, so it counts as the weekly check. Only putting a new trap up doesn't.
    /// </summary>
    private static PestTreatment? LastFor(PestCase item, IEnumerable<PestTreatment> treatments) =>
        TreatmentsFor(treatments, item.Id).FirstOrDefault(t =>
            !t.Skipped && (t.Kind != PestTreatmentKind.TrapCount
            || (item.Status == PestCaseStatus.Monitoring && t.OnTrap is not null)));

    private static bool Covers(PestCase item, Plant plant, Places places) => item.Scope switch
    {
        PestScope.Everywhere => true,
        PestScope.Room => places.IsIn(plant.PlaceId, item.PlaceId),
        _ => item.PlantIds.Contains(plant.Id)
    };
}
