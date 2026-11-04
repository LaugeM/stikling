using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class PotTests
{
    [Fact]
    public void Validate_does_not_want_a_name_on_a_pot() =>
        Assert.Empty(new Pot { Group = PotGroup.Outer }.Validate());

    [Fact]
    public void Validate_wants_a_name_on_a_container()
    {
        Assert.Contains("Give the container a name, like \"Clear box with lid\".",
            new Pot { Group = PotGroup.Standalone, ForPropagation = true }.Validate());
        Assert.Contains("Give the container a name, like \"Clear box with lid\".",
            new Pot { Group = PotGroup.Standalone, ForPropagation = true, Name = "  " }.Validate());
        Assert.Empty(new Pot { Group = PotGroup.Standalone, ForPropagation = true, Name = "Box" }.Validate());
    }

    [Fact]
    public void FamilyName_is_the_trimmed_name_when_there_is_one() =>
        Assert.Equal("Blue glazed", new Pot { Name = "  Blue glazed ", Colour = PotColour.Green }.FamilyName);

    [Fact]
    public void An_unnamed_pot_is_named_from_colour_material_and_kind()
    {
        Assert.Equal("White ceramic outer pot", Unnamed(PotGroup.Outer, PotMaterial.Ceramic, PotColour.White).FamilyName);
        Assert.Equal("Clear plastic nursery pot", Unnamed(PotGroup.Inner, PotMaterial.Plastic, PotColour.Clear).FamilyName);
    }

    [Fact]
    public void An_unnamed_pot_with_no_colour_starts_with_the_material()
    {
        Assert.Equal("Terracotta pot", Unnamed(PotGroup.Standalone, PotMaterial.Terracotta, null).FamilyName);
        Assert.Equal("Plastic nursery pot", Unnamed(PotGroup.Inner, PotMaterial.Plastic, null).FamilyName);
    }

    [Fact]
    public void An_unnamed_pot_of_another_material_leaves_the_material_out()
    {
        Assert.Equal("White outer pot", Unnamed(PotGroup.Outer, PotMaterial.Other, PotColour.White).FamilyName);
        Assert.Equal("Outer pot", Unnamed(PotGroup.Outer, PotMaterial.Other, null).FamilyName);
    }

    [Fact]
    public void A_blank_name_counts_as_no_name() =>
        Assert.Equal("White ceramic outer pot", new Pot { Name = " ", Group = PotGroup.Outer, Material = PotMaterial.Ceramic, Colour = PotColour.White }.FamilyName);

    [Fact]
    public void DisplayName_puts_the_size_after_a_made_up_name()
    {
        var pot = Unnamed(PotGroup.Outer, PotMaterial.Ceramic, PotColour.White);
        pot.TopCm = 15;

        Assert.Equal("White ceramic outer pot, 15 cm", pot.DisplayName);
    }

    [Fact]
    public void A_made_up_name_is_in_Danish_with_the_Danish_word_order()
    {
        var was = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("da");
        try
        {
            Assert.Equal("Hvid yderpotte i keramik", Unnamed(PotGroup.Outer, PotMaterial.Ceramic, PotColour.White).FamilyName);
            Assert.Equal("Klar inderpotte i plastik", Unnamed(PotGroup.Inner, PotMaterial.Plastic, PotColour.Clear).FamilyName);
            Assert.Equal("Potte i terrakotta", Unnamed(PotGroup.Standalone, PotMaterial.Terracotta, null).FamilyName);
            Assert.Equal("Hvid yderpotte", Unnamed(PotGroup.Outer, PotMaterial.Other, PotColour.White).FamilyName);
            Assert.Equal("Mønstret yderpotte i glas", Unnamed(PotGroup.Outer, PotMaterial.Glass, PotColour.Patterned).FamilyName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = was;
        }
    }

    [Fact]
    public void A_colour_is_stored_as_text()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new Pot { Colour = PotColour.Blue });

        Assert.Contains("\"Colour\":\"Blue\"", json);
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<Pot>("{}")!.Colour);
    }

    [Fact]
    public void Validate_wants_at_least_one_of_it() =>
        Assert.Contains("You have to have at least one of it.", Nursery(owned: 0).Validate());

    [Fact]
    public void Validate_rejects_a_measurement_of_nothing()
    {
        var pot = Nursery();
        pot.HeightCm = 0;

        Assert.Contains("A measurement has to be more than 0.", pot.Validate());
    }

    [Fact]
    public void Validate_rejects_a_bottom_wider_than_the_top()
    {
        var pot = Nursery();
        pot.TopCm = 10;
        pot.BottomCm = 12;

        Assert.Contains("The bottom can't be wider than the top.", pot.Validate());
    }

    [Fact]
    public void A_pot_with_only_a_name_is_enough() => Assert.Empty(Nursery().Validate());

    [Fact]
    public void DisplayName_puts_the_size_after_the_name()
    {
        var pot = Nursery();
        pot.TopCm = 13;

        Assert.Equal("Clear nursery pot, 13 cm", pot.DisplayName);
    }

    [Fact]
    public void DisplayName_is_just_the_name_when_there_is_no_size() =>
        Assert.Equal("Clear nursery pot", Nursery().DisplayName);

    [Fact]
    public void A_size_keeps_its_decimal_but_loses_the_zeros()
    {
        Assert.Equal("13", Pot.Size(13m));
        Assert.Equal("5.8", Pot.Size(5.8m));
        Assert.Equal("4.2", Pot.Size(4.20m));
    }

    [Fact]
    public void The_inside_is_the_outside_less_the_rim_on_both_sides()
    {
        var pot = Nursery();
        pot.TopCm = 10;
        pot.RimMm = 6;

        Assert.Equal(8.8m, pot.InsideTopCm);
    }

    [Theory]
    [InlineData(null, 6)]
    [InlineData(10, null)]
    public void The_inside_needs_both_the_size_and_the_rim(int? top, int? rim)
    {
        var pot = Nursery();
        pot.TopCm = top;
        pot.RimMm = rim;

        Assert.Null(pot.InsideTopCm);
    }

    [Fact]
    public void Validate_rejects_a_rim_as_wide_as_the_pot()
    {
        var pot = Nursery();
        pot.TopCm = 10;
        pot.RimMm = 50;

        Assert.Contains("The rim can't take up the whole width of the pot.", pot.Validate());
    }

    [Fact]
    public void A_pot_without_a_rim_cannot_hang()
    {
        Assert.False(Nursery().HasRim);

        var rimmed = Nursery();
        rimmed.RimMm = 7;

        Assert.True(rimmed.HasRim);
    }

    [Fact]
    public void A_pot_that_is_not_self_watering_has_no_wick_or_submerged() =>
        Assert.Null(Nursery().SelfWatering);

    private static Pot Unnamed(PotGroup group, PotMaterial material, PotColour? colour) =>
        new() { Group = group, Material = material, Colour = colour };

    private static Pot Nursery(int owned = 1) =>
        new() { Name = "Clear nursery pot", Group = PotGroup.Inner, Owned = owned };
}
