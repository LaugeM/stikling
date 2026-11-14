using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>A propagation whose water is due to be changed.</summary>
/// <param name="LastChanged">The day of the newest change, or null when the water hasn't been changed yet.</param>
/// <param name="DaysSince">Days since that change, or since it was started when there is none.</param>
public sealed record WaterChangeDue(Propagation Propagation, DateOnly? LastChanged, int DaysSince);

/// <summary>The propagations whose water is due to be changed, from the number of days each one was set to.</summary>
public static class WaterChangeReminders
{
    /// <summary>
    /// Active propagations that aren't dormant, are in a medium that takes a water change, have a
    /// number of days set and have gone that long since the water was changed (or since they were
    /// started). The one that has waited longest comes first.
    /// </summary>
    public static IReadOnlyList<WaterChangeDue> Due(
        IEnumerable<Propagation> propagations, IEnumerable<PropagationCareLog> careLogs, DateOnly today)
    {
        var changes = WaterChangeLog.DaysByBatch(careLogs);
        var due = new List<WaterChangeDue>();
        foreach (var p in propagations)
        {
            if (p.IsDeleted || !p.IsActive || p.IsDormant || !WaterChangeLog.Takes(p.Medium) || p.WaterChangeDays is not { } every)
                continue;
            DateOnly? last = changes.TryGetValue(p.Id, out var days) ? days.Max() : null;
            var since = today.DayNumber - (last ?? p.StartedOn).DayNumber;
            if (since >= every)
                due.Add(new WaterChangeDue(p, last, since));
        }
        return due
            .OrderByDescending(d => d.DaysSince - d.Propagation.WaterChangeDays!.Value)
            .ThenBy(d => d.Propagation.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
