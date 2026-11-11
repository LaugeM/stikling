using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PhotoCompareTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static Photo At(int days, bool reference = false, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), TakenAt = Start.AddDays(days), IsReference = reference };

    [Fact]
    public void Reference_photos_are_left_out()
    {
        var own = At(1);
        var comparable = PhotoCompare.Comparable([own, At(2, reference: true)]);
        Assert.Equal([own.Id], comparable.Select(p => p.Id));
    }

    [Fact]
    public void A_photo_that_comes_twice_is_listed_once()
    {
        var photo = At(1);
        Assert.Single(PhotoCompare.Comparable([photo, photo]));
    }

    [Fact]
    public void The_oldest_comes_first()
    {
        var a = At(5);
        var b = At(1);
        var c = At(3);
        Assert.Equal([b.Id, c.Id, a.Id], PhotoCompare.Comparable([a, b, c]).Select(p => p.Id));
    }

    [Fact]
    public void The_same_moment_goes_by_id()
    {
        var low = At(1, id: new Guid("00000000-0000-0000-0000-000000000001"));
        var high = At(1, id: new Guid("00000000-0000-0000-0000-000000000002"));
        Assert.Equal([low.Id, high.Id], PhotoCompare.Comparable([high, low]).Select(p => p.Id));
        Assert.Equal([low.Id, high.Id], PhotoCompare.Comparable([low, high]).Select(p => p.Id));
    }

    [Fact]
    public void A_middle_photo_starts_with_the_oldest()
    {
        var list = PhotoCompare.Comparable([At(1), At(2), At(3)]);
        Assert.Equal((list[0].Id, list[1].Id), PhotoCompare.StartPair(list, list[1].Id));
    }

    [Fact]
    public void The_newest_starts_with_the_oldest()
    {
        var list = PhotoCompare.Comparable([At(1), At(2), At(3)]);
        Assert.Equal((list[0].Id, list[2].Id), PhotoCompare.StartPair(list, list[2].Id));
    }

    [Fact]
    public void The_oldest_starts_with_the_newest()
    {
        var list = PhotoCompare.Comparable([At(1), At(2), At(3)]);
        Assert.Equal((list[0].Id, list[2].Id), PhotoCompare.StartPair(list, list[0].Id));
    }

    [Fact]
    public void One_photo_has_no_pair()
    {
        var list = PhotoCompare.Comparable([At(1)]);
        Assert.Null(PhotoCompare.StartPair(list, list[0].Id));
    }

    [Fact]
    public void A_reference_photo_has_no_pair()
    {
        var reference = At(2, reference: true);
        var list = PhotoCompare.Comparable([At(1), At(3), reference]);
        Assert.Null(PhotoCompare.StartPair(list, reference.Id));
    }
}
