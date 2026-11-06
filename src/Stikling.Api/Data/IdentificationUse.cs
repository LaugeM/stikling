namespace Stikling.Api.Data;

/// <summary>
/// How many plant identifications a person has had on one day (UTC, since Pl@ntNet's quota starts again
/// at midnight UTC). Only kept on the server, not a synced record, and rows older than yesterday are
/// deleted as new identifications come in. The photos themselves are never kept.
/// </summary>
public class IdentificationUse
{
    public Guid PersonId { get; set; }

    public DateOnly Day { get; set; }

    public int Count { get; set; }
}
