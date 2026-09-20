using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Pests;

/// <summary>A case and what the screens need to know about it.</summary>
/// <param name="Plants">The plants the case covers right now.</param>
/// <param name="Last">The most recent treatment, or null when there hasn't been one.</param>
/// <param name="NextDue">When the next treatment is due, or null for a resolved case.</param>
/// <param name="DaysUntilDue">Negative when it's overdue, 0 when it's due today.</param>
public sealed record PestCaseView(
    PestCase Case,
    IReadOnlyList<Plant> Plants,
    PestTreatment? Last,
    DateOnly? NextDue,
    int? DaysUntilDue)
{
    public bool IsDue => DaysUntilDue is <= 0;
}

/// <summary>
/// Reading pest cases back: which plants a case covers, and when the next treatment is due.
/// The writing side is plain repository calls, so everything worth testing lives here.
/// </summary>
public static class PestService
{
    /// <summary>
    /// The plants a case covers. Everywhere and Room are worked out from the plants as they
    /// are now, so a plant moved into the room mid-outbreak is covered from then on.
    /// Plants that are no longer in the collection are left out either way.
    /// </summary>
    public static IReadOnlyList<Plant> PlantsIn(PestCase item, IEnumerable<Plant> plants)
    {
        var here = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active);

        var covered = item.Scope switch
        {
            PestScope.Everywhere => here,
            PestScope.Room => here.Where(p => RoomName.IsIn(p.Location, item.Room)),
            _ => here.Where(p => item.PlantIds.Contains(p.Id))
        };

        return covered
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The open cases covering one plant, so its card and page can say so.</summary>
    public static IReadOnlyList<PestCase> CasesFor(Plant plant, IEnumerable<PestCase> cases) =>
        cases
            .Where(c => !c.IsDeleted && c.IsOpen && Covers(c, plant))
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
    /// interval. A resolved case is never due.
    /// </summary>
    public static DateOnly? NextDue(PestCase item, IEnumerable<PestTreatment> treatments)
    {
        if (!item.IsOpen)
            return null;

        var last = TreatmentsFor(treatments, item.Id).FirstOrDefault();
        if (last is null)
            return item.StartedOn.AddDays(item.IntervalDays);

        return last.NextDueOn ?? last.OccurredOn.AddDays(item.IntervalDays);
    }

    /// <summary>Everything a screen needs about one case.</summary>
    public static PestCaseView Describe(
        PestCase item,
        IEnumerable<Plant> plants,
        IEnumerable<PestTreatment> treatments,
        DateOnly today)
    {
        var next = NextDue(item, treatments);
        return new PestCaseView(
            item,
            PlantsIn(item, plants),
            TreatmentsFor(treatments, item.Id).FirstOrDefault(),
            next,
            next is { } due ? due.DayNumber - today.DayNumber : null);
    }

    /// <summary>Every case described, newest first, so the list screen is one call.</summary>
    public static IReadOnlyList<PestCaseView> Describe(
        IEnumerable<PestCase> cases,
        IEnumerable<Plant> plants,
        IEnumerable<PestTreatment> treatments,
        DateOnly today)
    {
        var all = plants.ToList();
        var logged = treatments.ToList();

        return cases
            .Where(c => !c.IsDeleted)
            .Select(c => Describe(c, all, logged, today))
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

    /// <summary>A blank treatment for a case, dated today and carrying the last one's recipe forward.</summary>
    public static PestTreatment StartTreatment(PestCase item, IEnumerable<PestTreatment> treatments, DateOnly today) =>
        new()
        {
            CaseId = item.Id,
            OccurredOn = today,
            // The same spray is usually used again, so it's offered rather than retyped
            What = TreatmentsFor(treatments, item.Id).FirstOrDefault()?.What
        };

    private static bool Covers(PestCase item, Plant plant) => item.Scope switch
    {
        PestScope.Everywhere => true,
        PestScope.Room => RoomName.IsIn(plant.Location, item.Room),
        _ => item.PlantIds.Contains(plant.Id)
    };
}
