using System.Collections.Generic;
using LPR381Project.Algorithms.BranchAndBound;
using LPR381Project.Algorithms.IntegerAlgorithms;
using LPR381Project.Algorithms.PrimalSimplex;

namespace LPR381Project.Algorithms
{
    /// <summary>
    /// Central place that lists every available algorithm so the menu never
    /// needs to change when someone finishes implementing theirs. Everyone:
    /// once your solver actually works, nothing else needs to change here -
    /// it is already wired into the menu.
    /// </summary>
    public static class AlgorithmRegistry
    {
        public static List<IAlgorithm> GetAll()
        {
            return new List<IAlgorithm>
            {
                new PrimalSimplexSolver(),          // Person 1
                new RevisedPrimalSimplexSolver(),   // Person 1
                new BranchAndBoundSimplexSolver(),  // Person 2
                new CuttingPlaneSolver(),           // Person 3
                new BranchAndBoundKnapsackSolver(), // Person 3
            };
        }
    }
}
