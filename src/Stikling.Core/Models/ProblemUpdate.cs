using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// One thing that happened with a <see cref="Problem"/>: what was done, how it looks now. A
/// record of its own, so devices adding updates never overwrite each other.
/// </summary>
public sealed class ProblemUpdate : Entity
{
    public Guid ProblemId { get; set; }

    public DateOnly OccurredOn { get; set; }

    [JsonConverter(typeof(TolerantProblemTrendConverter))]
    public ProblemTrend? Trend { get; set; }

    /// <summary>
    /// The names of <see cref="ProblemAction"/> values, as plain strings for the same reason as
    /// <see cref="Problem.Signs"/>. Read them with <see cref="KnownDid"/>.
    /// </summary>
    public List<string> Did { get; set; } = [];

    public string? Note { get; set; }

    public DateOnly? LookAgainOn { get; set; }

    /// <summary>The history line written with the update, so removing the update takes it off again.</summary>
    public Guid? TimelineEntryId { get; set; }

    /// <summary>The actions this version knows, in the order they were stored.</summary>
    [JsonIgnore]
    public IReadOnlyList<ProblemAction> KnownDid =>
        Did.Select(s => Enum.TryParse<ProblemAction>(s, out var action) && Enum.IsDefined(action) ? (ProblemAction?)action : null)
            .OfType<ProblemAction>().ToList();

    public ProblemUpdate Copy()
    {
        var copy = (ProblemUpdate)MemberwiseClone();
        copy.Did = [.. Did];
        return copy;
    }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (ProblemId == Guid.Empty)
            errors.Add(Strings.Pests.UpdateNeedsProblem);
        if (OccurredOn > today)
            errors.Add(Strings.Pests.DateInFuture);
        if (LookAgainOn is { } again && again < OccurredOn)
            errors.Add(Strings.Pests.UpdateLookAgainAfter);
        return errors;
    }
}
