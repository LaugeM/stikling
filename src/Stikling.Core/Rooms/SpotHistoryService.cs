using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Timeline;

namespace Stikling.Core.Rooms;

/// <summary>Reads what has lived in a room or spot for the page that shows it.</summary>
public sealed class SpotHistoryService(IPlaceRepository places, IPlantRepository plants, ITimelineRepository timeline, TimeProvider time)
{
    /// <summary>The place's name and what has stood in it. Null when the place is gone, and a place merged into another gives that one.</summary>
    public async Task<(Guid Id, string Name, IReadOnlyList<SpotPlant> Plants)?> GetAsync(Guid placeId)
    {
        var all = await places.GetPlacesAsync();
        if (all.Find(placeId) is not { } place)
            return null;

        var stood = SpotHistory.Of(place.Id, all, await plants.GetAllAsync(), await timeline.GetPlantChangesAsync(), time.LocalDay);
        return (place.Id, all.NameOf(place.Id) ?? place.Name, stood);
    }
}
