using System.Text.Json;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class LooseDateTests
{
    // How it reads

    [Fact]
    public void A_year_on_its_own_reads_as_the_year() => Assert.Equal("2024", LooseDate.Of(2024).Text());

    [Fact]
    public void A_month_reads_without_a_day() => Assert.Equal("June 2024", LooseDate.Of(2024, 6).Text());

    [Fact]
    public void A_whole_day_reads_like_every_other_date_in_the_app() =>
        Assert.Equal("12 Jun 2024", LooseDate.Of(new DateOnly(2024, 6, 12)).Text());

    [Fact]
    public void How_much_is_known_comes_from_the_date_itself()
    {
        Assert.Equal(DatePrecision.Year, LooseDate.Of(2024).Precision);
        Assert.Equal(DatePrecision.Month, LooseDate.Of(2024, 6).Precision);
        Assert.Equal(DatePrecision.Day, LooseDate.Of(new DateOnly(2024, 6, 12)).Precision);
    }

    [Fact]
    public void Start_is_the_first_day_of_what_is_known()
    {
        Assert.Equal(new DateOnly(2024, 1, 1), LooseDate.Of(2024).Start);
        Assert.Equal(new DateOnly(2024, 6, 1), LooseDate.Of(2024, 6).Start);
        Assert.Equal(new DateOnly(2024, 6, 12), LooseDate.Of(new DateOnly(2024, 6, 12)).Start);
    }

    // Storage

    [Theory]
    [InlineData("2024")]
    [InlineData("2024-06")]
    [InlineData("2024-06-12")]
    public void It_comes_back_out_of_storage_the_way_it_went_in(string stored)
    {
        Assert.True(LooseDate.TryParse(stored, out var date));

        var json = JsonSerializer.Serialize(date);

        Assert.Equal($"\"{stored}\"", json);
        Assert.Equal(date, JsonSerializer.Deserialize<LooseDate>(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("24")]
    [InlineData("2024-13")]
    [InlineData("2024-06-31")]
    [InlineData("June 2024")]
    public void Nonsense_is_not_a_date(string? text) => Assert.False(LooseDate.TryParse(text, out _));

    [Fact]
    public void Storing_something_that_is_not_a_date_is_an_error() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LooseDate>("\"soon\""));

    // Changing how much you say

    [Fact]
    public void Going_coarser_drops_what_is_no_longer_shown()
    {
        var day = LooseDate.Of(new DateOnly(2024, 6, 12));

        Assert.Equal(LooseDate.Of(2024, 6), day.To(DatePrecision.Month));
        Assert.Equal(LooseDate.Of(2024), day.To(DatePrecision.Year));
    }

    [Fact]
    public void Going_finer_falls_back_to_the_first()
    {
        var year = LooseDate.Of(2024);

        Assert.Equal(LooseDate.Of(2024, 1), year.To(DatePrecision.Month));
        Assert.Equal(LooseDate.Of(new DateOnly(2024, 1, 1)), year.To(DatePrecision.Day));
    }

    [Fact]
    public void Two_dates_that_say_the_same_thing_are_the_same_date() =>
        Assert.Equal(LooseDate.Of(2024, 6), LooseDate.Of(2024, 6));

    [Fact]
    public void A_year_is_not_the_same_as_the_first_of_January()
    {
        Assert.NotEqual(LooseDate.Of(2024), LooseDate.Of(new DateOnly(2024, 1, 1)));
    }
}
