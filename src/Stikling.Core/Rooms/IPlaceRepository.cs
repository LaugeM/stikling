using Stikling.Core.Models;

namespace Stikling.Core.Rooms;

public interface IPlaceRepository
{
    /// <summary>
    /// Every room and spot, with the ones merged into another kept so that whatever still points
    /// at them can be followed to where they went.
    /// </summary>
    Task<Places> GetPlacesAsync();

    Task SaveAsync(Place place);

    Task DeleteAsync(Guid id);
}
