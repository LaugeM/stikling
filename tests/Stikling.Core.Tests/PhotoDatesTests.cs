using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PhotoDatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 15, 0, 0, TimeSpan.FromHours(2));
    private static readonly TimeProvider Time = new FixedTime(Now, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset FileDate = Now.AddMinutes(-1);

    [Theory]
    [InlineData("IMG_20230514_102231.jpg", "2023-05-14", "10:22:31")]
    [InlineData("PXL_20230514_102231123.jpg", "2023-05-14", "10:22:31")]
    [InlineData("20230514_102231.jpg", "2023-05-14", "10:22:31")]
    [InlineData("Screenshot_20230514-102231_Chrome.jpg", "2023-05-14", "10:22:31")]
    [InlineData("Screenshot_2023-05-14-10-22-31-123_com.whatsapp.jpg", "2023-05-14", "10:22:31")]
    [InlineData("Screenshot 2023-05-14 at 10.22.31.png", "2023-05-14", "10:22:31")]
    [InlineData("signal-2023-05-14-102231.jpg", "2023-05-14", "10:22:31")]
    [InlineData("IMG-20230514-WA0003.jpg", "2023-05-14", null)]
    [InlineData("photo_2023-05-14.jpg", "2023-05-14", null)]
    public void Dates_are_read_from_the_names_phones_give_photos(string name, string day, string? time)
    {
        var found = PhotoDates.FromFileName(name);

        Assert.NotNull(found);
        Assert.Equal(DateOnly.Parse(day), found.Value.Day);
        Assert.Equal(time is null ? null : TimeOnly.Parse(time), found.Value.Time);
    }

    [Theory]
    [InlineData("IMG_0023.JPG")]
    [InlineData("1695123456789.jpg")]
    [InlineData("image.png")]
    [InlineData("IMG_20231399_120000.jpg")]
    [InlineData("")]
    [InlineData(null)]
    public void Names_without_a_real_date_give_nothing(string? name) =>
        Assert.Null(PhotoDates.FromFileName(name));

    [Fact]
    public void A_time_that_isnt_one_still_gives_the_day() =>
        Assert.Equal((new DateOnly(2023, 5, 14), (TimeOnly?)null), PhotoDates.FromFileName("IMG_20230514_996622.jpg"));

    [Theory]
    [InlineData("2023:05:14 10:22:31")]
    [InlineData("2023-05-14 10:22:31")]
    [InlineData(" 2023:05:14 10:22:31 ")]
    public void Camera_dates_are_read(string text) =>
        Assert.Equal(new DateTime(2023, 5, 14, 10, 22, 31), PhotoDates.FromCamera(text));

    [Theory]
    [InlineData("0000:00:00 00:00:00")]
    [InlineData("    :  :     :  :  ")]
    [InlineData(null)]
    public void Blank_camera_dates_give_nothing(string? text) =>
        Assert.Null(PhotoDates.FromCamera(text));

    [Fact]
    public void The_camera_date_comes_first_and_keeps_its_local_time()
    {
        var (takenAt, source) = PhotoDates.Pick("2023:05:14 23:40:00", "IMG_20220101_120000.jpg", FileDate, Time);

        Assert.Equal(PhotoDateSource.Camera, source);
        Assert.Equal(new DateTimeOffset(2023, 5, 14, 23, 40, 0, TimeSpan.FromHours(2)), takenAt);
        Assert.Equal(new DateOnly(2023, 5, 14), Time.LocalDay(takenAt));
    }

    [Fact]
    public void A_winter_photo_keeps_its_time_when_added_in_summer()
    {
        var copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        var summer = new FixedTime(Now, zone: copenhagen);

        var (takenAt, _) = PhotoDates.Pick("2024:03:01 09:15:00", null, FileDate, summer);

        Assert.Equal(TimeSpan.FromHours(1), takenAt.Offset);
        Assert.Equal(new TimeOnly(9, 15), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(takenAt, copenhagen).DateTime));
    }

    [Fact]
    public void A_summer_night_photo_stays_on_its_day_when_seen_in_winter()
    {
        var copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        var summer = new FixedTime(Now, zone: copenhagen);
        var winter = new FixedTime(new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero), zone: copenhagen);

        var (takenAt, _) = PhotoDates.Pick("2025:07:01 00:30:00", null, FileDate, summer);

        Assert.Equal(new DateOnly(2025, 7, 1), winter.LocalDay(takenAt));
    }

    [Fact]
    public void Without_a_camera_date_the_file_name_is_used()
    {
        var (takenAt, source) = PhotoDates.Pick(null, "IMG_20230514_102231.jpg", FileDate, Time);

        Assert.Equal(PhotoDateSource.FileName, source);
        Assert.Equal(new DateTimeOffset(2023, 5, 14, 10, 22, 31, TimeSpan.FromHours(2)), takenAt);
    }

    [Fact]
    public void A_file_name_with_only_a_day_is_placed_at_noon()
    {
        var (takenAt, _) = PhotoDates.Pick(null, "IMG-20230514-WA0003.jpg", FileDate, Time);

        Assert.Equal(new DateTimeOffset(2023, 5, 14, 12, 0, 0, TimeSpan.FromHours(2)), takenAt);
    }

    [Fact]
    public void Without_either_the_file_date_is_used() =>
        Assert.Equal((FileDate, PhotoDateSource.File), PhotoDates.Pick(null, "image.jpg", FileDate, Time));

    [Fact]
    public void Dates_in_the_future_are_passed_over()
    {
        var (takenAt, source) = PhotoDates.Pick("2027:01:01 10:00:00", "IMG_20270101_100000.jpg", Now.AddDays(3), Time);

        Assert.Equal(PhotoDateSource.File, source);
        Assert.Equal(Now, takenAt);
    }
}

