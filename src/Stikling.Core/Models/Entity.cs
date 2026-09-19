using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>
/// Base for everything that is stored. Ids are generated on the device and deletes are soft
/// (<see cref="DeletedAt"/>), which keeps a later sync between devices straightforward.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonIgnore]
    public bool IsDeleted => DeletedAt is not null;
}
