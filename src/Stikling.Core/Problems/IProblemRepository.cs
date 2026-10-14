using Stikling.Core.Models;

namespace Stikling.Core.Problems;

public interface IProblemRepository
{
    /// <summary>All problems that aren't deleted.</summary>
    Task<IReadOnlyList<Problem>> GetAllAsync();

    Task<Problem?> GetAsync(Guid id);

    /// <summary>Adds or updates a problem. Throws if it isn't valid.</summary>
    Task SaveAsync(Problem problem);

    /// <summary>Soft-deletes a problem.</summary>
    Task DeleteAsync(Guid id);
}

public interface IProblemUpdateRepository
{
    /// <summary>All updates that aren't deleted.</summary>
    Task<IReadOnlyList<ProblemUpdate>> GetAllAsync();

    Task<ProblemUpdate?> GetAsync(Guid id);

    /// <summary>Adds or updates an update. Throws if it isn't valid.</summary>
    Task SaveAsync(ProblemUpdate update);

    /// <summary>Soft-deletes an update.</summary>
    Task DeleteAsync(Guid id);
}
