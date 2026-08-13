using System;
using System.Collections.Generic;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.PrimalSimplex
{
    /// <summary>
    /// OWNER: Person 1 (Core App + Simplex)
    /// Implements the Primal Simplex algorithm with full tableau method.
    /// Converts the LP to canonical form and displays all tableau iterations.
    /// </summary>
    public class PrimalSimplexSolver : IAlgorithm
    {
        public string Name => "Primal Simplex";

        private const double Epsilon = 1e-9;

        public SolutionResult Solve(LPModel model)
        {
            var result = new SolutionResult
            {
                AlgorithmName = Name,
                Status = SolutionStatus.NotSolved
            };

            // Step 1: Convert to canonical form (standard form with slack/surplus variables)
            var canonicalData = BuildCanonicalForm(model);

            // Step 2: Build initial tableau
            var tableau = BuildInitialTableau(canonicalData, model.Objective);
            result.Iterations.Add(tableau);

            // Step 3: Simplex iterations
            int iteration = 0;
            int maxIterations = 10000;

            while (iteration < maxIterations)
            {
                // Check for optimality (all reduced costs >= 0 for max, <= 0 for min)
                int enteringCol = SelectEnteringVariable(tableau, model.Objective);

                if (enteringCol == -1)
                {
                    // Optimal solution found
                    result.Status = SolutionStatus.Optimal;
                    ExtractSolution(tableau, canonicalData, result, model);
                    break;
                }

                // Minimum ratio test to find leaving variable
                int leavingRow = SelectLeavingVariable(tableau, enteringCol);

                if (leavingRow == -1)
                {
                    // Unbounded
                    result.Status = SolutionStatus.Unbounded;
                    result.Notes.Add("Problem is unbounded - no valid leaving variable in ratio test.");
                    break;
                }

                // Pivot
                Pivot(tableau, leavingRow, enteringCol);

                iteration++;
                var iterTableau = CloneTableau(tableau);
                iterTableau.Label = $"Iteration {iteration}";
                result.Iterations.Add(iterTableau);
            }

            if (iteration >= maxIterations)
            {
                result.Notes.Add($"Maximum iterations ({maxIterations}) reached without convergence.");
            }

            return result;
        }

        private CanonicalData BuildCanonicalForm(LPModel model)
        {
            var data = new CanonicalData();
            int numOriginalVars = model.ObjectiveCoefficients.Length;
            int numConstraints = model.Constraints.Count;

            // Count slack/surplus/artificial variables needed
            int numSlack = 0;
            int numArtificial = 0;

            for (int i = 0; i < numConstraints; i++)
            {
                var constraint = model.Constraints[i];
                if (constraint.Relation == RelationType.LessOrEqual)
                {
                    numSlack++;
                }
                else if (constraint.Relation == RelationType.GreaterOrEqual)
                {
                    numSlack++; // surplus variable
                    numArtificial++; // need artificial for >=
                }
                else // Equal
                {
                    numArtificial++;
                }
            }

            // Handle unrestricted variables by splitting into x+ - x-
            int numExtraVars = 0;
            for (int i = 0; i < numOriginalVars; i++)
            {
                if (model.SignRestrictions[i] == SignRestriction.Unrestricted)
                {
                    numExtraVars++;
                }
            }

            int totalVars = numOriginalVars + numExtraVars + numSlack + numArtificial;

            // Initialize objective coefficients (maximization)
            data.ObjectiveCoeffs = new double[totalVars];
            int colIndex = 0;

            // Original variables
            for (int i = 0; i < numOriginalVars; i++)
            {
                if (model.SignRestrictions[i] == SignRestriction.Unrestricted)
                {
                    // Split x_i into x_i+ - x_i-
                    data.ObjectiveCoeffs[colIndex++] = model.ObjectiveCoefficients[i];
                    data.ObjectiveCoeffs[colIndex++] = -model.ObjectiveCoefficients[i];
                    data.OriginalVarMap.Add(i, new int[] { colIndex - 2, colIndex - 1 });
                }
                else
                {
                    data.ObjectiveCoeffs[colIndex++] = model.ObjectiveCoefficients[i];
                    data.OriginalVarMap.Add(i, new int[] { colIndex - 1 });
                }
            }

            // Slack/surplus variables (coefficient 0 in objective)
            for (int i = 0; i < numSlack; i++)
            {
                data.ObjectiveCoeffs[colIndex++] = 0;
            }

            // Artificial variables (use Big-M penalty)
            for (int i = 0; i < numArtificial; i++)
            {
                data.ObjectiveCoeffs[colIndex++] = model.Objective == ObjectiveType.Max ? -1e10 : 1e10;
            }

            // Build constraint matrix
            data.ConstraintMatrix = new double[numConstraints, totalVars];
            data.RightHandSide = new double[numConstraints];
            data.BasicVariables = new string[numConstraints];
            data.BasicVariableIndices = new int[numConstraints];

            int slackIndex = numOriginalVars + numExtraVars;
            int artificialIndex = numOriginalVars + numExtraVars + numSlack;

            for (int row = 0; row < numConstraints; row++)
            {
                var constraint = model.Constraints[row];
                double rhs = constraint.Rhs;

                // Make RHS non-negative
                double signMultiplier = 1.0;
                if (rhs < 0)
                {
                    signMultiplier = -1.0;
                    rhs = -rhs;
                }

                // Fill in original variable coefficients
                colIndex = 0;
                for (int i = 0; i < numOriginalVars; i++)
                {
                    double coeff = constraint.Coefficients[i] * signMultiplier;
                    if (model.SignRestrictions[i] == SignRestriction.Unrestricted)
                    {
                        data.ConstraintMatrix[row, colIndex++] = coeff;  // x+
                        data.ConstraintMatrix[row, colIndex++] = -coeff; // x-
                    }
                    else
                    {
                        data.ConstraintMatrix[row, colIndex++] = coeff;
                    }
                }

                // Add slack/surplus and artificial variables
                if (constraint.Relation == RelationType.LessOrEqual)
                {
                    data.ConstraintMatrix[row, slackIndex] = 1.0 * signMultiplier;
                    data.BasicVariables[row] = $"s{row + 1}";
                    data.BasicVariableIndices[row] = slackIndex;
                    slackIndex++;
                }
                else if (constraint.Relation == RelationType.GreaterOrEqual)
                {
                    data.ConstraintMatrix[row, slackIndex] = -1.0 * signMultiplier; // surplus
                    data.ConstraintMatrix[row, artificialIndex] = 1.0; // artificial
                    data.BasicVariables[row] = $"a{row + 1}";
                    data.BasicVariableIndices[row] = artificialIndex;
                    slackIndex++;
                    artificialIndex++;
                }
                else // Equal
                {
                    data.ConstraintMatrix[row, artificialIndex] = 1.0; // artificial
                    data.BasicVariables[row] = $"a{row + 1}";
                    data.BasicVariableIndices[row] = artificialIndex;
                    artificialIndex++;
                }

                data.RightHandSide[row] = rhs;
            }

            data.NumOriginalVars = numOriginalVars;
            data.NumExtraVars = numExtraVars;
            data.NumSlack = numSlack;
            data.NumArtificial = numArtificial;
            data.TotalVars = totalVars;
            data.NumConstraints = numConstraints;
            data.OriginalModel = model;

            return data;
        }

        private Tableau BuildInitialTableau(CanonicalData data, ObjectiveType objective)
        {
            int rows = data.NumConstraints + 1; // objective row + constraint rows
            int cols = data.TotalVars + 1; // variables + RHS

            var tableau = new Tableau
            {
                Values = new double[rows, cols],
                ColumnHeaders = new string[cols],
                BasicVariables = new string[data.NumConstraints],
                Label = "Canonical Form"
            };

            // Set column headers
            int col = 0;
            int varNum = 1;
            for (int i = 0; i < data.NumOriginalVars; i++)
            {
                if (data.OriginalModel.SignRestrictions[i] == SignRestriction.Unrestricted)
                {
                    tableau.ColumnHeaders[col++] = $"x{varNum}+";
                    tableau.ColumnHeaders[col++] = $"x{varNum}-";
                }
                else
                {
                    tableau.ColumnHeaders[col++] = $"x{varNum}";
                }
                varNum++;
            }

            for (int i = 0; i < data.NumSlack; i++)
                tableau.ColumnHeaders[col++] = $"s{i + 1}";
            for (int i = 0; i < data.NumArtificial; i++)
                tableau.ColumnHeaders[col++] = $"a{i + 1}";
            tableau.ColumnHeaders[col] = "RHS";

            // Objective row (row 0)
            // For maximization: z - c1*x1 - c2*x2 - ... = 0
            // For minimization: z + c1*x1 + c2*x2 + ... = 0 (but we convert to max by negating)
            for (int c = 0; c < data.TotalVars; c++)
            {
                double objCoeff = data.ObjectiveCoeffs[c];
                if (objective == ObjectiveType.Max)
                {
                    tableau.Values[0, c] = -objCoeff; // negative for max
                }
                else
                {
                    tableau.Values[0, c] = objCoeff; // positive for min
                }
            }
            tableau.Values[0, cols - 1] = 0; // RHS of objective

            // Constraint rows
            for (int r = 0; r < data.NumConstraints; r++)
            {
                for (int c = 0; c < data.TotalVars; c++)
                {
                    tableau.Values[r + 1, c] = data.ConstraintMatrix[r, c];
                }
                tableau.Values[r + 1, cols - 1] = data.RightHandSide[r];
                tableau.BasicVariables[r] = data.BasicVariables[r];
            }

            // Make objective row consistent with basic variables
            // Subtract appropriate multiples of constraint rows from objective row
            for (int r = 0; r < data.NumConstraints; r++)
            {
                int basicCol = data.BasicVariableIndices[r];
                if (Math.Abs(tableau.Values[0, basicCol]) > Epsilon)
                {
                    double multiplier = tableau.Values[0, basicCol];
                    for (int c = 0; c < cols; c++)
                    {
                        tableau.Values[0, c] -= multiplier * tableau.Values[r + 1, c];
                    }
                }
            }

            return tableau;
        }

        private int SelectEnteringVariable(Tableau tableau, ObjectiveType objective)
        {
            int enteringCol = -1;
            double bestValue = 0;

            // For maximization: choose most negative reduced cost
            // For minimization: choose most positive reduced cost (or use negative of min)
            for (int c = 0; c < tableau.ColumnCount - 1; c++) // exclude RHS
            {
                double reducedCost = tableau.Values[0, c];

                if (objective == ObjectiveType.Max)
                {
                    if (reducedCost < -Epsilon && reducedCost < bestValue)
                    {
                        bestValue = reducedCost;
                        enteringCol = c;
                    }
                }
                else
                {
                    if (reducedCost > Epsilon && reducedCost > bestValue)
                    {
                        bestValue = reducedCost;
                        enteringCol = c;
                    }
                }
            }

            return enteringCol;
        }

        private int SelectLeavingVariable(Tableau tableau, int enteringCol)
        {
            int leavingRow = -1;
            double minRatio = double.MaxValue;

            for (int r = 1; r < tableau.RowCount; r++) // skip objective row
            {
                double denominator = tableau.Values[r, enteringCol];
                if (denominator > Epsilon)
                {
                    double ratio = tableau.Values[r, tableau.ColumnCount - 1] / denominator;
                    if (ratio >= 0 && ratio < minRatio)
                    {
                        minRatio = ratio;
                        leavingRow = r;
                    }
                }
            }

            return leavingRow;
        }

        private void Pivot(Tableau tableau, int pivotRow, int pivotCol)
        {
            double pivotElement = tableau.Values[pivotRow, pivotCol];

            // Normalize pivot row
            for (int c = 0; c < tableau.ColumnCount; c++)
            {
                tableau.Values[pivotRow, c] /= pivotElement;
            }

            // Update other rows
            for (int r = 0; r < tableau.RowCount; r++)
            {
                if (r != pivotRow)
                {
                    double multiplier = tableau.Values[r, pivotCol];
                    for (int c = 0; c < tableau.ColumnCount; c++)
                    {
                        tableau.Values[r, c] -= multiplier * tableau.Values[pivotRow, c];
                    }
                }
            }

            // Update basic variable
            tableau.BasicVariables[pivotRow - 1] = tableau.ColumnHeaders[pivotCol];
        }

        private Tableau CloneTableau(Tableau source)
        {
            var clone = new Tableau
            {
                Label = source.Label,
                ColumnHeaders = (string[])source.ColumnHeaders.Clone(),
                BasicVariables = (string[])source.BasicVariables.Clone(),
                Values = (double[,])source.Values.Clone()
            };
            return clone;
        }

        private void ExtractSolution(Tableau tableau, CanonicalData data, SolutionResult result, LPModel model)
        {
            int numVars = model.ObjectiveCoefficients.Length;
            result.VariableValues = new double[numVars];

            // Extract variable values from the tableau
            for (int i = 0; i < numVars; i++)
            {
                double value = 0;

                if (model.SignRestrictions[i] == SignRestriction.Unrestricted)
                {
                    // x = x+ - x-
                    var indices = data.OriginalVarMap[i];
                    double xPlus = GetVariableValue(tableau, indices[0]);
                    double xMinus = GetVariableValue(tableau, indices[1]);
                    value = xPlus - xMinus;
                }
                else
                {
                    var indices = data.OriginalVarMap[i];
                    value = GetVariableValue(tableau, indices[0]);
                }

                result.VariableValues[i] = value;
            }

            // Objective value (with sign adjustment for minimization)
            result.ObjectiveValue = tableau.Values[0, tableau.ColumnCount - 1];
            if (model.Objective == ObjectiveType.Min)
            {
                result.ObjectiveValue = -result.ObjectiveValue;
            }
        }

        private double GetVariableValue(Tableau tableau, int colIndex)
        {
            // Check if variable is basic
            for (int r = 0; r < tableau.BasicVariables.Length; r++)
            {
                if (tableau.ColumnHeaders[colIndex] == tableau.BasicVariables[r])
                {
                    return tableau.Values[r + 1, tableau.ColumnCount - 1];
                }
            }
            // Non-basic variable = 0
            return 0;
        }

        private class CanonicalData
        {
            public double[] ObjectiveCoeffs;
            public double[,] ConstraintMatrix;
            public double[] RightHandSide;
            public string[] BasicVariables;
            public int[] BasicVariableIndices;
            public Dictionary<int, int[]> OriginalVarMap = new Dictionary<int, int[]>();
            public int NumOriginalVars;
            public int NumExtraVars;
            public int NumSlack;
            public int NumArtificial;
            public int TotalVars;
            public int NumConstraints;
            public LPModel OriginalModel;
        }
    }
}
