using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LPR381Project.Algorithms.PrimalSimplex;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.IntegerAlgorithms
{
    /// <summary>
    /// Solves pure integer and binary maximisation models using
    /// Gomory fractional cutting planes.
    /// </summary>
    ///  /// <summary>
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
        private const double Tolerance = 0.000001;
        private const int MaximumCuts = 50;
        private const int MaximumDualPivots = 1000;

        public string Name => "Cutting Plane";

        public SolutionResult Solve(LPModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var result = new SolutionResult
            {
                AlgorithmName = Name,
                Status = SolutionStatus.NotSolved
            };

            if (!ValidateModel(model, result))
            {
                return result;
            }

            LPModel workingModel = model.Clone();

            AddBinaryUpperBounds(workingModel, result);

            result.Notes.Add(
                "Step 1: Solve the LP relaxation using Primal Simplex.");

            var simplexSolver = new PrimalSimplexSolver();
            SolutionResult relaxation =
                simplexSolver.Solve(workingModel.Clone());

            CopyRelaxationIterations(relaxation, result);

            if (relaxation.Status != SolutionStatus.Optimal)
            {
                result.Status = relaxation.Status;
                result.Notes.Add(
                    "The LP relaxation did not produce an optimal solution.");

                foreach (string note in relaxation.Notes)
                {
                    result.Notes.Add(note);
                }

                return result;
            }

            if (relaxation.Iterations.Count == 0)
            {
                result.Notes.Add(
                    "The simplex solver did not return a final tableau.");

                return result;
            }

            Tableau currentTableau =
                CloneTableau(relaxation.Iterations.Last());

            result.Notes.Add(
                $"Initial relaxed objective value = " +
                $"{relaxation.ObjectiveValue:0.000}.");

            AddRelaxedVariableNotes(
                currentTableau,
                model.ObjectiveCoefficients.Length,
                result);

            int cutNumber = 0;

            while (cutNumber < MaximumCuts)
            {
                double[] currentValues = ExtractOriginalVariables(
                    currentTableau,
                    model.ObjectiveCoefficients.Length);

                if (IsIntegerFeasible(
                    currentValues,
                    model.SignRestrictions))
                {
                    result.Status = SolutionStatus.Optimal;
                    result.VariableValues =
                        RoundNearIntegers(currentValues);
                    result.ObjectiveValue =
                        currentTableau.Values![
                            0,
                            currentTableau.ColumnCount - 1];

                    result.Notes.Add(
                        "All integer and binary restrictions are satisfied.");
                    result.Notes.Add(
                        $"Final integer objective value = " +
                        $"{result.ObjectiveValue:0.000}.");

                    for (int i = 0;
                         i < result.VariableValues.Length;
                         i++)
                    {
                        result.Notes.Add(
                            $"x{i + 1} = " +
                            $"{result.VariableValues[i]:0.000}");
                    }

                    return result;
                }

                int sourceRow = FindFractionalSourceRow(
                    currentTableau,
                    model.SignRestrictions);

                if (sourceRow < 1)
                {
                    result.Notes.Add(
                        "A fractional integer variable was found, " +
                        "but no valid Gomory source row could be selected.");

                    return result;
                }

                cutNumber++;

                string sourceVariable =
                    currentTableau.BasicVariables![sourceRow - 1];

                double sourceRhs =
                    currentTableau.Values![
                        sourceRow,
                        currentTableau.ColumnCount - 1];

                double rhsFraction = FractionalPart(sourceRhs);

                result.Notes.Add(
                    $"Cut {cutNumber}: Selected row for " +
                    $"{sourceVariable}, whose RHS is " +
                    $"{sourceRhs:0.000}.");

                result.Notes.Add(
                    $"Fractional RHS = {rhsFraction:0.000}.");

                Tableau cutTableau = AddGomoryCut(
                    currentTableau,
                    sourceRow,
                    cutNumber,
                    result);

                result.Iterations.Add(CloneTableau(cutTableau));

                bool dualSucceeded = RunDualSimplex(
                    cutTableau,
                    cutNumber,
                    result);

                if (!dualSucceeded)
                {
                    result.Status = SolutionStatus.Infeasible;
                    result.Notes.Add(
                        $"The model became infeasible after Cut {cutNumber}.");

                    return result;
                }

                currentTableau = cutTableau;

                double objective =
                    currentTableau.Values![
                        0,
                        currentTableau.ColumnCount - 1];

                result.Notes.Add(
                    $"After Cut {cutNumber}, objective value = " +
                    $"{objective:0.000}.");

                AddCurrentVariableNotes(
                    currentTableau,
                    model.ObjectiveCoefficients.Length,
                    cutNumber,
                    result);
            }

            result.Notes.Add(
                $"Maximum number of cuts ({MaximumCuts}) reached.");

            return result;
        }

        private static bool ValidateModel(
            LPModel model,
            SolutionResult result)
        {
            if (model.Objective != ObjectiveType.Max)
            {
                result.Notes.Add(
                    "This Cutting Plane implementation requires a " +
                    "maximisation model.");

                return false;
            }

            if (model.ObjectiveCoefficients == null ||
                model.ObjectiveCoefficients.Length == 0)
            {
                result.Notes.Add(
                    "The model does not contain decision variables.");

                return false;
            }

            if (model.Constraints == null ||
                model.Constraints.Count == 0)
            {
                result.Notes.Add(
                    "The model does not contain constraints.");

                return false;
            }

            if (model.SignRestrictions == null ||
                model.SignRestrictions.Length !=
                model.ObjectiveCoefficients.Length)
            {
                result.Notes.Add(
                    "Every decision variable must have a valid " +
                    "sign restriction.");

                return false;
            }

            for (int i = 0;
                 i < model.SignRestrictions.Length;
                 i++)
            {
                SignRestriction restriction =
                    model.SignRestrictions[i];

                if (restriction != SignRestriction.Integer &&
                    restriction != SignRestriction.Binary)
                {
                    result.Notes.Add(
                        $"x{i + 1} is not an integer or binary variable. " +
                        "This implementation solves pure integer models.");

                    return false;
                }
            }

            for (int i = 0; i < model.Constraints.Count; i++)
            {
                Constraint constraint = model.Constraints[i];

                if (constraint.Relation !=
                    RelationType.LessOrEqual)
                {
                    result.Notes.Add(
                        $"Constraint {i + 1} does not use <=. " +
                        "This implementation requires <= constraints.");

                    return false;
                }

                if (constraint.Rhs < -Tolerance)
                {
                    result.Notes.Add(
                        $"Constraint {i + 1} has a negative RHS.");

                    return false;
                }

                if (constraint.Coefficients.Length !=
                    model.ObjectiveCoefficients.Length)
                {
                    result.Notes.Add(
                        $"Constraint {i + 1} has an incorrect " +
                        "number of coefficients.");

                    return false;
                }

                foreach (double coefficient
                         in constraint.Coefficients)
                {
                    if (!IsNearlyInteger(coefficient))
                    {
                        result.Notes.Add(
                            "Gomory fractional cutting planes require " +
                            "integer technological coefficients.");

                        return false;
                    }
                }

                if (!IsNearlyInteger(constraint.Rhs))
                {
                    result.Notes.Add(
                        "Gomory fractional cutting planes require " +
                        "integer right-hand-side values.");

                    return false;
                }
            }

            return true;
        }

        private static void AddBinaryUpperBounds(
            LPModel model,
            SolutionResult result)
        {
            int count = model.ObjectiveCoefficients.Length;

            for (int i = 0; i < count; i++)
            {
                if (model.SignRestrictions[i] !=
                    SignRestriction.Binary)
                {
                    continue;
                }

                double[] coefficients = new double[count];
                coefficients[i] = 1;

                model.Constraints.Add(
                    new Constraint(
                        coefficients,
                        RelationType.LessOrEqual,
                        1));

                result.Notes.Add(
                    $"Binary upper bound added: x{i + 1} <= 1.");
            }
        }

        private static void CopyRelaxationIterations(
            SolutionResult relaxation,
            SolutionResult result)
        {
            for (int i = 0;
                 i < relaxation.Iterations.Count;
                 i++)
            {
                Tableau copy =
                    CloneTableau(relaxation.Iterations[i]);

                copy.Label =
                    $"LP Relaxation - {relaxation.Iterations[i].Label}";

                result.Iterations.Add(copy);
            }
        }

        private static Tableau AddGomoryCut(
            Tableau source,
            int sourceRow,
            int cutNumber,
            SolutionResult result)
        {
            int oldRows = source.RowCount;
            int oldColumns = source.ColumnCount;

            int newRows = oldRows + 1;
            int newColumns = oldColumns + 1;

            int oldRhsColumn = oldColumns - 1;
            int newSlackColumn = oldColumns - 1;
            int newRhsColumn = newColumns - 1;

            double[,] values =
                new double[newRows, newColumns];

            string[] headers = new string[newColumns];
            string[] basicVariables = new string[newRows - 1];

            for (int column = 0;
                 column < oldRhsColumn;
                 column++)
            {
                headers[column] =
                    source.ColumnHeaders![column];

                for (int row = 0; row < oldRows; row++)
                {
                    values[row, column] =
                        source.Values![row, column];
                }
            }

            headers[newSlackColumn] = $"g{cutNumber}";
            headers[newRhsColumn] = "RHS";

            for (int row = 0; row < oldRows; row++)
            {
                values[row, newSlackColumn] = 0;
                values[row, newRhsColumn] =
                    source.Values![row, oldRhsColumn];
            }

            for (int i = 0;
                 i < source.BasicVariables!.Length;
                 i++)
            {
                basicVariables[i] =
                    source.BasicVariables[i];
            }

            int cutRow = newRows - 1;
            double rhs =
                source.Values![sourceRow, oldRhsColumn];

            var cutDescription = new StringBuilder();
            cutDescription.Append(
                $"Generated Gomory Cut {cutNumber}: ");

            bool firstTerm = true;

            for (int column = 0;
                 column < oldRhsColumn;
                 column++)
            {
                double coefficient =
                    FractionalPart(
                        source.Values[sourceRow, column]);

                values[cutRow, column] = -coefficient;

                if (coefficient > Tolerance)
                {
                    if (!firstTerm)
                    {
                        cutDescription.Append(" + ");
                    }

                    cutDescription.Append(
                        $"{coefficient:0.000}" +
                        $"{source.ColumnHeaders[column]}");

                    firstTerm = false;
                }
            }

            double rhsFraction = FractionalPart(rhs);

            values[cutRow, newSlackColumn] = 1;
            values[cutRow, newRhsColumn] = -rhsFraction;

            basicVariables[basicVariables.Length - 1] =
                $"g{cutNumber}";

            if (firstTerm)
            {
                cutDescription.Append("0");
            }

            cutDescription.Append(
                $" >= {rhsFraction:0.000}");

            result.Notes.Add(cutDescription.ToString());

            return new Tableau
            {
                Label =
                    $"Cut {cutNumber} Added - Before Dual Simplex",
                ColumnHeaders = headers,
                BasicVariables = basicVariables,
                Values = values
            };
        }

        private static bool RunDualSimplex(
            Tableau tableau,
            int cutNumber,
            SolutionResult result)
        {
            int pivotNumber = 0;

            while (pivotNumber < MaximumDualPivots)
            {
                int leavingRow =
                    SelectDualLeavingRow(tableau);

                if (leavingRow < 1)
                {
                    result.Notes.Add(
                        $"Dual Simplex completed for Cut {cutNumber}.");

                    return true;
                }

                int enteringColumn =
                    SelectDualEnteringColumn(
                        tableau,
                        leavingRow);

                if (enteringColumn < 0)
                {
                    result.Notes.Add(
                        $"No valid entering variable exists for " +
                        $"Cut {cutNumber}.");

                    return false;
                }

                string leavingVariable =
                    tableau.BasicVariables![leavingRow - 1];

                string enteringVariable =
                    tableau.ColumnHeaders![enteringColumn];

                pivotNumber++;

                result.Notes.Add(
                    $"Cut {cutNumber}, Dual Pivot {pivotNumber}: " +
                    $"{enteringVariable} enters and " +
                    $"{leavingVariable} leaves.");

                Pivot(
                    tableau,
                    leavingRow,
                    enteringColumn);

                tableau.Label =
                    $"Cut {cutNumber} - Dual Simplex Pivot " +
                    $"{pivotNumber}";

                result.Iterations.Add(
                    CloneTableau(tableau));
            }

            result.Notes.Add(
                $"Dual Simplex exceeded {MaximumDualPivots} pivots.");

            return false;
        }

        private static int SelectDualLeavingRow(
            Tableau tableau)
        {
            int rhsColumn = tableau.ColumnCount - 1;
            int leavingRow = -1;
            double mostNegativeRhs = -Tolerance;

            for (int row = 1;
                 row < tableau.RowCount;
                 row++)
            {
                double rhs =
                    tableau.Values![row, rhsColumn];

                if (rhs < mostNegativeRhs)
                {
                    mostNegativeRhs = rhs;
                    leavingRow = row;
                }
            }

            return leavingRow;
        }

        private static int SelectDualEnteringColumn(
            Tableau tableau,
            int leavingRow)
        {
            int rhsColumn = tableau.ColumnCount - 1;
            int enteringColumn = -1;
            double minimumRatio = double.PositiveInfinity;

            for (int column = 0;
                 column < rhsColumn;
                 column++)
            {
                double rowCoefficient =
                    tableau.Values![leavingRow, column];

                if (rowCoefficient < -Tolerance)
                {
                    double objectiveCoefficient =
                        tableau.Values[0, column];

                    double ratio =
                        objectiveCoefficient /
                        -rowCoefficient;

                    if (ratio >= -Tolerance &&
                        ratio < minimumRatio)
                    {
                        minimumRatio = ratio;
                        enteringColumn = column;
                    }
                }
            }

            return enteringColumn;
        }

        private static void Pivot(
            Tableau tableau,
            int pivotRow,
            int pivotColumn)
        {
            double pivotElement =
                tableau.Values![pivotRow, pivotColumn];

            for (int column = 0;
                 column < tableau.ColumnCount;
                 column++)
            {
                tableau.Values[pivotRow, column] /=
                    pivotElement;

                CleanSmallValue(
                    tableau.Values,
                    pivotRow,
                    column);
            }

            for (int row = 0;
                 row < tableau.RowCount;
                 row++)
            {
                if (row == pivotRow)
                {
                    continue;
                }

                double multiplier =
                    tableau.Values[row, pivotColumn];

                for (int column = 0;
                     column < tableau.ColumnCount;
                     column++)
                {
                    tableau.Values[row, column] -=
                        multiplier *
                        tableau.Values[pivotRow, column];

                    CleanSmallValue(
                        tableau.Values,
                        row,
                        column);
                }
            }

            tableau.BasicVariables![pivotRow - 1] =
                tableau.ColumnHeaders![pivotColumn];
        }

        private static int FindFractionalSourceRow(
            Tableau tableau,
            SignRestriction[] restrictions)
        {
            int selectedRow = -1;
            double largestFraction = Tolerance;

            for (int row = 1;
                 row < tableau.RowCount;
                 row++)
            {
                string basicVariable =
                    tableau.BasicVariables![row - 1];

                int variableIndex =
                    GetOriginalVariableIndex(basicVariable);

                if (variableIndex < 0 ||
                    variableIndex >= restrictions.Length)
                {
                    continue;
                }

                if (restrictions[variableIndex] !=
                        SignRestriction.Integer &&
                    restrictions[variableIndex] !=
                        SignRestriction.Binary)
                {
                    continue;
                }

                double rhs =
                    tableau.Values![
                        row,
                        tableau.ColumnCount - 1];

                double fraction =
                    FractionalPart(rhs);

                if (fraction > largestFraction &&
                    fraction < 1 - Tolerance)
                {
                    largestFraction = fraction;
                    selectedRow = row;
                }
            }

            return selectedRow;
        }

        private static double[] ExtractOriginalVariables(
            Tableau tableau,
            int variableCount)
        {
            double[] values = new double[variableCount];
            int rhsColumn = tableau.ColumnCount - 1;

            for (int i = 0; i < variableCount; i++)
            {
                string variableName = $"x{i + 1}";

                for (int row = 0;
                     row < tableau.BasicVariables!.Length;
                     row++)
                {
                    if (tableau.BasicVariables[row] ==
                        variableName)
                    {
                        values[i] =
                            tableau.Values![row + 1, rhsColumn];

                        break;
                    }
                }
            }

            return values;
        }

        private static bool IsIntegerFeasible(
            double[] values,
            SignRestriction[] restrictions)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (restrictions[i] ==
                        SignRestriction.Integer &&
                    !IsNearlyInteger(values[i]))
                {
                    return false;
                }

                if (restrictions[i] ==
                    SignRestriction.Binary)
                {
                    if (!IsNearlyInteger(values[i]) ||
                        values[i] < -Tolerance ||
                        values[i] > 1 + Tolerance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static double[] RoundNearIntegers(
            double[] values)
        {
            double[] rounded =
                (double[])values.Clone();

            for (int i = 0; i < rounded.Length; i++)
            {
                if (IsNearlyInteger(rounded[i]))
                {
                    rounded[i] =
                        Math.Round(rounded[i]);
                }
            }

            return rounded;
        }

        private static int GetOriginalVariableIndex(
            string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName) ||
                !variableName.StartsWith("x"))
            {
                return -1;
            }

            string number =
                variableName.Substring(1);

            if (int.TryParse(number, out int index))
            {
                return index - 1;
            }

            return -1;
        }

        private static double FractionalPart(
            double value)
        {
            double fraction =
                value - Math.Floor(value);

            if (fraction < Tolerance ||
                1 - fraction < Tolerance)
            {
                return 0;
            }

            return fraction;
        }

        private static bool IsNearlyInteger(
            double value)
        {
            return Math.Abs(
                value - Math.Round(value)) <= Tolerance;
        }

        private static void CleanSmallValue(
            double[,] values,
            int row,
            int column)
        {
            if (Math.Abs(values[row, column]) <
                Tolerance)
            {
                values[row, column] = 0;
            }
        }

        private static Tableau CloneTableau(
            Tableau source)
        {
            return new Tableau
            {
                Label = source.Label,
                ColumnHeaders =
                    (string[])source.ColumnHeaders!.Clone(),
                BasicVariables =
                    (string[])source.BasicVariables!.Clone(),
                Values =
                    (double[,])source.Values!.Clone()
            };
        }

        private static void AddRelaxedVariableNotes(
            Tableau tableau,
            int variableCount,
            SolutionResult result)
        {
            double[] values =
                ExtractOriginalVariables(
                    tableau,
                    variableCount);

            result.Notes.Add(
                "Initial LP relaxation variable values:");

            for (int i = 0; i < values.Length; i++)
            {
                result.Notes.Add(
                    $"x{i + 1} = {values[i]:0.000}");
            }
        }

        private static void AddCurrentVariableNotes(
            Tableau tableau,
            int variableCount,
            int cutNumber,
            SolutionResult result)
        {
            double[] values =
                ExtractOriginalVariables(
                    tableau,
                    variableCount);

            result.Notes.Add(
                $"Variable values after Cut {cutNumber}:");

            for (int i = 0; i < values.Length; i++)
            {
                result.Notes.Add(
                    $"x{i + 1} = {values[i]:0.000}");
            }
        }
    }
}