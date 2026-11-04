using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PhotoDestinationsTests
{
    private static Plant APlant(string nickname, Action<Plant>? change = null)
    {
        var plant = new Plant { Nickname = nickname };
        change?.Invoke(plant);
        return plant;
    }

    private static Propagation APropagation(string nickname, Action<Propagation>? change = null)
    {
        var propagation = new Propagation { Nickname = nickname };
        change?.Invoke(propagation);
        return propagation;
    }

    private static string[] Names(IEnumerable<Entity> subjects) =>
        [.. subjects.Select(s => s is Plant p ? p.DisplayName : ((Propagation)s).ListName)];

    [Fact]
    public void Plants_and_propagations_come_together_sorted_by_name()
    {
        var found = PhotoDestinations.Pick(
            [APlant("Zebra"), APlant("fern")],
            [APropagation("Monstera cutting")],
            null);

        Assert.Equal(["fern", "Monstera cutting", "Zebra"], Names(found));
    }

    [Fact]
    public void Plants_that_are_gone_or_deleted_and_propagations_that_are_finished_are_left_out()
    {
        var found = PhotoDestinations.Pick(
            [
                APlant("Kept"),
                APlant("Died", p => p.Status = PlantStatus.Died),
                APlant("Sold", p => p.Status = PlantStatus.Sold),
                APlant("Removed", p => p.DeletedAt = DateTimeOffset.UtcNow),
                APlant("Elsewhere", p => p.LivesElsewhere = true)
            ],
            [
                APropagation("Going"),
                APropagation("Rooted", p => p.Stage = PropagationStage.Rooted),
                APropagation("Done", p => p.Stage = PropagationStage.Done),
                APropagation("Failed", p => p.Stage = PropagationStage.Failed),
                APropagation("Removed too", p => p.DeletedAt = DateTimeOffset.UtcNow)
            ],
            null);

        Assert.Equal(["Going", "Kept", "Rooted"], Names(found));
    }

    [Fact]
    public void Every_word_has_to_match_a_name_ignoring_capitals_and_accents()
    {
        var thai = APlant("Thai", p => { p.Genus = "Monstera"; p.Species = "deliciosa"; p.Cultivar = "Thai Constellation"; });
        var other = APlant("Café", p => p.Genus = "Monstera");

        Assert.Equal(["Thai"], Names(PhotoDestinations.Pick([thai, other], [], "THAI monstera")));
        Assert.Equal(["Café"], Names(PhotoDestinations.Pick([thai, other], [], "cafe")));
        Assert.Empty(PhotoDestinations.Pick([thai, other], [], "thai pothos"));
    }

    [Fact]
    public void A_search_finds_propagations_by_their_botanical_names()
    {
        var cutting = APropagation("Mine", p => { p.Genus = "Epipremnum"; p.Species = "aureum"; });

        Assert.Equal(["Mine"], Names(PhotoDestinations.Pick([APlant("Other")], [cutting], "aureum")));
    }

    [Fact]
    public void A_blank_search_lists_everything()
    {
        Assert.Equal(2, PhotoDestinations.Pick([APlant("A"), APlant("B")], [], "   ").Count);
    }
}
