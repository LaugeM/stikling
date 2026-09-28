using Stikling.Core.Models;
using Stikling.Core.Rooms;

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
public sealed record PestCaseView(
    PestCase Case,
    string? Room,
    IReadOnlyList<Plant> Plants,
    PestTreatment? Last,
    DateOnly? NextDue,
    int? DaysUntilDue)
{
    public bool IsDue => DaysUntilDue is <= 0;

    /// <summary>What's due is a look for pests rather than a treatment.</summary>
    public bool IsCheck => Case.Status == PestCaseStatus.Monitoring;
}

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
    /// the last treatment plus the case's interval, otherwise the day the case started plus the
    /// interval. A case being watched is due a check a week after the last treatment, check or
    /// trap count instead. A resolved case is never due.
    /// </summary>
    public static DateOnly? NextDue(PestCase item, IEnumerable<PestTreatment> treatments)
    {
        if (!item.IsOpen)
            return null;

        var last = LastFor(item, treatments);
        var since = last?.OccurredOn ?? item.StartedOn;

        if (item.Status == PestCaseStatus.Monitoring)
            return since.AddDays(CheckEveryDays);

        return last?.NextDueOn ?? since.AddDays(item.IntervalDays);
    }

    /// <summary>Everything a screen needs about one case.</summary>
    public static PestCaseView Describe(
        PestCase item,
        IEnumerable<Plant> plants,
        IEnumerable<PestTreatment> treatments,
        Places places,
        DateOnly today)
    {
        var next = NextDue(item, treatments);
        return new PestCaseView(
            item,
            item.Scope == PestScope.Room ? places.NameOf(item.PlaceId) : null,
            PlantsIn(item, plants, places),
            LastFor(item, treatments),
            next,
            next is { } due ? due.DayNumber - today.DayNumber : null);
    }

    /// <summary>Every case described, newest first, so the list screen is one call.</summary>
    public static IReadOnlyList<PestCaseView> Describe(
        IEnumerable<PestCase> cases,
        IEnumerable<Plant> plants,
        IEnumerable<PestTreatment> treatments,
        Places places,
        DateOnly today)
    {
        var all = plants.ToList();
        var logged = treatments.ToList();

        return cases
            .Where(c => !c.IsDeleted)
            .Select(c => Describe(c, all, logged, places, today))
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
    public static PestCase Start(DateOnly today) => new() { StartedOn = today };

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
        var last = TreatmentsFor(treatments, item.Id).FirstOrDefault(t => t.Kind == PestTreatmentKind.Treated);

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
            t.Kind != PestTreatmentKind.TrapCount
            || (item.Status == PestCaseStatus.Monitoring && t.OnTrap is not null));

    private static bool Covers(PestCase item, Plant plant, Places places) => item.Scope switch
    {
        PestScope.Everywhere => true,
        PestScope.Room => places.IsIn(plant.PlaceId, item.PlaceId),
        _ => item.PlantIds.Contains(plant.Id)
    };
}
