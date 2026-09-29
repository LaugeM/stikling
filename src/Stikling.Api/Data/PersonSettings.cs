namespace Stikling.Api.Data;

/// <summary>
/// The person's own settings record from the app, such as the theme. It belongs to the person
/// rather than a collection, so it follows them to every device.
/// </summary>
public class PersonSettings
{
    public Guid PersonId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public required string Data { get; set; }
}
