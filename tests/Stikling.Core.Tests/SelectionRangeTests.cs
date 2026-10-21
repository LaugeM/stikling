using Stikling.Core.Selection;

namespace Stikling.Core.Tests;

public class SelectionRangeTests
{
    private static readonly Guid[] Rows = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();

    [Fact]
    public void A_plain_click_toggles_and_sets_the_anchor()
    {
        var selected = new HashSet<Guid>();

        var anchor = SelectionRange.Apply(Rows, selected, null, Rows[2], shift: false);
        Assert.Equal(Rows[2], anchor);
        Assert.Equal([Rows[2]], selected);

        anchor = SelectionRange.Apply(Rows, selected, anchor, Rows[2], shift: false);
        Assert.Equal(Rows[2], anchor);
        Assert.Empty(selected);
    }

    [Fact]
    public void Shift_picks_the_rows_down_from_the_anchor()
    {
        var selected = new HashSet<Guid>();
        var anchor = SelectionRange.Apply(Rows, selected, null, Rows[1], false);

        anchor = SelectionRange.Apply(Rows, selected, anchor, Rows[4], true);

        Assert.Equal(Rows[1..5].ToHashSet(), selected);
        Assert.Equal(Rows[4], anchor);
    }

    [Fact]
    public void Shift_picks_the_rows_up_from_the_anchor()
    {
        var selected = new HashSet<Guid>();
        var anchor = SelectionRange.Apply(Rows, selected, null, Rows[4], false);

        SelectionRange.Apply(Rows, selected, anchor, Rows[1], true);

        Assert.Equal(Rows[1..5].ToHashSet(), selected);
    }

    [Fact]
    public void Shift_on_a_row_that_becomes_unselected_clears_the_range()
    {
        var selected = Rows.ToHashSet();
        var anchor = SelectionRange.Apply(Rows, selected, null, Rows[1], false);
        Assert.DoesNotContain(Rows[1], selected);

        SelectionRange.Apply(Rows, selected, anchor, Rows[3], true);

        Assert.Equal(new[] { Rows[0], Rows[4], Rows[5] }.ToHashSet(), selected);
    }

    [Fact]
    public void Shift_without_an_anchor_acts_like_a_plain_click()
    {
        var selected = new HashSet<Guid>();

        var anchor = SelectionRange.Apply(Rows, selected, null, Rows[3], true);

        Assert.Equal([Rows[3]], selected);
        Assert.Equal(Rows[3], anchor);
    }

    [Fact]
    public void Shift_with_an_anchor_that_is_no_longer_shown_acts_like_a_plain_click()
    {
        var selected = new HashSet<Guid>();

        var anchor = SelectionRange.Apply(Rows, selected, Guid.NewGuid(), Rows[3], true);

        Assert.Equal([Rows[3]], selected);
        Assert.Equal(Rows[3], anchor);
    }

    [Fact]
    public void A_range_leaves_rows_outside_it_alone()
    {
        var selected = new HashSet<Guid> { Rows[0], Rows[5] };

        SelectionRange.Apply(Rows, selected, Rows[2], Rows[3], true);

        Assert.Equal(new[] { Rows[0], Rows[2], Rows[3], Rows[5] }.ToHashSet(), selected);
    }

    [Fact]
    public void The_range_follows_the_order_shown()
    {
        var selected = new HashSet<Guid>();
        var shown = new[] { Rows[3], Rows[0], Rows[5], Rows[1] };

        SelectionRange.Apply(shown, selected, Rows[3], Rows[5], true);

        Assert.Equal(new[] { Rows[3], Rows[0], Rows[5] }.ToHashSet(), selected);
    }
}
