using Stikling.Core.Models;

namespace Stikling.Core.Feeds;

/// <summary>Care entries keep their own copy of a deleted feed's name and doses.</summary>
public interface IFeedRepository : IRepository<Feed>
{
}
