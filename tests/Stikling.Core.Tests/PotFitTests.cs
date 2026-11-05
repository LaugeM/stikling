using Stikling.Core.Models;
using Stikling.Core.Pots;

namespace Stikling.Core.Tests;

public class PotFitTests
{
    private static Pot Outer(decimal? top, decimal? rimMm = null, decimal? bottom = null, decimal? height = null, string name = "Outer") =>
        new() { Name = name, Group = PotGroup.Outer, TopCm = top, RimMm = rimMm, BottomCm = bottom, HeightCm = height };

    private static PotUse Use(Pot pot, int inUse = 0) => new(pot, inUse, 0);

    [Fact]
    public void Fits_when_the_nursery_is_narrower_than_the_opening()
    {
        var result = PotFits.Check(NurserySize.Width(12), Outer(15, rimMm: 10));

        Assert.Equal(PotFitKind.GoesIn, result.Kind);
        Assert.Equal(1m, result.SpareCm); // opening is 15 - 2
        Assert.False(result.Tight);
        Assert.False(result.OpeningIsOutside);
    }

    [Fact]
    public void Exactly_the_opening_goes_in_and_is_tight()
    {
        var result = PotFits.Check(NurserySize.Width(13), Outer(15, rimMm: 10));

        Assert.Equal(PotFitKind.GoesIn, result.Kind);
        Assert.Equal(0m, result.SpareCm);
        Assert.True(result.Tight);
    }

    [Fact]
    public void Half_a_centimetre_to_spare_is_not_tight()
    {
        Assert.False(PotFits.Check(NurserySize.Width(12.5m), Outer(13)).Tight);
        Assert.True(PotFits.Check(NurserySize.Width(12.6m), Outer(13)).Tight);
    }

    [Fact]
    public void Without_a_rim_on_the_outer_the_outside_width_is_used()
    {
        var result = PotFits.Check(NurserySize.Width(14), Outer(15));

        Assert.Equal(PotFitKind.GoesIn, result.Kind);
        Assert.True(result.OpeningIsOutside);
    }

    [Fact]
    public void A_wider_nursery_with_a_rim_hangs_when_below_the_rim_fits()
    {
        var nursery = new NurserySize(14, null, null, 10); // 12 below the rim
        var result = PotFits.Check(nursery, Outer(15, rimMm: 10)); // opening 13

        Assert.Equal(PotFitKind.HangsOnRim, result.Kind);
        Assert.Equal(1m, result.SpareCm);
        Assert.False(result.Tight);
    }

    [Fact]
    public void A_thin_wall_entered_as_the_rim_has_nothing_to_hang_from()
    {
        var nursery = new NurserySize(13.3m, null, null, 2); // 12.9 inside the wall
        var result = PotFits.Check(nursery, Outer(13, rimMm: null));

        Assert.Equal(PotFitKind.TooNarrow, result.Kind);
    }

    [Fact]
    public void A_rim_that_still_does_not_fit_is_too_narrow()
    {
        var nursery = new NurserySize(16, null, null, 10); // 14 below the rim
        var result = PotFits.Check(nursery, Outer(15, rimMm: 10));

        Assert.Equal(PotFitKind.TooNarrow, result.Kind);
        Assert.Equal(0m, result.SpareCm);
        Assert.False(result.Tight);
    }

    [Fact]
    public void A_wider_nursery_without_a_rim_is_too_narrow()
    {
        Assert.Equal(PotFitKind.TooNarrow, PotFits.Check(NurserySize.Width(14), Outer(13)).Kind);
    }

    [Fact]
    public void A_nursery_rim_counts_even_when_the_outer_has_none()
    {
        var nursery = new NurserySize(14, null, null, 10); // 12 below the rim
        var result = PotFits.Check(nursery, Outer(13));

        Assert.Equal(PotFitKind.HangsOnRim, result.Kind);
        Assert.True(result.OpeningIsOutside);
    }

    [Fact]
    public void A_rim_on_the_outer_makes_the_opening_smaller_than_its_outside()
    {
        Assert.Equal(PotFitKind.GoesIn, PotFits.Check(NurserySize.Width(14), Outer(15)).Kind);
        Assert.Equal(PotFitKind.TooNarrow, PotFits.Check(NurserySize.Width(14), Outer(15, rimMm: 10)).Kind);
    }

    [Fact]
    public void Taller_is_the_difference_when_both_heights_are_known()
    {
        var nursery = new NurserySize(10, null, 14, null);

        Assert.Equal(3m, PotFits.Check(nursery, Outer(12, height: 11)).TallerByCm);
        Assert.Null(PotFits.Check(nursery, Outer(12, height: 14)).TallerByCm);
        Assert.Null(PotFits.Check(nursery, Outer(12, height: 20)).TallerByCm);
        Assert.Null(PotFits.Check(nursery, Outer(12)).TallerByCm);
        Assert.Null(PotFits.Check(NurserySize.Width(10), Outer(12, height: 5)).TallerByCm);
    }

