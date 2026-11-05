using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Pots;
using Stikling.Core.Text;

namespace Stikling.Core.Tests;

/// <summary>The statics for the date style, the clock and the length unit are shared, so these tests run alone and put them back.</summary>
[CollectionDefinition("Display choices", DisableParallelization = true)]
public sealed class DisplayChoicesCollection;

[Collection("Display choices")]
public sealed class DisplayChoiceTests : IDisposable
{
    public DisplayChoiceTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        TextCulture.Style = DateStyle.DayFirst;
        TextCulture.Clock = ClockStyle.TwentyFourHour;
        Lengths.Unit = LengthUnit.Centimetres;
    }

    [Theory]
    [InlineData("d MMM yyyy")]
    [InlineData("dddd d MMMM")]
    [InlineData("dddd 'den' d. MMMM")]
    public void Day_first_leaves_the_pattern_alone(string pattern) =>
        Assert.Equal(pattern, TextCulture.Styled(pattern, DateStyle.DayFirst));

    [Theory]
    [InlineData("d MMM yyyy", "MMM d, yyyy")]
    [InlineData("d. MMM yyyy", "MMM d, yyyy")]
    [InlineData("d MMMM yyyy", "MMMM d, yyyy")]
    [InlineData("d MMM", "MMM d")]
    [InlineData("d. MMMM", "MMMM d")]
    [InlineData("ddd d MMM", "ddd, MMM d")]
    [InlineData("ddd d. MMM", "ddd, MMM d")]
    [InlineData("dddd d MMMM", "dddd, MMMM d")]
    [InlineData("dddd 'den' d. MMMM", "dddd, MMMM d")]
    [InlineData("dddd d. MMM", "dddd, MMM d")]
    public void Month_first_puts_the_month_before_the_day(string pattern, string expected) =>
        Assert.Equal(expected, TextCulture.Styled(pattern, DateStyle.MonthFirst));

    [Theory]
    [InlineData("d MMM yyyy", "yyyy-MM-dd")]
    [InlineData("d. MMM yyyy", "yyyy-MM-dd")]
    [InlineData("d MMMM yyyy", "yyyy-MM-dd")]
    [InlineData("d MMM", "MM-dd")]
    [InlineData("ddd d MMM", "ddd MM-dd")]
    [InlineData("ddd d. MMM", "ddd MM-dd")]
    [InlineData("dddd d MMM yyyy", "dddd yyyy-MM-dd")]
    public void Year_first_writes_numbers(string pattern, string expected) =>
        Assert.Equal(expected, TextCulture.Styled(pattern, DateStyle.YearFirst));

    [Theory]
    [InlineData("d MMMM")]
    [InlineData("dddd d MMMM")]
    [InlineData("dddd 'den' d. MMMM")]
    public void Year_first_leaves_a_full_month_name_without_a_year_alone(string pattern) =>
        Assert.Equal(pattern, TextCulture.Styled(pattern, DateStyle.YearFirst));

    [Theory]
    [InlineData("MMMM yyyy")]
    [InlineData("MMM yyyy")]
    [InlineData("MMMM")]
    [InlineData("MMM")]
    [InlineData("ddd")]
    [InlineData("yyyy")]
    [InlineData("ddd d")]
    [InlineData("ddd d.")]
    public void A_pattern_without_a_day_and_a_month_is_left_alone_in_every_style(string pattern)
    {
        foreach (var style in Enum.GetValues<DateStyle>())
            Assert.Equal(pattern, TextCulture.Styled(pattern, style));
    }

    [Fact]
    public void Date_writes_in_the_chosen_style()
    {
        var date = new DateOnly(2026, 11, 5);

        Assert.Equal("5 Nov 2026", TextCulture.Date(date, "d MMM yyyy"));
        TextCulture.Style = DateStyle.MonthFirst;
        Assert.Equal("Nov 5, 2026", TextCulture.Date(date, "d MMM yyyy"));
        Assert.Equal("Thursday, November 5", TextCulture.Date(date, "dddd d MMMM"));
        TextCulture.Style = DateStyle.YearFirst;
        Assert.Equal("2026-11-05", TextCulture.Date(date, "d MMM yyyy"));
        Assert.Equal("Thu 11-05", TextCulture.Date(date, "ddd d MMM"));
        Assert.Equal("Thursday 5 November", TextCulture.Date(date, "dddd d MMMM"));
    }

    [Fact]
    public void The_clock_is_24_hour_or_12_hour()
    {
        var afternoon = new TimeOnly(14, 5);

        Assert.Equal("14:05", TextCulture.TimeOf(afternoon));
        TextCulture.Clock = ClockStyle.TwelveHour;
        Assert.Equal("2:05 PM", TextCulture.TimeOf(afternoon));
        Assert.Equal("12:00 AM", TextCulture.TimeOf(new TimeOnly(0, 0)));
        Assert.Equal("2:05 PM", TextCulture.TimeOf(new DateTimeOffset(2026, 11, 5, 14, 5, 0, TimeSpan.FromHours(2))));
    }

    [Fact]
    public void Lengths_are_written_in_the_chosen_unit()
    {
        Assert.Equal("13 cm", Lengths.Cm(13));
        Assert.Equal("4 mm", Lengths.Mm(4));
        Assert.Equal("cm", Lengths.UnitName);
        Assert.Equal("mm", Lengths.SmallUnitName);

        Lengths.Unit = LengthUnit.Inches;

        Assert.Equal("5.12 in", Lengths.Cm(13));
        Assert.Equal("0.16 in", Lengths.Mm(4));
        Assert.Equal("4 in", Lengths.Cm(10.16m));
        Assert.Equal("in", Lengths.UnitName);
        Assert.Equal("in", Lengths.SmallUnitName);
    }

    [Fact]
    public void Typed_inches_come_back_as_exact_centimetres()
    {
        Lengths.Unit = LengthUnit.Inches;

        Assert.Equal(10.16m, Lengths.CmFromInput(4));
        Assert.Equal(11.43m, Lengths.CmFromInput(4.5m));
        Assert.Equal(25.4m, Lengths.MmFromInput(1));
        Assert.Equal(4, Lengths.CmToInput(10.16m));
        Assert.Equal(5.12m, Lengths.CmToInput(13));
        Assert.Equal(0.16m, Lengths.MmToInput(4));
    }

    [Fact]
    public void In_centimetres_the_input_numbers_are_unchanged()
    {
        Assert.Equal(13.5m, Lengths.CmToInput(13.5m));
        Assert.Equal(13.5m, Lengths.CmFromInput(13.5m));
        Assert.Equal(4, Lengths.MmToInput(4));
        Assert.Equal(4, Lengths.MmFromInput(4));
    }

    [Fact]
    public void A_pot_is_named_in_the_chosen_unit_but_its_stored_name_stays_in_centimetres()
    {
        var pot = new Pot { Name = "Clear nursery pot", TopCm = 13 };

        Assert.Equal("Clear nursery pot, 13 cm", pot.DisplayName);
        Lengths.Unit = LengthUnit.Inches;

        Assert.Equal("Clear nursery pot, 5.12 in", pot.DisplayName);
        Assert.Equal("Clear nursery pot, 13 cm", pot.StoredName);
    }

    [Fact]
    public void The_size_change_note_follows_the_unit()
    {
        var from = new Pot { TopCm = 10.16m };
        var to = new Pot { TopCm = 15.24m };

        Lengths.Unit = LengthUnit.Inches;

        Assert.StartsWith("From 4 in to 6 in across the top.", PotSizeChange.Between(from, to)!.Text);
    }

    [Fact]
    public void Older_settings_without_the_choices_read_as_null()
    {
        var settings = JsonSerializer.Deserialize<UserSettings>("""{"Theme":"Dark"}""")!;

        Assert.Null(settings.Lengths);
        Assert.Null(settings.Dates);
        Assert.Null(settings.Clock);
    }

    [Fact]
    public void A_choice_this_version_does_not_know_reads_as_null()
    {
        var settings = JsonSerializer.Deserialize<UserSettings>("""{"Lengths":"Furlongs","Dates":"Roman","Clock":"Sundial"}""")!;

        Assert.Null(settings.Lengths);
        Assert.Null(settings.Dates);
        Assert.Null(settings.Clock);
    }

    [Fact]
    public void The_choices_round_trip()
    {
        var saved = new UserSettings { Lengths = LengthUnit.Inches, Dates = DateStyle.YearFirst, Clock = ClockStyle.TwelveHour };

        var json = JsonSerializer.Serialize(saved);
        var read = JsonSerializer.Deserialize<UserSettings>(json)!;

        Assert.Contains("\"Inches\"", json);
        Assert.Equal(LengthUnit.Inches, read.Lengths);
        Assert.Equal(DateStyle.YearFirst, read.Dates);
        Assert.Equal(ClockStyle.TwelveHour, read.Clock);
    }
}
