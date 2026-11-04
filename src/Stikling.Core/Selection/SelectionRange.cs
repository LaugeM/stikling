namespace Stikling.Core.Selection;

/// <summary>A click on a row in Select mode, and whether shift was held.</summary>
public readonly record struct SelectClick(Guid Id, bool Shift);

/// <summary>
/// Select mode on a list: a click toggles a row, and a shift-click picks or clears every row
/// between the anchor (the row last clicked) and this one.
/// </summary>
public static class SelectionRange
{
    /// <summary>
    /// Applies a click to the selection and returns the new anchor. <paramref name="shown"/> is the rows in the
    /// order they are on screen. A shift-click without a usable anchor acts like a plain click.
    /// </summary>
    public static Guid? Apply(IReadOnlyList<Guid> shown, ISet<Guid> selected, Guid? anchor, Guid clicked, bool shift)
    {
        var becomesSelected = !selected.Contains(clicked);
        var from = shift && anchor is { } a ? IndexOf(shown, a) : -1;
        var to = IndexOf(shown, clicked);

        if (from < 0 || to < 0)
        {
            Set(selected, clicked, becomesSelected);
            return clicked;
        }

        var (low, high) = from <= to ? (from, to) : (to, from);
        for (var i = low; i <= high; i++)
            Set(selected, shown[i], becomesSelected);
        return clicked;
    }

    private static int IndexOf(IReadOnlyList<Guid> shown, Guid id)
    {
        for (var i = 0; i < shown.Count; i++)
            if (shown[i] == id)
                return i;
        return -1;
    }

    private static void Set(ISet<Guid> selected, Guid id, bool on)
    {
        if (on)
            selected.Add(id);
        else
            selected.Remove(id);
    }
}
