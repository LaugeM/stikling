using Stikling.Core.Calendar;
using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class CalendarFeedTests
{
    private static readonly DateOnly Today = new(2026, 4, 20);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero));
    private const string Url = "https://stikling.app/";

    private readonly List<Plant> plants = [];
    private readonly List<CareLog> logs = [];
    private readonly List<AwayStretch> stretches = [];

    private TodayData Data() => new()
    {
        Plants = plants,
        CareLogs = logs,
        PutOffs = PutOffs.Empty(),
        Stretches = stretches,
        Settings = new UserSettings { WateringReminder = true }
    };

    private Plant Flagged(string name)
    {
        var plant = new Plant { Nickname = name, Attention = new Attention("Yellow leaves", Today.AddDays(-1)) };
        plants.Add(plant);
        return plant;
    }

    private Plant Weekly(string name, DateOnly last)
    {
        var plant = new Plant { Nickname = name };
        plants.Add(plant);
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = last.AddDays(-7 * i) });
        return plant;
    }

    [Fact]
    public void A_day_gets_the_notification_text_as_its_title_and_a_line_for_each_part()
    {
        Flagged("Fern");
        Weekly("Ficus", Today.AddDays(-7));

        var day = Assert.Single(CalendarFeed.Days(Data(), Time, Url), d => d.Day == Today);

        Assert.Equal("Stikling: Fern needs attention · Check the soil of Ficus", day.Title);
        Assert.Equal("Fern needs attention\nCheck the soil of Ficus\n\nOpen Today: https://stikling.app/", day.Description);
        Assert.DoesNotContain("ater", day.Title);
    }

    [Fact]
    public void Nothing_due_gives_no_day_and_away_days_give_none()
    {
        Assert.Empty(CalendarFeed.Days(Data(), Time, Url));

        Flagged("Fern");
        stretches.Add(new AwayStretch { From = Today.AddDays(1), Until = Today.AddDays(4) });
        var days = CalendarFeed.Days(Data(), Time, Url, days: 6).Select(d => d.Day).ToList();

        Assert.Contains(Today, days);
        Assert.DoesNotContain(Today.AddDays(2), days);
        Assert.Contains(Today.AddDays(4), days);
    }

    [Fact]
    public void A_day_that_is_all_left_out_gets_no_event_and_a_mixed_day_loses_the_part()
    {
        Flagged("Fern");
        Assert.Empty(CalendarFeed.Days(Data(), Time, Url, [CalendarParts.Plants]));

        Weekly("Ficus", Today.AddDays(-7));
        var day = Assert.Single(CalendarFeed.Days(Data(), Time, Url, [CalendarParts.Plants]), d => d.Day == Today);

        Assert.Equal("Stikling: Check the soil of Ficus", day.Title);
        Assert.DoesNotContain("Fern", day.Description);
    }

    [Fact]
    public void Unknown_keys_are_ignored_and_null_shows_everything()
    {
        Flagged("Fern");

        Assert.Equal(
            CalendarFeed.Days(Data(), Time, Url),
            CalendarFeed.Days(Data(), Time, Url, ["from-the-future"]));
        Assert.NotEmpty(CalendarFeed.Days(Data(), Time, Url, null));
        Assert.NotEmpty(CalendarFeed.Days(Data(), Time, Url, []));
    }

    [Fact]
    public void The_photo_round_can_be_left_out_of_the_parts()
    {
        var plant = new Plant { Nickname = "Fern" };
        var photos = new[] { new PhotoDue(plant, "Fern", null, null), new PhotoDue(plant, "Fern", null, null) };
        var sections = new TodaySections
        {
            FlaggedNow = [new AttentionItem(plant, "Fern", new Attention("Dry", Today))],
            PhotosDue = photos,
            PhotoRoundShows = true
        };

        Assert.Equal(["Fern needs attention", "Take 2 photos"], TodayNotice.Parts(sections));
        Assert.Equal(["Fern needs attention"], TodayNotice.Parts(sections, [CalendarParts.Photos]));
        Assert.Empty(TodayNotice.Parts(new TodaySections { PhotosDue = photos, PhotoRoundShows = true }, [CalendarParts.Photos]));
    }

    [Fact]
    public void Parts_are_not_capped_but_the_text_is()
    {
        var plant = new Plant { Nickname = "Fern" };
        var sections = new TodaySections
        {
            FlaggedNow = [new AttentionItem(plant, "Fern", new Attention("Dry", Today))],
            LookAgainNow = [new Stikling.Core.Problems.LookAgain(new Problem(), plant, Today)],
            QuarantineNow = [plant],
            TransitionNow = [plant],
            RestNow = [(plant, Today)],
            PhotosDue = [new PhotoDue(plant, "Fern", null, null)],
            PhotoRoundShows = true
        };

        Assert.Equal(6, TodayNotice.Parts(sections).Count);
        Assert.EndsWith("and 2 more", TodayNotice.Text(sections));
    }

    [Fact]
    public void A_long_description_lists_twenty_lines_then_counts_the_rest()
    {
        var parts = Enumerable.Range(1, 23).Select(i => $"Part {i}").ToList();

        var lines = CalendarFeed.Describe(parts, Url).Split('\n');

        Assert.Equal("Part 20", lines[19]);
        Assert.Equal("and 3 more", lines[20]);
        Assert.Equal("", lines[21]);
        Assert.Equal("Open Today: " + Url, lines[22]);
    }

    [Fact]
    public void A_long_title_is_cut_without_splitting_a_character()
    {
        Flagged(string.Concat(Enumerable.Repeat("🌿", 120)));
        Weekly(string.Concat(Enumerable.Repeat("🌱", 120)), Today.AddDays(-7));

        var day = Assert.Single(CalendarFeed.Days(Data(), Time, Url), d => d.Day == Today);

        Assert.True(day.Title.Length <= CalendarFeed.MaxTitleLength);
        Assert.EndsWith("…", day.Title);
        Assert.False(char.IsHighSurrogate(day.Title[^2]));
        Assert.True(day.Description!.Length <= CalendarFeed.MaxDescriptionLength);
    }
}
