using System;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.IntegerAlgorithms
{
    /// <summary>
    /// OWNER: Person 3 (Integer Algorithms)
    ///
    /// TODO:
    ///   1. Solve the LP relaxation of the model (reuse Person 1's
    ///      PrimalSimplexSolver on a model.Clone(), the same way Person 2
    ///      does for Branch &amp; Bound).
    ///   2. If the relaxed solution already satisfies all int/bin
    ///      restrictions, stop - it is optimal.
    ///   3. Otherwise pick a source row with a fractional basic variable and
    ///      derive a Gomory cut from it, add the cut as a new constraint to
    ///      the working model/tableau, and re-solve (dual simplex is the
    ///      usual choice once a cut is added, since it restores primal
    ///      feasibility after the RHS becomes negative).
    ///   4. Repeat step 3 until the solution is integer feasible or the
    ///      problem is shown to be infeasible.
    ///   5. Record every Product Form / Price Out (or tableau) iteration,
    ///      including each added cut, in result.Iterations so they can be
    ///      displayed and exported the same way as the other algorithms.
    ///
    /// See Project_LPR381_Programming.pdf, "Algorithm Criteria" -> Cutting
    /// Plane Algorithm ("Display the Canonical Form and solve using the
    /// Cutting Plane Algorithm. Display all Product Form and Price Out
    /// iterations.").
    /// </summary>
    public class CuttingPlaneSolver : IAlgorithm
    {
        public string Name
        {
            get { return "Cutting Plane"; }
        }

        public SolutionResult Solve(LPModel model)
        {
            // TODO: replace this placeholder with the real algorithm.
            throw new NotImplementedException(Name + " has not been implemented yet.");
        }
    }
}
