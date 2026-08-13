using System;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.Sensitivity
{
    /// <summary>
    /// OWNER: Person 4 (Sensitivity + Duality)
    ///
    /// TODO:
    ///   1. ApplyDuality: build the dual LPModel from the primal - swap the
    ///      number of variables/constraints, transpose the coefficient
    ///      matrix, swap objective coefficients with RHS values, flip
    ///      max/min, and map relation types / sign restrictions according to
    ///      the primal-dual conversion rules.
    ///   2. SolveDual: solve the dual model, typically by calling
    ///      new PrimalSimplex.PrimalSimplexSolver().Solve(dualModel).
    ///   3. VerifyDuality: compare primalSolution.ObjectiveValue and
    ///      dualSolution.ObjectiveValue. If they match (within rounding),
    ///      report Strong Duality; otherwise report Weak Duality and explain
    ///      the gap.
    ///
    /// See Project_LPR381_Programming.pdf, "Sensitivity Analysis Criteria" ->
    /// Duality (apply duality, solve the dual, verify strong/weak duality).
    /// </summary>
    public class DualityAnalyzer : IDualityAnalyzer
    {
        public LPModel ApplyDuality(LPModel primalModel)
        {
            throw new NotImplementedException("ApplyDuality has not been implemented yet.");
        }

        public SolutionResult SolveDual(LPModel dualModel)
        {
            throw new NotImplementedException("SolveDual has not been implemented yet.");
        }

        public string VerifyDuality(SolutionResult primalSolution, SolutionResult dualSolution)
        {
            throw new NotImplementedException("VerifyDuality has not been implemented yet.");
        }
    }
}
