using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Settings;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class NoticeUploadTests
{
    // 2026-11-02 is a Monday
    private static readonly IReadOnlyList<NoticeDay> Week =
    [
        .. Enumerable.Range(0, 7).Select(i => new NoticeDay(new DateOnly(2026, 11, 2).AddDays(i), $"Day {i}"))
    ];

    private static List<DateOnly> Uploaded(UserSettings settings) =>
        [.. NoticeUpload.Days(Week, settings).Select(d => d.Day)];

    [Fact]
    public void Every_day_uploads_all_of_them()
    {
        var settings = new UserSettings { NotifyOn = NotifyDays.EveryDay, RoundDays = [Weekday.Monday] };

        var days = NoticeUpload.Days(Week, settings);

        Assert.Equal(7, days.Count);
        Assert.Equal("Day 3", days[3].Text);
        Assert.Equal(new DateOnly(2026, 11, 5), days[3].Day);
    }

    [Fact]
    public void Round_days_upload_only_those_weekdays()
    {
        var settings = new UserSettings { NotifyOn = NotifyDays.RoundDays, RoundDays = [Weekday.Wednesday, Weekday.Sunday] };

        Assert.Equal([new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 8)], Uploaded(settings));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    public void Round_days_without_any_set_upload_nothing(bool? empty)
    {
        var settings = new UserSettings { NotifyOn = NotifyDays.RoundDays, RoundDays = empty is null ? null : [] };

        Assert.Empty(NoticeUpload.Days(Week, settings));
    }

    [Fact]
    public void Picked_days_upload_only_the_picked_weekdays_and_not_the_round_days()
    {
        var settings = new UserSettings
        {
            NotifyOn = NotifyDays.Picked,
            NotifyWeekdays = [Weekday.Monday, Weekday.Friday],
            RoundDays = [Weekday.Tuesday]
        };

        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6)], Uploaded(settings));
    }

    [Fact]
    public void Picked_with_none_ticked_uploads_nothing()
    {
        Assert.Empty(NoticeUpload.Days(Week, new UserSettings { NotifyOn = NotifyDays.Picked }));
        Assert.Empty(NoticeUpload.Days(Week, new UserSettings { NotifyOn = NotifyDays.Picked, NotifyWeekdays = [] }));
    }

    [Fact]
    public void A_text_that_is_too_long_is_cut_without_splitting_an_emoji()
    {
        // The emoji takes two chars, at 398 and 399, right where the cut falls
        var text = new string('a', 398) + "🌱" + "tail";
        var days = NoticeUpload.Days([new NoticeDay(new DateOnly(2026, 11, 2), text)], new UserSettings());

        var cut = days[0].Text;
        Assert.True(cut.Length <= NoticeUpload.MaxTextLength);
        Assert.EndsWith("a…", cut, StringComparison.Ordinal);
        Assert.DoesNotContain(cut, c => char.IsSurrogate(c));
    }

    // The settings

    [Fact]
    public void Notification_settings_are_stored_as_text_and_older_settings_read_as_defaults()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(
            new UserSettings { NotifyAt = new TimeOnly(7, 45), NotifyOn = NotifyDays.Picked, NotifyWeekdays = [Weekday.Monday] }, options);

        Assert.Contains("\"notifyOn\":\"Picked\"", json);
        Assert.Contains("\"notifyWeekdays\":[\"Monday\"]", json);
        var back = JsonSerializer.Deserialize<UserSettings>(json, options)!;
        Assert.Equal(new TimeOnly(7, 45), back.NotifyAt);
        Assert.Equal(NotifyDays.Picked, back.NotifyOn);
        Assert.Equal([Weekday.Monday], back.NotifyWeekdays);

        var older = JsonSerializer.Deserialize<UserSettings>("""{"theme":"Dark"}""", options)!;
        Assert.Null(older.NotifyAt);
        Assert.Equal(NotifyDays.EveryDay, older.NotifyOn);
        Assert.Null(older.NotifyWeekdays);
    }

    [Fact]
    public async Task Turning_on_sets_eight_o_clock_once_and_keeps_a_time_already_chosen()
    {
        var repository = new FakeSettingsRepository();
        var service = new SettingsService(repository);

        Assert.Equal(new TimeOnly(8, 0), await service.EnsureNotifyAtAsync());

        await service.SetNotifyAtAsync(new TimeOnly(19, 40));
        Assert.Equal(new TimeOnly(19, 30), repository.Settings[UserSettings.SettingsId].NotifyAt);
        Assert.Equal(new TimeOnly(19, 30), await service.EnsureNotifyAtAsync());
    }

    [Fact]
    public async Task Picked_weekdays_are_saved_in_week_order_and_none_clears_them()
    {
        var repository = new FakeSettingsRepository();
        var service = new SettingsService(repository);

        await service.SetNotifyOnAsync(NotifyDays.Picked);
        await service.SetNotifyWeekdaysAsync([Weekday.Sunday, Weekday.Tuesday, Weekday.Sunday]);
        var saved = repository.Settings[UserSettings.SettingsId];
        Assert.Equal(NotifyDays.Picked, saved.NotifyOn);
        Assert.Equal([Weekday.Tuesday, Weekday.Sunday], saved.NotifyWeekdays);

        await service.SetNotifyWeekdaysAsync([]);
        Assert.Null(repository.Settings[UserSettings.SettingsId].NotifyWeekdays);
    }
}
