using Stikling.Core.Examples;

namespace Stikling.Core.Tour;

/// <summary>
/// One stop on the tour. The route is relative to the app's base address, so Today is an empty string.
/// The target is the value of the <c>data-tour</c> attribute on the part of the page to mark, or null for none.
/// </summary>
public sealed record TourStep(string Route, string? Target, string Title, string Text);

/// <summary>
/// The tour of the example plants for someone's first visit: the five stops, the closing card, and which one
/// is showing. Nothing here is stored, so a reload ends the tour.
/// </summary>
public sealed class GuidedTour
{
    public static readonly IReadOnlyList<TourStep> Steps =
    [
        new("", "today", "Today",
            "What needs doing turns up here. The coleus and its cuttings are an example, so try anything on them."),
        new($"plants/{ExampleFamily.MotherId}", "dock", "A plant's page",
            "Each plant opens on its photos and notes, newest first. The marked buttons add a photo, a note or care."),
        new($"propagations/{ExampleFamily.CurrentCuttingId}", "stage", "A cutting",
            "A cutting you're rooting is a propagation. It keeps where it came from, what it's rooting in and how many days it has taken. When it has rooted, it becomes a plant of its own."),
        new("water", "water", "Watering",
            "Tick what you watered as you go round. There's no fixed watering day. Stikling learns from what you log and asks you to check a plant when it's probably dry."),
        new("backup", "spreadsheet", "Your data",
            "Everything stays on this device, with no account needed. Save a backup or a spreadsheet whenever you like, or import yours to bring your plants in.")
    ];

    /// <summary>The card after the last stop. It has no count and marks nothing.</summary>
    public static readonly TourStep Closing = new("", null, "That's the tour", "");

    private int index;

    public bool Active { get; private set; }

    /// <summary>Where the tour is: 0 to 4 for the stops, 5 for the closing card.</summary>
    public int Index => index;

    public bool IsClosing => Active && index == Steps.Count;

    public TourStep Current => index < Steps.Count ? Steps[index] : Closing;

    /// <summary>The 1-based number of the stop, for "2 of 5". Zero on the closing card.</summary>
    public int Number => IsClosing ? 0 : index + 1;

    public int Count => Steps.Count;

    public bool CanGoBack => Active && index > 0;

    public void Start()
    {
        Active = true;
        index = 0;
    }

    /// <summary>Moves on, and from the closing card ends the tour. Returns whether anything changed.</summary>
    public bool Next()
    {
        if (!Active)
            return false;
        if (index >= Steps.Count)
        {
            Skip();
            return true;
        }
        index++;
        return true;
    }

    /// <summary>Goes back a step. It stops at the first.</summary>
    public bool Back()
    {
        if (!CanGoBack)
            return false;
        index--;
        return true;
    }

    /// <summary>Ends the tour from any step.</summary>
    public bool Skip()
    {
        if (!Active)
            return false;
        Active = false;
        index = 0;
        return true;
    }
}
