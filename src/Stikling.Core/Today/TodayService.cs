using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Problems;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Settings;
using Stikling.Core.Timeline;

namespace Stikling.Core.Today;

/// <summary>Reads everything the to-do sections on Today are worked out from, once.</summary>
public sealed class TodayService(
    IPlantRepository plants,
    IPropagationRepository propagations,
    ITimelineRepository entries,
    IPhotoRepository photos,
    IPestCaseRepository pestCases,
    IPestTreatmentRepository pestTreatments,
    ICareLogRepository careLogs,
    IPropagationCareLogRepository propagationCareLogs,
    IPotRepository pots,
    ICareIntervalRepository careIntervals,
    IFeedRepository feeds,
    SettingsService settings,
    PutOffService putOffs,
    GrowLightService lights,
    AwayService away,
    OwnCareKindService ownKinds,
    ProblemService problems,
    RoomService rooms,
    TimeProvider time)
{
    /// <param name="days">How many days from today the list on the day back is read for.</param>
    public async Task<TodayData> LoadAsync(int days = 1)
    {
        var today = time.Today();
        var stretches = await away.GetAllAsync();

        // The list on the day back reads the timeline from the day back, so each distinct day back is read once
        var backTimelines = new Dictionary<DateOnly, IReadOnlyList<TimelineEntry>>();
        for (var i = 0; i < Math.Max(days, 1); i++)
        {
            if (AwayTime.BackDay(stretches, today.AddDays(i)) is { } backDay && !backTimelines.ContainsKey(backDay))
                backTimelines[backDay] = await BackTimeline.LoadAsync(entries, backDay, time);
        }

        return new TodayData
        {
            Plants = await plants.GetAllAsync(),
            Propagations = await propagations.GetAllAsync(),
            Settings = await settings.GetAsync(),
            PutOffs = await putOffs.GetAsync(today),
            Latest = await entries.GetLatestPerSubjectAsync(),
            // Logging care on a propagation is looking at it too
            PropagationCareDays = PropagationCareService.NewestDays(await propagationCareLogs.GetAllAsync()),
            Problems = await problems.AllAsync(),
            ProblemUpdates = await problems.AllUpdatesAsync(),
            Places = await rooms.GetPlacesAsync(),
            CareIntervals = await careIntervals.GetAllAsync(),
            CareLogs = await careLogs.GetAllAsync(),
            OwnCareKinds = await ownKinds.GetAllAsync(),
            Feeds = await feeds.GetAllAsync(),
            NewestPhotos = await photos.GetNewestPerSubjectAsync(),
            Stretches = stretches,
            LastActivity = await entries.GetLastActivityBeforeAsync(time.MomentAt(today.ToDateTime(TimeOnly.MinValue))),
            PlantChanges = await entries.GetPlantChangesAsync(),
            Lights = await lights.GetAllAsync(),
            PotsById = (await pots.GetAllAsync()).ToDictionary(p => p.Id),
            PestCases = await pestCases.GetAllAsync(),
            PestTreatments = await pestTreatments.GetAllAsync(),
            BackTimelines = backTimelines
        };
    }
}
