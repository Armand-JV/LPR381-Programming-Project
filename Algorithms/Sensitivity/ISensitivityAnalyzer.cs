using LPR381Project.Models;

namespace LPR381Project.Algorithms.Sensitivity
{
    /// <summary>
    /// Sensitivity analysis operations to run after a model has been solved
    /// to optimality by any of the algorithms in the Algorithms namespace.
    /// OWNER: Person 4 (Sensitivity + Duality).
    ///
    /// Each method should return a human-readable string describing the
    /// result (range, new solution, shadow prices, etc.) - the menu prints
    /// it directly and the output writer can append it to the results file.
    /// variableIndex / constraintIndex are 0-based, matching LPModel's
    /// ObjectiveCoefficients / Constraints arrays.
    /// </summary>
    public interface ISensitivityAnalyzer
    {
        string RangeNonBasicVariable(LPModel model, SolutionResult solved, int variableIndex);
        string ApplyNonBasicVariableChange(LPModel model, SolutionResult solved, int variableIndex, double newCoefficient);

        string RangeBasicVariable(LPModel model, SolutionResult solved, int variableIndex);
        string ApplyBasicVariableChange(LPModel model, SolutionResult solved, int variableIndex, double newCoefficient);

        string RangeConstraintRhs(LPModel model, SolutionResult solved, int constraintIndex);
        string ApplyConstraintRhsChange(LPModel model, SolutionResult solved, int constraintIndex, double newRhs);

        string RangeNonBasicColumn(LPModel model, SolutionResult solved, int variableIndex);
        string ApplyNonBasicColumnChange(LPModel model, SolutionResult solved, int variableIndex, double[] newColumn);

        string AddActivity(LPModel model, SolutionResult solved, double[] newColumn, double newObjectiveCoefficient);
        string AddConstraint(LPModel model, SolutionResult solved, Constraint newConstraint);

        string DisplayShadowPrices(LPModel model, SolutionResult solved);
    }
}
