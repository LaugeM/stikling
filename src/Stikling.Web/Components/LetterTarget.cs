namespace Stikling.Web.Components;

/// <summary>
/// One letter on the letter strip: A to Z, or # for the rest. <paramref name="TargetId"/> is the id of the
/// element it scrolls to, or null when no name starts with it.
/// </summary>
public sealed record LetterTarget(char Letter, string? TargetId);
