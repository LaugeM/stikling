using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Rooms;
using Stikling.Core.SoilMixes;
using Stikling.Core.Timeline;
using Stikling.Core.Year;

namespace Stikling.Core.Tests;

public class ReviewFixesTests
{
    private static readonly DateOnly Today = new(2026, 10, 28);
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static DateOnly Utc(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    [Fact]
    public void An_unchanged_day_in_the_past_is_still_valid()
    {
        var choice = BringBackChoice.For(Today.AddDays(-3));

        Assert.False(choice.IsValid(new DateOnly(2025, 1, 1), Today));
        Assert.True(choice.IsValid(new DateOnly(2025, 1, 1), Today, Today.AddDays(-3)));
        Assert.False(choice.IsValid(new DateOnly(2025, 1, 1), Today, Today.AddDays(-4)));
    }

    [Fact]
    public async Task Saving_a_note_with_its_unchanged_past_day_does_not_throw()
    {
        var timeline = new FakeTimelineRepository();
        var service = new TimelineService(timeline, new FakePhotoRepository(), new FixedTime(Now));
        var entry = new TimelineEntry
        {
            Kind = TimelineKind.Note, SubjectType = SubjectType.Plant, SubjectId = Guid.NewGuid(), Text = "Repot",
            ComesBackOn = Today.AddDays(-2)
        };

        await service.SetComesBackAsync(entry, Today.AddDays(-2));

        Assert.Equal(Today.AddDays(-2), entry.ComesBackOn);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetComesBackAsync(entry, Today.AddDays(-1)));
    }

    [Fact]
    public void Same_day_next_year_for_an_old_note_rolls_forward_until_it_is_ahead()
    {
        var choice = new BringBackChoice { Mode = BringBackMode.SameDayNextYear };
        var noteDay = new DateOnly(2024, 5, 14);

        Assert.Equal(new DateOnly(2027, 5, 14), choice.Resolve(noteDay, Today));
        Assert.True(choice.IsValid(noteDay, Today));
    }

    [Fact]
    public void Plants_that_live_elsewhere_are_left_out_of_the_year()
    {
        var mine = new Plant { Nickname = "Mine", CreatedAt = Now, UpdatedAt = Now };
        var theirs = new Plant
        {
            Nickname = "Theirs", LivesElsewhere = true,
            CreatedAt = new DateTimeOffset(2019, 3, 1, 0, 0, 0, TimeSpan.Zero)
        };

        Assert.Equal([2026], PlantYear.Years([mine, theirs], [], [], Today, Utc));
        var summary = PlantYear.For(2026, [mine, theirs], [], [], [], Today, Utc, "Died");
        Assert.Equal(1, summary.Added.Count);
    }

    [Fact]
    public void Plants_that_live_elsewhere_are_not_counted_in_rooms_pots_or_mixes()
    {
        var fakePlaces = new FakePlaceRepository();
        var room = fakePlaces.Add("Office");
        var pot = new Pot { Name = "Nursery", Group = PotGroup.Inner, Owned = 3 };
        var mix = new SoilMix { Name = "Chunky" };
        var theirs = new Plant
        {
            Nickname = "Theirs", LivesElsewhere = true, PlaceId = room.Id, InnerPotId = pot.Id, SoilMixId = mix.Id
        };

        Assert.Equal(0, Assert.Single(Room.List(fakePlaces.Current, [theirs], [])).Plants);
        Assert.Equal(0, Assert.Single(PotUse.List([pot], [theirs])).InUse);
        Assert.Equal(0, Assert.Single(MixUse.List([mix], [theirs])).Plants);
    }

    [Fact]
    public async Task Ticking_lives_elsewhere_keeps_quarantine_and_flags_for_when_it_is_unticked()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var plant = new Plant
        {
            Nickname = "Hoya", QuarantinedSince = Today.AddDays(-3), QuarantineDays = 14, DormantSince = Today.AddDays(-9),
            Attention = new Attention("Check roots", Today)
        };
        await plants.SaveAsync(plant);

        var before = plant.Copy();
        plant.LivesElsewhere = true;
        await service.UpdateAsync(before, plant, v => v.ToString());
        before = plant.Copy();
        plant.LivesElsewhere = false;
        await service.UpdateAsync(before, plant, v => v.ToString());

        Assert.Equal(Today.AddDays(-3), plant.QuarantinedSince);
        Assert.Equal(14, plant.QuarantineDays);
        Assert.Equal(Today.AddDays(-9), plant.DormantSince);
        Assert.NotNull(plant.Attention);
    }
}
