using Stikling.Core.Import;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class ImportValuesTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static string? Date(string text, DateOrder order = DateOrder.DayFirst) =>
        ImportValues.ReadDate(text, order, Today)?.ToString();

    [Theory]
    [InlineData("2024", "2024")]
    [InlineData("2024-04", "2024-04")]
    [InlineData("2024-04-03", "2024-04-03")]
    [InlineData("2024-04-03T10:30:00", "2024-04-03")]
    [InlineData("2024-04-03 10:30", "2024-04-03")]
    [InlineData("3/4/2024 08:15", "2024-04-03")]
    [InlineData("3/4/2024", "2024-04-03")]
    [InlineData("03.04.2024", "2024-04-03")]
    [InlineData("3-4-2024", "2024-04-03")]
    [InlineData("3.4.2024.", "2024-04-03")]
    [InlineData("2024/04/03", "2024-04-03")]
    [InlineData("04/2024", "2024-04")]
    [InlineData("4.2024", "2024-04")]
    [InlineData("2024/04", "2024-04")]
    [InlineData("April 2024", "2024-04")]
    [InlineData("apr. 2024", "2024-04")]
    [InlineData("3 april 2024", "2024-04-03")]
    [InlineData("3rd April 2024", "2024-04-03")]
    [InlineData("april 3, 2024", "2024-04-03")]
    [InlineData("3. maj 2024", "2024-05-03")]
    [InlineData("okt 2023", "2023-10")]
    [InlineData("Sept 2023", "2023-09")]
    [InlineData("3-Apr-24", "2024-04-03")]
    [InlineData("marts 2022", "2022-03")]
    public void Dates_in_the_forms_a_spreadsheet_gives(string text, string expected) =>
        Assert.Equal(expected, Date(text));

    [Fact]
    public void The_order_decides_which_part_is_the_day()
    {
        Assert.Equal("2024-04-03", Date("03/04/2024", DateOrder.DayFirst));
        Assert.Equal("2024-03-04", Date("03/04/2024", DateOrder.MonthFirst));
        Assert.Equal("2024-04-13", Date("13/04/2024", DateOrder.DayFirst));
        Assert.Equal("2024-04-13", Date("4/13/2024", DateOrder.MonthFirst));
        Assert.Equal("2024-04-03", Date("2024-04-03", DateOrder.MonthFirst));
    }

    [Theory]
    [InlineData("31/02/2024")]
    [InlineData("31/4/2024")]
    [InlineData("32/1/2024")]
    [InlineData("1/13/2024")]
    [InlineData("13/2024")]
    [InlineData("3 april")]
    [InlineData("april")]
    [InlineData("3 smarch 2024")]
    [InlineData("soon")]
    [InlineData("0000")]
    [InlineData("")]
    public void Text_that_isnt_a_date(string text) => Assert.Null(Date(text));

    [Fact]
    public void A_day_that_only_exists_the_other_way_round_isnt_a_date()
    {
        Assert.Null(Date("2/31/2024", DateOrder.MonthFirst));
        Assert.Null(Date("25/12/2024", DateOrder.MonthFirst));
    }

    [Fact]
    public void A_leap_day_is_a_date_only_in_a_leap_year()
    {
        Assert.Equal("2024-02-29", Date("29/02/2024"));
        Assert.Null(Date("29/02/2023"));
    }

    [Theory]
    [InlineData("3/4/24", "2024-04-03")]
    [InlineData("3/4/99", "1999-04-03")]
    [InlineData("3/4/26", "2026-04-03")]
    [InlineData("3/4/27", "1927-04-03")]
    public void A_two_digit_year_is_this_century_unless_that_is_still_to_come(string text, string expected) =>
        Assert.Equal(expected, Date(text));

    [Theory]
    [InlineData("150 kr", 150)]
    [InlineData("150", 150)]
    [InlineData("kr. 150", 150)]
    [InlineData("$9.99", 9.99)]
    [InlineData("12,50", 12.50)]
    [InlineData("12.5", 12.5)]
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1,234.50", 1234.50)]
    [InlineData("1,234", 1234)]
    [InlineData("1.234", 1234)]
    [InlineData("1,234,567", 1234567)]
    [InlineData("1 234,50 kr", 1234.50)]
    [InlineData("0", 0)]
    public void Prices(string text, double expected) =>
        Assert.Equal((decimal)expected, ImportValues.ReadPrice(text));

    [Theory]
    [InlineData("-5")]
    [InlineData("kr -5")]
    [InlineData("lots")]
    [InlineData("10-15")]
    [InlineData("5 for 10")]
    [InlineData("")]
    public void Prices_that_cant_be_read(string text) => Assert.Null(ImportValues.ReadPrice(text));

    [Fact]
    public void Tags_are_split_on_semicolons_commas_and_bars_and_tidied()
    {
        Assert.Equal(["Rare", "for swap", "Variegated"], ImportValues.ReadTags("Rare; for  swap, rare | Variegated,"));
        Assert.Empty(ImportValues.ReadTags(""));
    }

    [Theory]
    [InlineData("Active", PlantStatus.Active)]
    [InlineData("In collection", PlantStatus.Active)]
    [InlineData("alive", PlantStatus.Active)]
    [InlineData("i samling", PlantStatus.Active)]
    [InlineData("Died", PlantStatus.Died)]
    [InlineData("død", PlantStatus.Died)]
    [InlineData("Given away", PlantStatus.GivenAway)]
    [InlineData("GivenAway", PlantStatus.GivenAway)]
    [InlineData("given-away", PlantStatus.GivenAway)]
    [InlineData("bortgivet", PlantStatus.GivenAway)]
    [InlineData("Sold", PlantStatus.Sold)]
    [InlineData("SOLGT", PlantStatus.Sold)]
    public void Status_words(string text, PlantStatus expected) => Assert.Equal(expected, ImportValues.ReadStatus(text));

    [Theory]
    [InlineData("Purchased", PlantOrigin.Purchased)]
    [InlineData("bought", PlantOrigin.Purchased)]
    [InlineData("Propagated", PlantOrigin.Propagated)]
    [InlineData("cutting", PlantOrigin.Propagated)]
    [InlineData("Grown from seed", PlantOrigin.GrownFromSeed)]
    [InlineData("GrownFromSeed", PlantOrigin.GrownFromSeed)]
    [InlineData("frø", PlantOrigin.GrownFromSeed)]
    [InlineData("Gift", PlantOrigin.Gift)]
    [InlineData("present", PlantOrigin.Gift)]
    [InlineData("Swap", PlantOrigin.Swap)]
    [InlineData("byttet", PlantOrigin.Swap)]
    [InlineData("Unknown", PlantOrigin.Unknown)]
    [InlineData("ukendt", PlantOrigin.Unknown)]
    public void Origin_words(string text, PlantOrigin expected) => Assert.Equal(expected, ImportValues.ReadOrigin(text));

    [Theory]
    [InlineData("Soil", GrowingMedium.Soil)]
    [InlineData("potting mix", GrowingMedium.Soil)]
    [InlineData("muld", GrowingMedium.Soil)]
    [InlineData("Orchid bark", GrowingMedium.OrchidBark)]
    [InlineData("bark", GrowingMedium.OrchidBark)]
    [InlineData("LECA", GrowingMedium.Leca)]
    [InlineData("lecakugler", GrowingMedium.Leca)]
    [InlineData("PON", GrowingMedium.Pon)]
    [InlineData("Sphagnum moss", GrowingMedium.Sphagnum)]
    [InlineData("mos", GrowingMedium.Sphagnum)]
    [InlineData("vand", GrowingMedium.Water)]
    [InlineData("Corm riser", GrowingMedium.CormRiser)]
    [InlineData("andet", GrowingMedium.Other)]
    [InlineData("Perlite", GrowingMedium.Perlite)]
    public void Medium_words(string text, GrowingMedium expected) => Assert.Equal(expected, ImportValues.ReadMedium(text));

    [Fact]
    public void Words_nobody_knows_are_null()
    {
        Assert.Null(ImportValues.ReadStatus("zombie"));
        Assert.Null(ImportValues.ReadOrigin("found"));
        Assert.Null(ImportValues.ReadMedium("hydro"));
        Assert.Null(ImportValues.ReadMedium(null));
    }

    [Fact]
    public void The_date_order_comes_from_a_date_that_can_only_be_read_one_way()
    {
        Assert.Equal(DateOrder.DayFirst, DateOrderGuess.FromValues(["13/04/2024", "01/02/2024", ""]));
        Assert.Equal(DateOrder.MonthFirst, DateOrderGuess.FromValues(["01/02/2024", "04/13/2024 10:00"]));
    }

    [Fact]
    public void The_date_order_is_unknown_when_the_dates_say_nothing_or_disagree()
    {
        Assert.Null(DateOrderGuess.FromValues(["03/04/2024", "5/6/2024"]));
        Assert.Null(DateOrderGuess.FromValues(["13/04/2024", "04/13/2024"]));
        Assert.Null(DateOrderGuess.FromValues(["2024-04-13", "April 2024", "2024"]));
        Assert.Null(DateOrderGuess.FromValues([]));
    }

    [Fact]
    public void The_device_language_gives_the_order_to_start_with()
    {
        Assert.Equal(DateOrder.MonthFirst, DateOrderGuess.ForLanguages(["en-US", "da"]));
        Assert.Equal(DateOrder.DayFirst, DateOrderGuess.ForLanguages(["da-DK", "en-US"]));
        Assert.Equal(DateOrder.DayFirst, DateOrderGuess.ForLanguages(["en-GB"]));
        Assert.Equal(DateOrder.DayFirst, DateOrderGuess.ForLanguages([]));
    }
}
