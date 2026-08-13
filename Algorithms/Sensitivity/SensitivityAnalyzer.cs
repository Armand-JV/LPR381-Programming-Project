using System;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.Sensitivity
{
    /// <summary>
    /// OWNER: Person 4 (Sensitivity + Duality)
    ///
    /// TODO: implement each operation against the optimal tableau stored in
    /// SolutionResult.Iterations (the last entry is the final tableau).
    /// Typical approach for each:
    ///   - Range of a non-basic variable: how far its objective coefficient
    ///     can change before it would want to enter the basis.
    ///   - Apply change to a non-basic variable: recompute its reduced cost
    ///     with the new coefficient and report the new (still optimal, or
    ///     newly attractive) tableau.
    ///   - Range / apply change to a basic variable: propagate the change
    ///     through the row it is basic in and check every other row stays
    ///     feasible/optimal.
    ///   - Range / apply change to a constraint RHS: use B^-1 * new RHS and
    ///     check feasibility (all basic variable values stay &gt;= 0).
    ///   - Range / apply change to a non-basic column: recompute B^-1 * a_j.
    ///   - Add a new activity: price out the new column against the current
    ///     dual prices (y^T) to see if it should enter.
    ///   - Add a new constraint: check if the current optimal solution
    ///     already satisfies it; if not, add it as a new row and re-optimize
    ///     (dual simplex is usually simplest here).
    ///   - Shadow prices: the objective-row coefficients under the slack /
    ///     artificial columns in the optimal tableau.
    ///
    /// See Project_LPR381_Programming.pdf, "Sensitivity Analysis Criteria"
    /// for the exact list of operations required.
    /// </summary>
    public class SensitivityAnalyzer : ISensitivityAnalyzer
    {
        public string RangeNonBasicVariable(LPModel model, SolutionResult solved, int variableIndex)
        {
            throw new NotImplementedException("RangeNonBasicVariable has not been implemented yet.");
        }

        public string ApplyNonBasicVariableChange(LPModel model, SolutionResult solved, int variableIndex, double newCoefficient)
        {
            throw new NotImplementedException("ApplyNonBasicVariableChange has not been implemented yet.");
        }

        public string RangeBasicVariable(LPModel model, SolutionResult solved, int variableIndex)
        {
            throw new NotImplementedException("RangeBasicVariable has not been implemented yet.");
        }

        public string ApplyBasicVariableChange(LPModel model, SolutionResult solved, int variableIndex, double newCoefficient)
        {
            throw new NotImplementedException("ApplyBasicVariableChange has not been implemented yet.");
        }

        public string RangeConstraintRhs(LPModel model, SolutionResult solved, int constraintIndex)
        {
            throw new NotImplementedException("RangeConstraintRhs has not been implemented yet.");
        }

        public string ApplyConstraintRhsChange(LPModel model, SolutionResult solved, int constraintIndex, double newRhs)
        {
            throw new NotImplementedException("ApplyConstraintRhsChange has not been implemented yet.");
        }

        public string RangeNonBasicColumn(LPModel model, SolutionResult solved, int variableIndex)
        {
            throw new NotImplementedException("RangeNonBasicColumn has not been implemented yet.");
        }

        public string ApplyNonBasicColumnChange(LPModel model, SolutionResult solved, int variableIndex, double[] newColumn)
        {
            throw new NotImplementedException("ApplyNonBasicColumnChange has not been implemented yet.");
        }

        public string AddActivity(LPModel model, SolutionResult solved, double[] newColumn, double newObjectiveCoefficient)
        {
            throw new NotImplementedException("AddActivity has not been implemented yet.");
        }

        public string AddConstraint(LPModel model, SolutionResult solved, Constraint newConstraint)
        {
            throw new NotImplementedException("AddConstraint has not been implemented yet.");
        }

        public string DisplayShadowPrices(LPModel model, SolutionResult solved)
        {
            throw new NotImplementedException("DisplayShadowPrices has not been implemented yet.");
        }
    }
}
