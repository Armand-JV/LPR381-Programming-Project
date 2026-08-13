using LPR381Project.Models;

namespace LPR381Project.Algorithms.Sensitivity
{
    /// <summary>
    /// Duality operations. OWNER: Person 4 (Sensitivity + Duality).
    /// </summary>
    public interface IDualityAnalyzer
    {
        /// <summary>Build the dual model of the given primal LPModel.</summary>
        LPModel ApplyDuality(LPModel primalModel);

        /// <summary>Solve the dual model (typically by reusing Person 1's Primal Simplex solver).</summary>
        SolutionResult SolveDual(LPModel dualModel);

        /// <summary>Compare the primal and dual optimal objective values and report whether
        /// the pair exhibits strong or weak duality.</summary>
        string VerifyDuality(SolutionResult primalSolution, SolutionResult dualSolution);
    }
}
