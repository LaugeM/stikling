using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class ToxicityTextTests
{
    private static PlantCare Care(Toxicity? cats, Toxicity? dogs, Toxicity? people = null) =>
        new() { Name = "Test", Cats = cats, Dogs = dogs, People = people };

    [Theory]
    [InlineData(Toxicity.Toxic, Toxicity.Toxic, "Toxic to cats and dogs")]
    [InlineData(Toxicity.NonToxic, Toxicity.NonToxic, "Not toxic to cats and dogs")]
    [InlineData(Toxicity.Toxic, Toxicity.NonToxic, "Toxic to cats, not to dogs")]
    [InlineData(Toxicity.NonToxic, Toxicity.Toxic, "Toxic to dogs, not to cats")]
    [InlineData(Toxicity.Toxic, null, "Toxic to cats")]
    [InlineData(Toxicity.NonToxic, null, "Not toxic to cats")]
    [InlineData(null, Toxicity.Toxic, "Toxic to dogs")]
    [InlineData(null, Toxicity.NonToxic, "Not toxic to dogs")]
    [InlineData(null, null, "Nothing known about pets")]
    public void Pets_reads_as_the_sources_say(Toxicity? cats, Toxicity? dogs, string expected)
    {
        Assert.Equal(expected, ToxicityText.Pets(Care(cats, dogs)));
    }

    [Fact]
    public void A_plant_with_no_entry_says_nothing_is_known()
    {
        Assert.Equal("Nothing known about pets", ToxicityText.Pets(null));
        Assert.Null(ToxicityText.People(null));
    }

    [Fact]
    public void People_is_null_when_unknown()
    {
        Assert.Equal("Toxic to people", ToxicityText.People(Care(null, null, Toxicity.Toxic)));
        Assert.Equal("Not toxic to people", ToxicityText.People(Care(null, null, Toxicity.NonToxic)));
        Assert.Null(ToxicityText.People(Care(Toxicity.Toxic, Toxicity.Toxic)));
    }
}
