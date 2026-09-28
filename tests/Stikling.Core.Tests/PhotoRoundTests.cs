using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class PhotoRoundTests
{
    private static readonly DateOnly Today = new(2026, 9, 28);
    private static readonly FixedTime Time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

    private static Photo TakenOn(Guid subjectId, DateOnly day) => new()
    {
        SubjectId = subjectId,
        TakenAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)
    };

    private static Dictionary<Guid, Photo> Newest(params Photo[] photos) => photos.ToDictionary(p => p.SubjectId);

    [Fact]
    public void Anything_without_a_photo_this_month_is_due()
    {
        var lastMonth = new Plant { Nickname = "Monstera" };
        var thisMonth = new Plant { Nickname = "Pothos" };
        var never = new Propagation { Nickname = "Coleus", StartedOn = Today };

        var due = PhotoRound.Due(
            [lastMonth, thisMonth], [never],
            Newest(TakenOn(lastMonth.Id, new DateOnly(2026, 8, 31)), TakenOn(thisMonth.Id, new DateOnly(2026, 9, 1))),
            Time);

        Assert.Equal(["Coleus", "Monstera"], due.Select(d => d.Name));
        Assert.Null(due[0].LastPhoto);
        Assert.Equal(new DateOnly(2026, 8, 31), due[1].LastPhoto);
    }

    [Fact]
    public void Resting_gone_and_finished_ones_are_left_out()
    {
        var dormant = new Plant { Nickname = "Alocasia", DormantSince = Today };
        var died = new Plant { Nickname = "Hoya", Status = PlantStatus.Died };
        var deleted = new Plant { Nickname = "Fern", DeletedAt = DateTimeOffset.UtcNow };
        var sleepingCorm = new Propagation { Nickname = "Corm", StartedOn = Today, DormantSince = Today };
        var done = new Propagation { Nickname = "Coleus", StartedOn = Today, Stage = PropagationStage.Done };

        Assert.Empty(PhotoRound.Due([dormant, died, deleted], [sleepingCorm, done], Newest(), Time));
    }

    [Fact]
    public void The_round_goes_room_by_room_with_no_room_last()
    {
        var nowhere = new Plant { Nickname = "Aloe" };
        var kitchen = new Plant { Nickname = "Basil", Location = "Kitchen" };
        var bedroom = new Plant { Nickname = "Pothos", Location = "Bedroom" };
        var bedroomToo = new Propagation { Nickname = "Coleus", Location = "Bedroom", StartedOn = Today };

        var due = PhotoRound.Due([nowhere, kitchen, bedroom], [bedroomToo], Newest(), Time);

        Assert.Equal(["Coleus", "Pothos", "Basil", "Aloe"], due.Select(d => d.Name));
    }

    [Fact]
    public void Skipping_puts_it_off_until_the_first_of_next_month()
    {
        Assert.Equal(new DateOnly(2026, 10, 1), PhotoRound.NextRound(Today));
        Assert.Equal(new DateOnly(2027, 1, 1), PhotoRound.NextRound(new DateOnly(2026, 12, 1)));
    }
}
