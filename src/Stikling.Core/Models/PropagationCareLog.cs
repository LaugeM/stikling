namespace Stikling.Core.Models;

/// <summary>
/// One entry in a propagation's care log: watered, changed the water or a moisture reading.
/// Kept apart from <see cref="CareLog"/>, which belongs to plants. When the propagation is
/// potted up, its last watering is copied to the new plants as a plant care entry.
/// </summary>
public sealed class PropagationCareLog : Entity
{
    public Guid PropagationId { get; set; }

    /// <summary>The day it was done, which can be earlier than when it was written down.</summary>
    public DateOnly OccurredOn { get; set; }

    public PropagationCareKind Kind { get; set; }

    /// <summary>The moisture meter reading, 0 to 10. Only on <see cref="PropagationCareKind.MoistureReading"/>.</summary>
    public int? Moisture { get; set; }

    public string? Notes { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (PropagationId == Guid.Empty)
            errors.Add("A care entry has to belong to a propagation.");

        if (Kind == PropagationCareKind.MoistureReading)
        {
            if (Moisture is null)
                errors.Add("Give the moisture reading a number from 0 to 10.");
            else if (Moisture is < 0 or > 10)
                errors.Add("The moisture reading goes from 0 to 10.");
        }
        else if (Moisture is not null)
        {
            errors.Add("Only a moisture reading has a number.");
        }

        return errors;
    }
}
