using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class PutOffsTests
{
    private static readonly DateOnly Today = new(2026, 9, 28);

    [Fact]
    public void Something_put_off_comes_back_on_the_day_given()
    {
        var putOffs = PutOffs.Empty();
        var key = PutOffs.Check(Guid.NewGuid());

        putOffs.PutOff(key, Today.AddDays(3));

        Assert.True(putOffs.IsPutOff(key, Today));
        Assert.True(putOffs.IsPutOff(key, Today.AddDays(2)));
        Assert.False(putOffs.IsPutOff(key, Today.AddDays(3)));
        Assert.False(putOffs.IsPutOff(PutOffs.PhotoRound, Today));
    }

    [Fact]
    public void Put_offs_are_built_from_records_that_have_not_come_back_yet()
    {
        var records = new[]
        {
            new PutOff { Key = "waiting", Until = Today.AddDays(7) },
            new PutOff { Key = "tomorrow", Until = Today.AddDays(1) },
            new PutOff { Key = "today", Until = Today },
            new PutOff { Key = "past", Until = Today.AddDays(-4) },
            new PutOff { Key = "deleted", Until = Today.AddDays(7), DeletedAt = DateTimeOffset.UtcNow }
        };

        var putOffs = PutOffs.From(records, Today);

        Assert.True(putOffs.IsPutOff("waiting", Today));
        Assert.True(putOffs.IsPutOff("tomorrow", Today));
        Assert.False(putOffs.IsPutOff("today", Today.AddDays(-1)));
        Assert.False(putOffs.IsPutOff("past", Today.AddDays(-5)));
        Assert.False(putOffs.IsPutOff("deleted", Today));
    }

    [Fact]
    public void Nothing_saved_starts_empty()
    {
        Assert.False(PutOffs.From([], Today).IsPutOff("photos", Today));
    }

    [Fact]
    public void A_flag_raised_again_is_not_still_put_off()
    {
        var id = Guid.NewGuid();
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Flag(id, new Attention("Repot soon", Today.AddDays(-2))), Today.AddDays(7));

        Assert.True(putOffs.IsPutOff(PutOffs.Flag(id, new Attention("Repot soon", Today.AddDays(-2))), Today));
        Assert.False(putOffs.IsPutOff(PutOffs.Flag(id, new Attention("Repot soon", Today)), Today));
    }
}
