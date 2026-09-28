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
    public void Saved_put_offs_read_back_without_the_ones_that_have_come_back()
    {
        var putOffs = PutOffs.Empty();
        putOffs.PutOff("waiting", Today.AddDays(7));
        putOffs.PutOff("back", Today.AddDays(1));

        var later = PutOffs.Parse(putOffs.ToJson(), Today.AddDays(1));

        Assert.True(later.IsPutOff("waiting", Today.AddDays(1)));
        Assert.DoesNotContain("back", later.ToJson());
    }

    [Fact]
    public void Nothing_saved_or_something_unreadable_starts_empty()
    {
        Assert.Equal("{}", PutOffs.Parse(null, Today).ToJson());
        Assert.Equal("{}", PutOffs.Parse("not json", Today).ToJson());
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
