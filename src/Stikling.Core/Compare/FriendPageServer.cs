using Stikling.Core.Sharing;

namespace Stikling.Core.Compare;

/// <summary>The compare endpoint of the API, which needs no sign-in.</summary>
public interface IFriendPageServer
{
    /// <summary>
    /// A friend's public page as a list, by its address (@name or a token). A page that isn't there is a
    /// failure with status 404 and one that was turned off is 410. A server that can't be reached is Offline.
    /// </summary>
    Task<ShareOutcome<FriendPageResponse>> GetFriendPageAsync(string address);
}
