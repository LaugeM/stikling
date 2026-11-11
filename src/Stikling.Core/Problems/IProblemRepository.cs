using Stikling.Core.Models;

namespace Stikling.Core.Problems;

public interface IProblemRepository : IRepository<Problem>
{
}

public interface IProblemUpdateRepository : IRepository<ProblemUpdate>
{
}
