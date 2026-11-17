using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class RoundOrderTests
{
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Place living = new() { Name = "Living room", CreatedAt = Created };
    private readonly Place sill;
    private readonly Place shelf;
    private readonly List<Plant> plants = [];
    private readonly List<Propagation> propagations = [];

    public RoundOrderTests()
    {
        sill = new Place { Name = "Windowsill", RoomId = living.Id, CreatedAt = Created.AddDays(1) };
        shelf = new Place { Name = "Shelf", RoomId = living.Id, CreatedAt = Created.AddDays(2) };
    }

    private Plant Plant(string name, Place? place = null, PlantStatus status = PlantStatus.Active)
    {
        var plant = new Plant { Nickname = name, PlaceId = (place ?? living).Id, Status = status };
        plants.Add(plant);
        return plant;
    }

    private Propagation Cutting(string name, Place? place = null)
    {
        var cutting = new Propagation { Nickname = name, PlaceId = (place ?? living).Id };
        propagations.Add(cutting);
        return cutting;
    }

    private IEnumerable<string> Round(Place scope, params Guid[] order)
    {
        living.RoundOrder = order.Length > 0 ? [.. order] : null;
        return GoingRound.ForPlace(scope.Id, plants, propagations, [], [], null, [], new Places([living, sill, shelf]), [],
                Hemisphere.Northern, Time, wateringOn: false)
            .Select(c => c.Name);
    }

    [Fact]
    public void Without_an_order_the_round_is_the_usual_one()
    {
        Plant("B"); Plant("A"); Plant("C", sill); Cutting("X");

        Assert.Equal(["A", "B", "C", "X"], Round(living));
    }

    [Fact]
    public void Plants_and_propagations_follow_one_mixed_order()
    {
        var a = Plant("A"); var b = Plant("B"); var x = Cutting("X"); var c = Plant("C", sill);

        Assert.Equal(["X", "C", "B", "A"], Round(living, x.Id, c.Id, b.Id, a.Id));
    }

    [Fact]
    public void Ids_that_are_gone_or_not_active_are_skipped()
    {
        var a = Plant("A"); var gone = Plant("Gone", status: PlantStatus.Died); var b = Plant("B");

        Assert.Equal(["B", "A"], Round(living, Guid.NewGuid(), gone.Id, b.Id, a.Id));
    }

    [Fact]
    public void A_card_not_in_the_order_goes_after_the_last_card_in_the_same_place()
    {
        var a = Plant("A", sill); var b = Plant("B"); var c = Plant("C", sill); var d = Plant("D", shelf);
        Plant("New on sill", sill);

        // The ordered cards are C, B, A, D, so the new one follows A, the last on the windowsill
        Assert.Equal(["C", "B", "A", "New on sill", "D"], Round(living, c.Id, b.Id, a.Id, d.Id));
    }

    [Fact]
    public void A_card_in_a_place_with_no_ordered_card_goes_last()
    {
        var a = Plant("A"); var b = Plant("B");
        Plant("Lone", shelf);

        Assert.Equal(["B", "A", "Lone"], Round(living, b.Id, a.Id));
    }

    [Fact]
    public void Several_new_cards_in_one_place_keep_their_usual_order_after_it()
    {
        var a = Plant("A"); var b = Plant("B", sill);
        Plant("N2", sill); Plant("N1", sill);

        Assert.Equal(["A", "B", "N1", "N2"], Round(living, a.Id, b.Id));
    }

    [Fact]
    public void A_spots_round_uses_the_order_of_its_room()
    {
        var a = Plant("A", sill); var b = Plant("B", sill); var c = Plant("C");

        Assert.Equal(["B", "A"], Round(sill, c.Id, b.Id, a.Id));
    }

    [Fact]
    public void An_order_does_not_change_what_is_in_the_round()
    {
        var a = Plant("A"); Plant("B"); Plant("C", sill);

        Assert.Equal(3, Round(living, a.Id).Count());
    }
}