public class PhotoEntriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTime(Now);
    private static readonly Guid Subject = Guid.NewGuid();

    private static Photo TakenAt(DateTimeOffset moment) =>
        new() { SubjectType = SubjectType.Plant, SubjectId = Subject, TakenAt = moment };

    private static readonly Photo[] ThreeDays =
    [
        TakenAt(new(2024, 3, 1, 9, 0, 0, TimeSpan.Zero)),
        TakenAt(new(2025, 6, 10, 8, 0, 0, TimeSpan.Zero)),
        TakenAt(new(2024, 3, 1, 17, 0, 0, TimeSpan.Zero)),
        TakenAt(Now)
    ];

    [Fact]
    public void Photos_on_their_own_get_an_entry_per_day()
    {
        var entries = PhotoEntries.For(SubjectType.Plant, Subject, ThreeDays, Time);

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal(TimelineKind.Photo, e.Kind));

        var march = Assert.Single(entries, e => e.PhotoIds.Count == 2);
        Assert.Equal([ThreeDays[0].Id, ThreeDays[2].Id], march.PhotoIds);
        Assert.Equal(ThreeDays[2].TakenAt, march.OccurredAt);
    }

    [Fact]
    public void Photos_with_a_note_stay_together_on_the_newest_day()
    {
        var entry = Assert.Single(PhotoEntries.For(SubjectType.Plant, Subject, ThreeDays, Time, " Before and after "));

        Assert.Equal(TimelineKind.Note, entry.Kind);
        Assert.Equal("Before and after", entry.Text);
        Assert.Equal(ThreeDays.Select(p => p.Id), entry.PhotoIds);
        Assert.Equal(Now, entry.OccurredAt);
    }

    [Fact]
    public void A_blank_note_counts_as_no_note() =>
        Assert.Equal(3, PhotoEntries.For(SubjectType.Plant, Subject, ThreeDays, Time, "  ").Count);

    [Fact]
    public void The_newest_photo_is_the_cover() =>
        Assert.Same(ThreeDays[3], PhotoEntries.Cover(ThreeDays));
}
