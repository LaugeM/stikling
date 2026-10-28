using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>A plant whose outer pot is to be emptied, with the watering the reminder comes from.</summary>
public sealed record OuterPotDue(Plant Plant, Guid CareLogId);

/// <summary>What the "Empty the outer pot" card shows, and when the next reminder still waiting becomes due.</summary>
public sealed record OuterPotReminders(IReadOnlyList<OuterPotDue> Due, DateTimeOffset? NextDueAt)
{
    public static readonly OuterPotReminders None = new([], null);

    /// <summary>How long after watering the reminder comes, when the setting hasn't been touched.</summary>
    public const int DefaultMinutes = 60;

    /// <summary>The wait in minutes, or 0 when the reminder is off.</summary>
    public static int WaitMinutes(UserSettings settings) => Math.Max(settings.EmptyOuterPotMinutes ?? DefaultMinutes, 0);

    /// <summary>
    /// Plants in the collection that stand in an outer pot without water kept in it, and whose
    /// latest watering was logged on the day it happened, is not yet ticked and is at least the
    /// wait old. Only Watered counts, not topping up or an own kind of care.
    /// </summary>
    public static OuterPotReminders Find(
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> careLogs,
        UserSettings settings,
        TimeProvider time)
    {
        var wait = WaitMinutes(settings);
        if (wait == 0)
            return None;

        var now = time.GetUtcNow();
        var latest = careLogs
            .Where(l => !l.IsDeleted && l.Kind == CareKind.Watered && l.OwnKindId is null)
            .GroupBy(l => l.PlantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreatedAt).First());

        var due = new List<OuterPotDue>();
        DateTimeOffset? next = null;
        foreach (var plant in plants.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            if (plant.IsDeleted || !plant.InCollection || plant.OuterPotId is null || plant.WaterInOuterPot)
                continue;
            if (!latest.TryGetValue(plant.Id, out var log) || log.OuterPotEmptiedAt is not null)
                continue;
            if (time.LocalDay(log.CreatedAt) != log.OccurredOn)
                continue;

            var dueAt = log.CreatedAt.AddMinutes(wait);
            if (dueAt <= now)
                due.Add(new OuterPotDue(plant, log.Id));
            else if (next is null || dueAt < next)
                next = dueAt;
        }

        return new OuterPotReminders(due, next);
    }
}
