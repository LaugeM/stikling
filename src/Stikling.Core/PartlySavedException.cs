namespace Stikling.Core;

/// <summary>
/// The record was saved, but a step after the save failed, like a line in the history. What was
/// saved stays saved, so the caller treats it as done and doesn't run the save again. The message
/// is the one of the step that failed.
/// </summary>
public sealed class PartlySavedException(Exception inner) : Exception(inner.Message, inner)
{
    /// <summary>Runs the steps that come after a save, so a failure in them says the save itself went through.</summary>
    public static async Task AfterSaveAsync(Func<Task> steps)
    {
        try
        {
            await steps();
        }
        catch (PartlySavedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PartlySavedException(ex);
        }
    }
}
