namespace Stikling.Api.Data;

/// <summary>
/// One record from the app, such as a plant or a care log, kept as the JSON the device stores.
/// The server only needs the columns next to it to sync, so a new field in the app needs no
/// change here.
/// </summary>
public class SyncedRecord
{
    public Guid CollectionId { get; set; }

    /// <summary>What it is, named after the store it is kept in on the device. See <c>SyncKinds</c>.</summary>
    public required string Kind { get; set; }

    /// <summary>The id the device gave it.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The collection's change number when this version arrived. A device asks for everything
    /// after the last number it saw.
    /// </summary>
    public long Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public required string Data { get; set; }
}
