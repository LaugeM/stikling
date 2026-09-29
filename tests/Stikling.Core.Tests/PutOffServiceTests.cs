using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class PutOffServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 28);

    private readonly FakePutOffRepository repository = new();
    private readonly PutOffService service;

    public PutOffServiceTests() => service = new PutOffService(repository);

    [Fact]
    public void The_same_key_gives_the_same_id_and_different_keys_do_not()
    {
        var plant = Guid.NewGuid();

        Assert.Equal(PutOff.IdFor(PutOffs.Check(plant)), PutOff.IdFor(PutOffs.Check(plant)));
        Assert.NotEqual(PutOff.IdFor(PutOffs.Check(plant)), PutOff.IdFor(PutOffs.Check(Guid.NewGuid())));
        Assert.NotEqual(PutOff.IdFor("photos"), PutOff.IdFor("Photos"));
    }

    [Fact]
    public async Task Putting_something_off_hides_it_until_the_day_given()
    {
        await service.PutOffAsync(PutOffs.PhotoRound, Today.AddDays(3));

        var putOffs = await service.GetAsync(Today);
        Assert.True(putOffs.IsPutOff(PutOffs.PhotoRound, Today));
        Assert.False(putOffs.IsPutOff(PutOffs.PhotoRound, Today.AddDays(3)));
    }

    [Fact]
    public async Task Putting_off_the_same_key_twice_keeps_one_record_with_the_later_day()
    {
        await service.PutOffAsync("photos", Today.AddDays(1));
        await service.PutOffAsync("photos", Today.AddDays(7));

        var record = Assert.Single(repository.PutOffs.Values);
        Assert.Equal(PutOff.IdFor("photos"), record.Id);
        Assert.Equal(Today.AddDays(7), record.Until);
    }

    [Fact]
    public async Task Put_offs_that_have_come_back_are_ignored_but_kept()
    {
        await service.PutOffAsync("old", Today);
        await service.PutOffAsync("waiting", Today.AddDays(2));

        var putOffs = await service.GetAsync(Today);

        Assert.False(putOffs.IsPutOff("old", Today.AddDays(-1)));
        Assert.True(putOffs.IsPutOff("waiting", Today));
        Assert.Equal(2, repository.PutOffs.Count);
    }
}
