using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class UnsureNameMatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(NameQualifier.Cf, null, true)]
    [InlineData(NameQualifier.Unknown, null, true)]
    [InlineData(NameQualifier.Cf, NameQualifier.Cf, false)]
    [InlineData(NameQualifier.Aff, NameQualifier.Cf, false)]
    [InlineData(null, null, false)]
    [InlineData(null, NameQualifier.Cf, false)]
    public void Only_losing_the_qualifier_counts_as_identified(NameQualifier? before, NameQualifier? after, bool expected) =>
        Assert.Equal(expected, PlantNames.WasIdentified(before, after));

    [Fact]
    public void A_hidden_species_is_ignored_but_kept()
    {
        var sp = new Plant { Genus = "Hoya", Species = "carnosa", NameQualifier = NameQualifier.Sp };
        var lost = new Plant { Genus = "Hoya", Species = "carnosa", NameQualifier = NameQualifier.Unknown };
        Assert.Null(sp.KnownSpecies);
        Assert.Null(lost.KnownSpecies);
        Assert.Equal("carnosa", sp.Species);
        Assert.Equal("carnosa", new Plant { Species = "carnosa", NameQualifier = NameQualifier.Cf }.KnownSpecies);
    }

    [Fact]
    public void Different_unsure_names_are_not_flagged_as_the_same_plant()
    {
        var sumatra = new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp, NameNote = "Sumatra" };
        var borneo = new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp, NameNote = "Borneo" };
        var again = new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp, NameNote = " sumatra " };
        Assert.Empty(PlantMatches.SameName(borneo, [sumatra]));
        Assert.Single(PlantMatches.SameName(again, [sumatra]));
    }

    [Fact]
    public void A_cf_name_does_not_match_a_sure_one()
    {
        var sure = new Plant { Genus = "Hoya", Species = "carnosa" };
        var cf = new Plant { Genus = "Hoya", Species = "carnosa", NameQualifier = NameQualifier.Cf };
        Assert.Empty(PlantMatches.SameName(cf, [sure]));
    }

    [Fact]
    public void The_csv_species_column_leaves_out_a_hidden_species()
    {
        var plant = new Plant { Genus = "Hoya", Species = "carnosa", NameQualifier = NameQualifier.Sp, CreatedAt = Now };
        var line = CsvExport.Plants([plant], Places.None).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[1];
        Assert.DoesNotContain("carnosa,", line);
    }
}
