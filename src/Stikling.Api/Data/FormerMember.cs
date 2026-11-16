namespace Stikling.Api.Data;

/// <summary>
/// Someone who left a collection or was removed from it. It lets the app keep showing the name of
/// a person who logged care and has gone, until they delete their account. The row is deleted when
/// they join again.
/// </summary>
public class FormerMember
{
    public Guid CollectionId { get; set; }
    public Guid PersonId { get; set; }
    public DateTimeOffset LeftAt { get; set; }
}
