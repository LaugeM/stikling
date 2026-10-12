using Stikling.Core.Away;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class AwayTests
{
    private static readonly DateOnly D = new(2026, 12, 20);

    private static AwayStretch Stretch(int from, int until, AwayKind kind = AwayKind.Away, bool deleted = false) =>
        new()
        {
            From = D.AddDays(from), Until = D.AddDays(until), Kind = kind,
            DeletedAt = deleted ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) : null
        };

    [Fact]
    public void A_stretch_is_on_from_its_first_day_up_to_the_day_back()
    {
        var stretch = Stretch(0, 5);

        Assert.False(stretch.IsOn(D.AddDays(-1)));
        Assert.True(stretch.IsOn(D));
        Assert.True(stretch.IsOn(D.AddDays(4)));
        Assert.False(stretch.IsOn(D.AddDays(5)));
    }

    [Fact]
    public void A_stretch_needs_a_day_back_after_the_first_day()
    {
        Assert.Empty(Stretch(0, 1).Validate());
        Assert.NotEmpty(Stretch(2, 2).Validate());
        Assert.NotEmpty(Stretch(3, 1).Validate());
    }

    [Fact]
    public void Overlapping_and_touching_stretches_are_one_span()
    {
        var spans = AwayTime.Spans([Stretch(10, 12), Stretch(0, 5), Stretch(3, 8), Stretch(8, 9), Stretch(20, 22)]);

        Assert.Equal(3, spans.Count);
        Assert.Equal((D, D.AddDays(9)), (spans[0].From, spans[0].Until));
        Assert.Equal(3, spans[0].Stretches.Count);
        Assert.Equal((D.AddDays(10), D.AddDays(12)), (spans[1].From, spans[1].Until));
        Assert.Equal(D.AddDays(22), spans[2].Until);
    }

    [Fact]
    public void A_stretch_inside_another_does_not_shorten_the_span()
    {
        var span = Assert.Single(AwayTime.Spans([Stretch(0, 10), Stretch(2, 4)]));

        Assert.Equal(D.AddDays(10), span.Until);
    }

    [Fact]
    public void A_span_is_away_when_any_stretch_in_it_is_away()
    {
        Assert.Equal(AwayKind.Away, Assert.Single(AwayTime.Spans([Stretch(0, 5, AwayKind.Quiet), Stretch(5, 8)])).Kind);
        Assert.Equal(AwayKind.Quiet, Assert.Single(AwayTime.Spans([Stretch(0, 5, AwayKind.Quiet), Stretch(5, 8, AwayKind.Quiet)])).Kind);
    }

    [Fact]
    public void Deleted_stretches_are_ignored()
    {
        Assert.Empty(AwayTime.Spans([Stretch(0, 5, deleted: true)]));
        Assert.Null(AwayTime.Current([Stretch(0, 5, deleted: true)], D));

        // A deleted one in between doesn't join the two around it
        Assert.Equal(2, AwayTime.Spans([Stretch(0, 5), Stretch(4, 9, deleted: true), Stretch(8, 10)]).Count);
    }

    [Fact]
    public void Current_is_the_span_from_its_first_day_to_the_day_before_the_day_back()
    {
        var stretches = new[] { Stretch(0, 5), Stretch(5, 7) };

        Assert.Null(AwayTime.Current(stretches, D.AddDays(-1)));
        Assert.Equal(D, AwayTime.Current(stretches, D)?.From);
        Assert.Equal(D.AddDays(7), AwayTime.Current(stretches, D.AddDays(6))?.Until);
        Assert.Null(AwayTime.Current(stretches, D.AddDays(7)));
        Assert.True(AwayTime.IsOn(stretches, D.AddDays(5)));
        Assert.False(AwayTime.IsOn(stretches, D.AddDays(7)));
    }

    [Fact]
    public void Upcoming_is_the_next_away_span_starting_within_a_week()
    {
        var stretches = new[] { Stretch(3, 9), Stretch(30, 33) };

        Assert.Equal(D.AddDays(3), AwayTime.Upcoming(stretches, D)?.From);
        Assert.Equal(D.AddDays(3), AwayTime.Upcoming(stretches, D.AddDays(-4))?.From);

        // Not before the week is out, not once it has started
        Assert.Null(AwayTime.Upcoming(stretches, D.AddDays(-5)));
        Assert.Null(AwayTime.Upcoming(stretches, D.AddDays(3)));
        Assert.Equal(D.AddDays(30), AwayTime.Upcoming(stretches, D.AddDays(25))?.From);
    }

    [Fact]
    public void A_quiet_stretch_is_never_upcoming()
    {
        Assert.Null(AwayTime.Upcoming([Stretch(3, 9, AwayKind.Quiet)], D));
    }

    [Fact]
    public void The_day_back_shows_for_the_day_itself_and_the_two_after()
    {
        var stretches = new[] { Stretch(0, 10) };
        var back = D.AddDays(10);

        Assert.Null(AwayTime.BackDay(stretches, D.AddDays(9)));
        Assert.Equal(back, AwayTime.BackDay(stretches, back));
        Assert.Equal(back, AwayTime.BackDay(stretches, back.AddDays(1)));
        Assert.Equal(back, AwayTime.BackDay(stretches, back.AddDays(2)));
        Assert.Null(AwayTime.BackDay(stretches, back.AddDays(3)));
    }

    [Fact]
    public void There_is_no_day_back_while_a_later_stretch_is_on()
    {
        // Back on day 10, away again from day 12 after a day at home
        var stretches = new[] { Stretch(0, 10), Stretch(12, 20) };

        Assert.Equal(D.AddDays(10), AwayTime.BackDay(stretches, D.AddDays(11)));
        Assert.Null(AwayTime.BackDay(stretches, D.AddDays(12)));
    }

    [Fact]
    public void The_day_back_is_the_latest_one_and_quiet_stretches_have_one_too()
    {
        var stretches = new[] { Stretch(0, 5), Stretch(7, 9, AwayKind.Quiet) };

        Assert.Equal(D.AddDays(9), AwayTime.BackDay(stretches, D.AddDays(10)));
        Assert.Equal(D.AddDays(5), AwayTime.BackDay(stretches, D.AddDays(6)));
        Assert.Null(AwayTime.BackDay([Stretch(0, 5, deleted: true)], D.AddDays(5)));
    }

    [Fact]
    public async Task Im_back_ends_the_stretch_today()
    {
        var repository = new FakeAwayStretchRepository();
        var service = new AwayService(repository);
        var stretch = Stretch(0, 14);
        await service.SaveAsync(stretch);

        await service.ImBackAsync(D.AddDays(5));

        Assert.Equal(D.AddDays(5), stretch.Until);
        Assert.False(stretch.IsDeleted);
        Assert.Null(AwayTime.Current(await service.GetAllAsync(), D.AddDays(5)));
    }

    [Fact]
    public async Task Im_back_shortens_every_stretch_in_the_span_and_deletes_one_left_empty()
    {
        var repository = new FakeAwayStretchRepository();
        var service = new AwayService(repository);
        var first = Stretch(0, 7);
        var second = Stretch(7, 14);
        var later = Stretch(14, 20, AwayKind.Quiet);
        var apart = Stretch(40, 45);
        foreach (var stretch in new[] { first, second, later, apart })
            await service.SaveAsync(stretch);

        await service.ImBackAsync(D.AddDays(3));

        Assert.Equal(D.AddDays(3), first.Until);
        Assert.True(second.IsDeleted);
        Assert.True(later.IsDeleted);
        Assert.False(apart.IsDeleted);
        Assert.Equal(D.AddDays(45), apart.Until);
    }

    [Fact]
    public async Task Im_back_on_the_first_day_deletes_the_stretch_and_does_nothing_when_none_is_on()
    {
        var repository = new FakeAwayStretchRepository();
        var service = new AwayService(repository);
        var stretch = Stretch(0, 14);
        await service.SaveAsync(stretch);

        await service.ImBackAsync(D.AddDays(-2));
        Assert.Equal(D.AddDays(14), stretch.Until);

        await service.ImBackAsync(D);
        Assert.True(stretch.IsDeleted);
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task The_sitter_sheet_reads_as_the_defaults_until_it_is_saved()
    {
        var repository = new FakeSitterSheetRepository();
        var service = new SitterSheetService(repository);

        var sheet = await service.GetAsync();
        Assert.Equal(SitterSheet.SheetId, sheet.Id);
        Assert.Equal(VisitPattern.TwiceAWeek, sheet.Visits);
        Assert.Empty(repository.Sheets);

        sheet.Layout = SheetLayout.Visits;
        await service.SaveAsync(sheet);

        Assert.Equal(SheetLayout.Visits, (await service.GetAsync()).Layout);
        Assert.Single(repository.Sheets);
    }

    [Fact]
    public void Visits_twice_a_week_follow_the_example_from_the_brief()
    {
        var from = new DateOnly(2025, 12, 20);
        var until = new DateOnly(2026, 1, 4);

        Assert.Equal(
            [new DateOnly(2025, 12, 23), new DateOnly(2025, 12, 27), new DateOnly(2025, 12, 30), new DateOnly(2026, 1, 3)],
            SitterVisits.Dates(VisitPattern.TwiceAWeek, from, until));
    }

    [Theory]
    [InlineData(VisitPattern.EveryDay, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
    [InlineData(VisitPattern.EveryTwoDays, new[] { 2, 4, 6, 8 })]
    [InlineData(VisitPattern.EveryThreeDays, new[] { 3, 6, 9 })]
    [InlineData(VisitPattern.TwiceAWeek, new[] { 3, 7 })]
    [InlineData(VisitPattern.Weekly, new[] { 7 })]
    [InlineData(VisitPattern.None, new int[0])]
    public void Visits_fall_between_leaving_and_coming_back(VisitPattern pattern, int[] offsets)
    {
        var dates = SitterVisits.Dates(pattern, D, D.AddDays(10));

        Assert.Equal(offsets.Select(D.AddDays), dates);
    }

    [Fact]
    public void A_visit_on_the_day_back_is_not_one_and_a_short_trip_has_none()
    {
        Assert.Equal([D.AddDays(1)], SitterVisits.Dates(VisitPattern.EveryDay, D, D.AddDays(2)));
        Assert.Empty(SitterVisits.Dates(VisitPattern.Weekly, D, D.AddDays(7)));
        Assert.Empty(SitterVisits.Dates(VisitPattern.EveryDay, D, D));
    }
}
