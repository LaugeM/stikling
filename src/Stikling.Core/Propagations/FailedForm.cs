using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>
/// The rules of the Mark failed sheet, kept apart from the page so they can be tested. The page binds
/// to the properties and calls <see cref="CountForm.SaveAsync"/>.
/// </summary>
public sealed class FailedForm : CountForm
{
    internal FailedForm(UnitForms deps, Propagation propagation) : base(deps, propagation)
    {
    }

    public FailReason? Reason { get; set; }

    public string? Note { get; set; }

    private protected override Task RecordAsync(Propagation propagation) =>
        Deps.PropagationService.MarkFailedAsync(propagation, Count, Reason, Note, Date);
}
