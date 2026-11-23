namespace Stikling.Web.Services;

/// <summary>
/// Says something to a screen reader without moving focus, like "Monstera watered" after a tap. A page
/// calls <see cref="Say"/> and the <c>LiveRegion</c> in the layout reads it out. Saved, done and undone
/// messages go through here, so they are read out and never only shown.
/// </summary>
public sealed class Announcer
{
    /// <summary>Raised with the text to read out.</summary>
    public event Action<string>? Said;

    public void Say(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            Said?.Invoke(text);
    }
}