    [Fact]
    public void Taller_is_not_given_for_a_too_narrow_pot()
    {
        var nursery = new NurserySize(20, null, 14, null);

        Assert.Null(PotFits.Check(nursery, Outer(12, height: 5)).TallerByCm);
    }

    [Fact]
    public void Taller_is_given_when_it_hangs_on_the_rim()
    {
        var nursery = new NurserySize(14, null, 14, 10);
        var result = PotFits.Check(nursery, Outer(15, rimMm: 10, height: 10));

        Assert.Equal(PotFitKind.HangsOnRim, result.Kind);
        Assert.Equal(4m, result.TallerByCm);
    }

    [Fact]
    public void The_bottom_is_tighter_when_it_is_wider_than_the_outer_floor_less_its_rim()
    {
        var nursery = new NurserySize(12, 9, null, null);

        Assert.True(PotFits.Check(nursery, Outer(14, rimMm: 10, bottom: 10)).BottomTighter); // 10 - 2 = 8, less than 9
        Assert.False(PotFits.Check(nursery, Outer(14, rimMm: 10, bottom: 11)).BottomTighter); // 11 - 2 = 9, not wider
        Assert.False(PotFits.Check(nursery, Outer(14, bottom: 9)).BottomTighter);
        Assert.True(PotFits.Check(nursery, Outer(14, bottom: 8)).BottomTighter);
    }

    [Fact]
    public void The_bottom_needs_both_sizes_and_a_pot_that_goes_in()
    {
        Assert.False(PotFits.Check(new NurserySize(12, null, null, null), Outer(14, bottom: 5)).BottomTighter);
        Assert.False(PotFits.Check(new NurserySize(12, 9, null, null), Outer(14)).BottomTighter);
        Assert.False(PotFits.Check(new NurserySize(14, 12, null, 10), Outer(15, rimMm: 10, bottom: 5)).BottomTighter); // hangs
    }

    [Fact]
    public void Of_a_pot_needs_a_width()
    {
        Assert.Null(NurserySize.Of(new Pot { Group = PotGroup.Inner }));

        var size = NurserySize.Of(new Pot { Group = PotGroup.Inner, TopCm = 12, BottomCm = 8, HeightCm = 11, RimMm = 6 });
        Assert.Equal(new NurserySize(12, 8, 11, 6), size);
    }

    [Fact]
    public void List_orders_by_kind_then_least_spare_then_widest_first_for_too_narrow()
    {
        var nursery = new NurserySize(14, null, null, 10); // 12 below the rim
        var roomy = Outer(20, name: "Roomy");
        var snug = Outer(14.2m, name: "Snug");
        var rimmed = Outer(15, rimMm: 10, name: "Rimmed"); // opening 13: hangs, spare 1
        var rimmedTight = Outer(14, rimMm: 10, name: "RimmedTight"); // opening 12: hangs, spare 0
        var small = Outer(8, name: "Small");
        var smaller = Outer(5, name: "Smaller");

        var list = PotFits.List(nursery, new[] { smaller, roomy, rimmed, small, snug, rimmedTight }.Select(p => Use(p)));

        Assert.Equal(["Snug", "Roomy", "RimmedTight", "Rimmed", "Small", "Smaller"], list.Select(r => r.Outer.Pot.Name));
        Assert.Equal(
            [PotFitKind.GoesIn, PotFitKind.GoesIn, PotFitKind.HangsOnRim, PotFitKind.HangsOnRim, PotFitKind.TooNarrow, PotFitKind.TooNarrow],
            list.Select(r => r.Kind));
    }

    [Fact]
    public void List_has_only_free_outer_pots_that_are_not_deleted_and_have_a_size()
    {
        var free = Outer(20, name: "Free");
        var taken = Outer(20, name: "Taken");
        var deleted = Outer(20, name: "Deleted");
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        var noSize = Outer(null, name: "NoSize");
        var inner = new Pot { Name = "Inner", Group = PotGroup.Inner, TopCm = 10 };
        var twoFree = Outer(18, name: "OneOfTwo");
        twoFree.Owned = 2;

        var list = PotFits.List(NurserySize.Width(10), new[]
        {
            Use(free), Use(taken, inUse: 1), Use(deleted), Use(noSize), Use(inner), Use(twoFree, inUse: 1)
        });

        Assert.Equal(["OneOfTwo", "Free"], list.Select(r => r.Outer.Pot.Name));
    }

    [Fact]
    public void Without_a_size_counts_only_free_outer_pots_with_no_width()
    {
        var deleted = Outer(null, name: "Deleted");
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        var count = PotFits.CountWithoutSize(new[]
        {
            Use(Outer(null)),
            Use(Outer(null), inUse: 1),
            Use(deleted),
            Use(Outer(12)),
            Use(new Pot { Group = PotGroup.Inner })
        });

        Assert.Equal(1, count);
    }
}
