using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Today;

/// <summary>Reads the timeline entries the list of what to look at first on the day back needs.</summary>
public static class BackTimeline
{
    /// <summary>
    /// The plant changes (for telling when plants were dormant), the entries since the day back
    /// (for what has been looked over) and the newest entry of each subject (for how long since a
    /// cutting in water was looked at), each once.
    /// </summary>
    public static async Task<IReadOnlyList<TimelineEntry>> LoadAsync(ITimelineRepository entries, DateOnly backDay, TimeProvider time)
    {
        var since = await entries.GetSinceAsync(time.MomentAt(backDay.ToDateTime(TimeOnly.MinValue)));
        var latest = await entries.GetLatestPerSubjectAsync();
        var changes = await entries.GetPlantChangesAsync();
        return [.. changes.Concat(since).Concat(latest.Values).DistinctBy(e => e.Id)];
    }
}
