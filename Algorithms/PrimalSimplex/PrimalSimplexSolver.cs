using System;
using System.Collections.Generic;
using System.Text;
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
            // Warn when model contains integer/binary variables: we only solve the LP relaxation
            if (model.SignRestrictions != null)
            {
                for (int i = 0; i < model.SignRestrictions.Length; i++)
                {
                    if (model.SignRestrictions[i] == SignRestriction.Integer || model.SignRestrictions[i] == SignRestriction.Binary)
                    {
                        result.Notes.Add("Warning: model contains integer/binary variables — simplex will solve the LP relaxation only.");
                        break;
                    }
                }
            }

            // Work on a clone so we don't mutate the caller's model. Convert minimization
            // problems to maximization by negating objective coefficients so the rest of
            // the solver can assume a maximization problem.
            var workingModel = model.Clone();
            bool wasMin = false;
            if (workingModel.Objective == ObjectiveType.Min)
            {
                wasMin = true;
                workingModel.Objective = ObjectiveType.Max;
                for (int i = 0; i < workingModel.ObjectiveCoefficients.Length; i++)
                {
                    workingModel.ObjectiveCoefficients[i] = -workingModel.ObjectiveCoefficients[i];
                }
            }

            // Step 1: Convert to canonical form (standard form with slack/surplus variables)
            var canonicalData = BuildCanonicalForm(workingModel);

            // Step 2: Build initial tableau
            var tableau = BuildInitialTableau(canonicalData, workingModel.Objective);

            // Add the numeric initial tableau (iteration 0) and print it to console
            tableau.Label = "Initial Tableau (Iteration 0)";
            result.Iterations.Add(tableau);
            Console.WriteLine(tableau.ToString());

            // Print human-readable canonical equations (do not add as a tableau entry
            // so it doesn't count toward tableau iteration snapshots).
            Console.WriteLine("Canonical Form\n" + FormatCanonical(canonicalData));

            // Step 3: Simplex iterations
            int iteration = 0;
            int maxIterations = 10000;

            while (iteration < maxIterations)
            {
                // Check for optimality (reduced costs based on working objective)
                int enteringCol = SelectEnteringVariable(tableau, workingModel.Objective);

                if (enteringCol == -1)
                {
                    // Candidate optimality: check for leftover artificial basics with positive RHS
                    // which indicate infeasibility of the original LP.
                    int rhsCol = tableau.ColumnCount - 1;
                    var infeasibleArtificials = new List<string>();
                    for (int r = 0; r < tableau.BasicVariables.Length; r++)
                    {
                        var bv = tableau.BasicVariables[r];
                        if (!string.IsNullOrEmpty(bv) && bv.StartsWith("a", StringComparison.OrdinalIgnoreCase))
                        {
                            double rhsVal = tableau.Values[r + 1, rhsCol];
                            if (rhsVal > Epsilon)
                            {
                                infeasibleArtificials.Add($"{bv} (RHS={rhsVal:0.######})");
                            }
                        }
                    }

                    if (infeasibleArtificials.Count > 0)
                    {
                        result.Status = SolutionStatus.Infeasible;
                        result.Notes.Add("Problem is infeasible: artificial variable(s) remain positive in basis: " + string.Join(", ", infeasibleArtificials));
                        break;
                    }

                    // Optimal solution found
                    result.Status = SolutionStatus.Optimal;
                    ExtractSolution(tableau, canonicalData, result, workingModel);
                    // If we converted from a minimization problem, flip the objective sign back
                    if (wasMin)
                    {
                        result.ObjectiveValue = -result.ObjectiveValue;
                    }
                    // Add the optimal tableau snapshot with an explicit label
                    var optimalTableau = CloneTableau(tableau);
                    optimalTableau.Label = "Optimal Tableau";
                    result.Iterations.Add(optimalTableau);
                    Console.WriteLine(optimalTableau.ToString());
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

            // Objective row: compute reduced costs as c_B * A - c
            // c_B are the objective coefficients of the basic variables
            double[] cB = new double[data.NumConstraints];
            for (int r = 0; r < data.NumConstraints; r++)
            {
                int basicCol = data.BasicVariableIndices[r];
                cB[r] = data.ObjectiveCoeffs[basicCol];
            }

            for (int j = 0; j < data.TotalVars; j++)
            {
                double val = 0.0;
                for (int r = 0; r < data.NumConstraints; r++)
                {
                    val += cB[r] * data.ConstraintMatrix[r, j];
                }
                tableau.Values[0, j] = val - data.ObjectiveCoeffs[j];
            }
            // Objective RHS = c_B * x_B (x_B = RHS since basis is identity for slacks)
            double objRhs = 0.0;
            for (int r = 0; r < data.NumConstraints; r++)
            {
                objRhs += cB[r] * data.RightHandSide[r];
            }
            tableau.Values[0, cols - 1] = objRhs;

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

            // The reduced-cost computation above already makes the objective row
            // consistent with the basic variables (c_B * B^-1 * A - c), so no
            // extra adjustment is required here.

            return tableau;
        }

        private string FormatCanonical(CanonicalData data)
        {
            var sb = new StringBuilder();

            // Objective row: (z) - c1 x1 - c2 x2 ... = 0
            sb.Append("(z)\t");
            for (int i = 0; i < data.NumOriginalVars; i++)
            {
                var indices = data.OriginalVarMap[i];
                double coeff = data.OriginalModel.ObjectiveCoefficients[i];
                if (i > 0) sb.Append("\t");
                if (coeff >= 0)
                    sb.AppendFormat("-\t{0}x{1}", coeff, i + 1);
                else
                    sb.AppendFormat("+\t{0}x{1}", Math.Abs(coeff), i + 1);
            }
            sb.Append("\t=\t0\n");

            // Constraints
            int totalCols = data.TotalVars;
            int slackStart = data.NumOriginalVars + data.NumExtraVars;

            for (int r = 0; r < data.NumConstraints; r++)
            {
                var terms = new List<string>();
                // original variables
                for (int i = 0; i < data.NumOriginalVars; i++)
                {
                    var indices = data.OriginalVarMap[i];
                    double coeff = data.ConstraintMatrix[r, indices[0]]; // use x+ column for unrestricted
                    if (Math.Abs(coeff) > 1e-12)
                    {
                        string term = (coeff == (int)coeff) ? string.Format("{0}x{1}", (int)coeff, i + 1)
                            : string.Format("{0}x{1}", coeff, i + 1);
                        terms.Add(term);
                    }
                }

                // slack variables
                for (int s = 0; s < data.NumSlack; s++)
                {
                    int col = slackStart + s;
                    double coeff = data.ConstraintMatrix[r, col];
                    if (Math.Abs(coeff) > 1e-12)
                    {
                        string term = string.Format("{0}s{1}", coeff, s + 1);
                        terms.Add(term);
                    }
                }

                if (terms.Count == 0)
                    sb.Append("0");
                else
                    sb.Append(string.Join("\t+\t", terms));

                sb.AppendFormat("\t=\t{0}\n", data.RightHandSide[r]);
            }

            return sb.ToString();
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
