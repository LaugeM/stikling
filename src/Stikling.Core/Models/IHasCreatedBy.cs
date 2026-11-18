namespace Stikling.Core.Models;

/// <summary>
/// A record that remembers who added it, for the kinds a helper may add: care entries, photos and notes.
/// The device fills it in when the record is first saved, and sync fills it in on what comes from the server.
/// </summary>
public interface IHasCreatedBy
{
    Guid? CreatedBy { get; set; }
}
