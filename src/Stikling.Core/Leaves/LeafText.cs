using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Leaves;

/// <summary>The words for leaves, kept out of the pages so they can be tested.</summary>
public static class LeafText
{
    public static string Rating(Variegation rating) => rating switch
    {
        Variegation.Reverted => Strings.Pests.VariegationReverted,
        Variegation.Low => Strings.Pests.VariegationLow,
        Variegation.Medium => Strings.Pests.VariegationMedium,
        Variegation.High => Strings.Pests.VariegationHigh,
        _ => Strings.Pests.VariegationAllWhite
    };

    /// <summary>The history line for a leaf, with its note as a second line when there is one.</summary>
    public static IReadOnlyList<HistoryLine> Lines(Leaf leaf)
    {
        var line = HistoryText.Line("NewLeaf", HistoryArg.Of(leaf.Rating ?? Variegation.Medium));
        return string.IsNullOrWhiteSpace(leaf.Note)
            ? [line]
            : [line, HistoryText.Line("TextLine", HistoryArg.Of(leaf.Note.Trim()))];
    }
}
