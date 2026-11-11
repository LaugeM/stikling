using System.Text;
using Stikling.Core.Sharing;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Compare;

/// <summary>The message to send a friend after a comparison, to ask for a swap.</summary>
public static class SwapReplyText
{
    /// <summary>
    /// Their plants I'd like under one heading, spare ones first, and mine they are looking for under
    /// another. A section with no rows is left out, and when both are empty it just asks if they'd like to swap.
    /// </summary>
    /// <param name="theirTitle">The title of their page, if they had one. Not used in the text yet, so a message reads the same whoever it goes to.</param>
    public static string Write(ListComparisonResult result, string? theirTitle)
    {
        var text = new StringBuilder();
        text.AppendLine(Strings.Compare.ReplyHello);
        text.AppendLine();

        if (result.ForYou.Count == 0 && result.ForThem.Count == 0)
        {
            text.AppendLine(Strings.Compare.ReplyWouldYouSwap);
        }
        else
        {
            if (result.ForYou.Count > 0)
            {
                text.AppendLine(Strings.Compare.ReplyCouldIHave);
                foreach (var match in result.ForYou)
                {
                    var spare = match.Theirs.SpareText();
                    text.AppendLine(spare is null ? $"- {match.Theirs.Name}" : $"- {match.Theirs.Name}, {spare}");
                }
                text.AppendLine();
            }
            if (result.ForThem.Count > 0)
            {
                text.AppendLine(Strings.Compare.ReplyICouldOffer);
                foreach (var match in result.ForThem)
                    text.AppendLine($"- {match.Name}");
                text.AppendLine();
            }
        }

        if (result.ForYou.Count == 0 && result.ForThem.Count == 0)
            text.AppendLine();
        text.Append(SwapLists.Footer);
        return text.ToString();
    }
}
