using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class MemoriesTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    private static DateOnly Day(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    private static Photo Taken(Plant plant, DateOnly day, int hour = 12) => new()
    {
        SubjectType = SubjectType.Plant,
        SubjectId = plant.Id,
        TakenAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero)
    };

    private static Memory? Pick(IEnumerable<Plant> plants, IEnumerable<Photo> photos, DateOnly? today = null) =>
        Memories.Pick(plants, photos, today ?? Today, Day);

    [Fact]
    public void A_photo_from_a_year_ago_today_is_shown_with_the_newest()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var old = Taken(plant, new DateOnly(2025, 10, 9));
        var newest = Taken(plant, new DateOnly(2026, 10, 1));

        var memory = Pick([plant], [old, newest]);

        Assert.NotNull(memory);
        Assert.Equal(old.Id, memory.Old.Id);
        Assert.Equal(newest.Id, memory.Newest.Id);
        Assert.Equal(12, memory.Months);
        Assert.Equal("A year ago today", memory.Heading);
    }

    [Fact]
    public void Several_years_are_counted()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var memory = Pick([plant], [Taken(plant, new DateOnly(2024, 10, 9)), Taken(plant, new DateOnly(2026, 9, 1))]);

        Assert.Equal("2 years ago today", memory!.Heading);
    }

    [Fact]
    public void Six_months_ago_is_the_fallback()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var memory = Pick([plant], [Taken(plant, new DateOnly(2026, 4, 9)), Taken(plant, new DateOnly(2026, 10, 1))]);

        Assert.Equal(6, memory!.Months);
        Assert.Equal("Six months ago today", memory.Heading);
    }

    [Fact]
    public void Six_months_back_from_the_end_of_a_month_lands_where_AddMonths_does()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var today = new DateOnly(2026, 8, 31);
        var memory = Pick([plant], [Taken(plant, new DateOnly(2026, 2, 28)), Taken(plant, new DateOnly(2026, 8, 1))], today);

        Assert.Equal(6, memory!.Months);
    }

    [Fact]
    public void The_longest_gap_wins_then_the_most_photos_then_the_name()
    {
        var half = new Plant { Nickname = "Half" };
        var year = new Plant { Nickname = "Year" };
        var three = new Plant { Nickname = "Three" };
        var photos = new[]
        {
            Taken(half, new DateOnly(2026, 4, 9)), Taken(half, new DateOnly(2026, 10, 1)),
            Taken(year, new DateOnly(2025, 10, 9)), Taken(year, new DateOnly(2026, 10, 1)),
            Taken(three, new DateOnly(2023, 10, 9)), Taken(three, new DateOnly(2026, 10, 1)),
        };

        Assert.Equal("Three", Pick([half, year, three], photos)!.Plant.Nickname);
        Assert.Equal("Year", Pick([half, year], photos)!.Plant.Nickname);

        var busy = new Plant { Nickname = "Busy" };
        var calm = new Plant { Nickname = "Calm" };
        var tied = new[]
        {
            Taken(calm, new DateOnly(2025, 10, 9)), Taken(calm, new DateOnly(2026, 10, 1)),
            Taken(busy, new DateOnly(2025, 10, 9)), Taken(busy, new DateOnly(2026, 5, 1)), Taken(busy, new DateOnly(2026, 10, 1)),
        };
        Assert.Equal("Busy", Pick([calm, busy], tied)!.Plant.Nickname);

        var a = new Plant { Nickname = "Alpha" };
        var b = new Plant { Nickname = "Beta" };
        var same = new[]
        {
            Taken(b, new DateOnly(2025, 10, 9)), Taken(b, new DateOnly(2026, 10, 1)),
            Taken(a, new DateOnly(2025, 10, 9)), Taken(a, new DateOnly(2026, 10, 1)),
        };
        Assert.Equal("Alpha", Pick([b, a], same)!.Plant.Nickname);
    }

    [Fact]
    public void Gone_plants_and_deleted_photos_are_skipped()
    {
        var gone = new Plant { Nickname = "Gone", Status = PlantStatus.Died };
        var deleted = new Plant { Nickname = "Deleted", DeletedAt = DateTimeOffset.UtcNow };
        var lost = new Plant { Nickname = "Lost" };
        var oldLost = Taken(lost, new DateOnly(2025, 10, 9));
        oldLost.DeletedAt = DateTimeOffset.UtcNow;

        var photos = new[]
        {
            Taken(gone, new DateOnly(2025, 10, 9)), Taken(gone, new DateOnly(2026, 10, 1)),
            Taken(deleted, new DateOnly(2025, 10, 9)), Taken(deleted, new DateOnly(2026, 10, 1)),
            oldLost, Taken(lost, new DateOnly(2026, 10, 1)),
        };

        Assert.Null(Pick([gone, deleted, lost], photos));
    }

    [Fact]
    public void A_newest_photo_less_than_30_days_later_is_skipped()
    {
        var plant = new Plant { Nickname = "Monstera" };
        Assert.Null(Pick([plant], [Taken(plant, new DateOnly(2025, 10, 9)), Taken(plant, new DateOnly(2025, 11, 7))]));
        Assert.Null(Pick([plant], [Taken(plant, new DateOnly(2025, 10, 9))]));
        Assert.NotNull(Pick([plant], [Taken(plant, new DateOnly(2025, 10, 9)), Taken(plant, new DateOnly(2025, 11, 8))]));
    }

    [Fact]
    public void The_earliest_photo_of_the_old_day_is_used()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var morning = Taken(plant, new DateOnly(2025, 10, 9), 8);
        var evening = Taken(plant, new DateOnly(2025, 10, 9), 20);

        var memory = Pick([plant], [evening, morning, Taken(plant, new DateOnly(2026, 10, 1))]);

        Assert.Equal(morning.Id, memory!.Old.Id);
    }

    [Fact]
    public void The_29th_of_February_maps_to_the_28th_in_other_years()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var today = new DateOnly(2026, 2, 28);
        var memory = Pick([plant], [Taken(plant, new DateOnly(2025, 2, 28)), Taken(plant, new DateOnly(2026, 2, 1))], today);
        Assert.Equal(12, memory!.Months);

        var leap = new DateOnly(2028, 2, 29);
        var other = Pick([plant], [Taken(plant, new DateOnly(2024, 2, 29)), Taken(plant, new DateOnly(2028, 1, 1))], leap);
        Assert.Equal(48, other!.Months);
    }
}
