namespace Stikling.Core.Models;

/// <summary>
/// A plant, propagation or pot named with an @ in a note. The note's text keeps the plain "@Name", so
/// an older version of the app that doesn't know about mentions shows the note as it was written. The
/// kind is <see cref="SubjectType.Plant"/>, <see cref="SubjectType.Propagation"/> or <see cref="SubjectType.Pot"/>.
/// </summary>
public sealed record Mention
{
    public SubjectType Kind { get; set; }

    public Guid Id { get; set; }

    /// <summary>The name as it was put in the text, after the @. It doesn't follow a rename of the record.</summary>
    public string Name { get; set; } = "";
}
