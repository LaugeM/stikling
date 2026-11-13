using static Stikling.Core.Text.Format;
using Stikling.Core.Text;
using Strings = Stikling.Web.Resources;

namespace Stikling.Web.Services;

/// <summary>Text about what stands in a room or spot, shared by Rooms and tags and the page of a room.</summary>
public static class RoomText
{
    /// <summary>"3 plants, 1 propagation", for the line under a room.</summary>
    public static string Held(int plants, int propagations) => (plants, propagations) switch
    {
        (0, 0) => Strings.Shared.RoomsEmptyPlace,
        (_, 0) => Labels.Plants(plants),
        (0, _) => Propagations(propagations),
        _ => F(Strings.Shared.RoomsPlantsAndPropagations, Labels.Plants(plants), Propagations(propagations))
    };

    private static string Propagations(int count) =>
        Plural.Of(count, Strings.Shared.RoomsPropagations_One, Strings.Shared.RoomsPropagations_Other);
}
