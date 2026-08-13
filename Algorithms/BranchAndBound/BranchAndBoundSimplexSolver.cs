using System;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.BranchAndBound
{
    /// <summary>
    /// OWNER: Person 2 (Branch &amp; Bound Simplex)
    ///
    /// TODO:
    ///   1. Solve the LP relaxation of the model (drop the int/bin
    ///      restrictions and reuse Person 1's PrimalSimplexSolver on a
    ///      model.Clone()).
    ///   2. If every int/bin variable already has an integer value, that IS
    ///      the optimal integer solution - stop.
    ///   3. Otherwise pick a fractional int/bin variable and branch into two
    ///      sub-problems: one with an added "<= floor(value)" constraint,
    ///      one with ">= ceil(value)". Solve each sub-problem the same way.
    ///   4. Implement backtracking across the tree of sub-problems, and
    ///      fathom a branch when it is infeasible, when its relaxed bound is
    ///      worse than the current best integer solution, or when it is
    ///      already integer feasible.
    ///   5. Track the best candidate found across all sub-problems.
    ///   6. For every sub-problem, add its tableau iterations to
    ///      result.Iterations (a good Tableau.Label is something like
    ///      "Sub-problem 3 (x2 &lt;= 2), Iteration 1") and record fathoming
    ///      reasons / the best candidate in result.Notes so they can be
    ///      displayed and exported.
    ///   7. Implement algorithm-specific validation / error handling for
    ///      infeasible or unbounded relaxations.
    ///
    /// See Project_LPR381_Programming.pdf, "Algorithm Criteria" -> Branch &amp;
    /// Bound Simplex (backtracking, all sub-problems created and fathomed,
    /// all table iterations of sub-problems displayed, best candidate
    /// displayed).
    /// </summary>
    public class BranchAndBoundSimplexSolver : IAlgorithm
    {
        public string Name
        {
            get { return "Branch & Bound Simplex"; }
        }

        public SolutionResult Solve(LPModel model)
        {
            // TODO: replace this placeholder with the real algorithm.
            throw new NotImplementedException(Name + " has not been implemented yet.");
        }
    }
}
