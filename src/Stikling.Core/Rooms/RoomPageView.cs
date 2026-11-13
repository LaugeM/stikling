using Stikling.Core.Models;

namespace Stikling.Core.Rooms;

/// <summary>A plant on a room's page, with the spot it stands in when the page is of the room.</summary>
/// <param name="SpotName">The spot on its own, or null when it stands in the place itself.</param>
public sealed record RoomPagePlant(Plant Plant, string? SpotName);

/// <summary>A propagation on a room's page, with the spot it stands in when the page is of the room.</summary>
/// <param name="SpotName">The spot on its own, or null when it stands in the place itself.</param>
public sealed record RoomPagePropagation(Propagation Propagation, string? SpotName);

/// <summary>Everything the page of a room or spot shows.</summary>
/// <param name="Place">The place as stored. The one it was merged into when the page was asked for a merged one.</param>
/// <param name="Name">The whole name, "Living room / On top of the PC" for a spot.</param>
/// <param name="Room">The room of a spot. Null for a room.</param>
/// <param name="RoomIsOutdoors">The room around a spot is outdoors, which makes the spot outdoors too.</param>
/// <param name="Spots">The spots of a room. Empty for a spot.</param>
public sealed record RoomPageView(
    Place Place, string Name, Place? Room, bool RoomIsOutdoors,
    IReadOnlyList<RoomSpot> Spots, IReadOnlyList<RoomPagePlant> Plants, IReadOnlyList<RoomPagePropagation> Propagations);
