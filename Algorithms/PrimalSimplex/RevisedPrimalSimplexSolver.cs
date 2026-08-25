using System;
using System.Collections.Generic;
using System.Text;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.PrimalSimplex
{
    /// <summary>
    /// OWNER: Person 1 (Core App + Simplex)
    /// Implements the Revised Primal Simplex algorithm using the product form
    /// of the inverse. More efficient for large sparse problems as it only
    /// maintains the basis inverse rather than the full tableau.
    /// </summary>
    public class RevisedPrimalSimplexSolver : IAlgorithm
    {
        public string Name => "Revised Primal Simplex";

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

            // Build canonical form data using working model
            var data = BuildCanonicalForm(workingModel);

            // Initialize basis inverse (B^-1) as identity
            int basisSize = data.NumConstraints;
            double[,] basisInverse = new double[basisSize, basisSize];
            for (int i = 0; i < basisSize; i++)
                basisInverse[i, i] = 1.0;

            // Track basic variable indices
            int[] basicVars = (int[])data.BasicVariableIndices.Clone();

            // Store initial tableau for display
            var initialTableau = BuildDisplayTableau(data, basisInverse, basicVars, "Initial Basis (B = I)");
            result.Iterations.Add(initialTableau);

            // Main simplex loop
            int iteration = 0;
            int maxIterations = 10000;

            while (iteration < maxIterations)
            {
                // Compute current RHS: B^-1 * b (needed for infeasibility checks and ratio tests)
                double[] currentRHS = new double[basisSize];
                for (int i = 0; i < basisSize; i++)
                {
                    for (int j = 0; j < basisSize; j++)
                    {
                        currentRHS[i] += basisInverse[i, j] * data.RightHandSide[j];
                    }
                }

                // Compute reduced costs: c_B * B^-1 * A - c (use working objective)
                int enteringCol = SelectEnteringVariable(data, basisInverse, basicVars, workingModel.Objective);

                if (enteringCol == -1)
                {
                    // Candidate optimal: check for artificial basics with positive RHS
                    var infeasibleArtificials = new List<string>();
                    int artificialStart = data.TotalVars - data.NumArtificial;
                    for (int i = 0; i < basisSize; i++)
                    {
                        int bvIndex = basicVars[i];
                        if (data.NumArtificial > 0 && bvIndex >= artificialStart)
                        {
                            double rhsVal = currentRHS[i];
                            if (rhsVal > Epsilon)
                            {
                                string name = GetVariableName(bvIndex, data);
                                infeasibleArtificials.Add($"{name} (RHS={rhsVal:0.######})");
                            }
                        }
                    }

                    if (infeasibleArtificials.Count > 0)
                    {
                        result.Status = SolutionStatus.Infeasible;
                        result.Notes.Add("Problem is infeasible: artificial variable(s) remain positive in basis: " + string.Join(", ", infeasibleArtificials));
                        break;
                    }

                    // Optimal
                    result.Status = SolutionStatus.Optimal;
                    ExtractSolution(data, basisInverse, basicVars, result, workingModel);
                    if (wasMin)
                    {
                        result.ObjectiveValue = -result.ObjectiveValue;
                    }
                    break;
                }

                // Compute entering column: B^-1 * A_j
                double[] enteringColumn = new double[basisSize];
                for (int i = 0; i < basisSize; i++)
                {
                    for (int j = 0; j < basisSize; j++)
                    {
                        enteringColumn[i] += basisInverse[i, j] * data.ConstraintMatrix[j, enteringCol];
                    }
                }

                // Minimum ratio test
                int leavingRow = -1;
                double minRatio = double.MaxValue;
                for (int i = 0; i < basisSize; i++)
                {
                    if (enteringColumn[i] > Epsilon)
                    {
                        double ratio = currentRHS[i] / enteringColumn[i];
                        if (ratio >= 0 && ratio < minRatio)
                        {
                            minRatio = ratio;
                            leavingRow = i;
                        }
                    }
                }

                if (leavingRow == -1)
                {
                    result.Status = SolutionStatus.Unbounded;
                    result.Notes.Add("Problem is unbounded - no valid leaving variable.");
                    break;
                }

                // Update basis inverse using product form
                UpdateBasisInverse(basisInverse, enteringColumn, leavingRow);

                // Update basic variable
                basicVars[leavingRow] = enteringCol;

                iteration++;

                // Store iteration tableau
                var iterTableau = BuildDisplayTableau(data, basisInverse, basicVars, $"Iteration {iteration}");
                result.Iterations.Add(iterTableau);
            }

            if (iteration >= maxIterations)
            {
                result.Notes.Add($"Maximum iterations ({maxIterations}) reached.");
            }

            return result;
        }

        private CanonicalData BuildCanonicalForm(LPModel model)
        {
            var data = new CanonicalData();
            int numOriginalVars = model.ObjectiveCoefficients.Length;
            int numConstraints = model.Constraints.Count;

            int numSlack = 0;
            int numArtificial = 0;

            for (int i = 0; i < numConstraints; i++)
            {
                var constraint = model.Constraints[i];
                if (constraint.Relation == RelationType.LessOrEqual)
                    numSlack++;
                else if (constraint.Relation == RelationType.GreaterOrEqual)
                {
                    numSlack++;
                    numArtificial++;
                }
                else
                    numArtificial++;
            }

            int numExtraVars = 0;
            for (int i = 0; i < numOriginalVars; i++)
            {
                if (model.SignRestrictions[i] == SignRestriction.Unrestricted)
                    numExtraVars++;
            }

            int totalVars = numOriginalVars + numExtraVars + numSlack + numArtificial;

            data.ObjectiveCoeffs = new double[totalVars];
            int colIndex = 0;

            for (int i = 0; i < numOriginalVars; i++)
            {
                if (model.SignRestrictions[i] == SignRestriction.Unrestricted)
                {
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

            for (int i = 0; i < numSlack; i++)
                data.ObjectiveCoeffs[colIndex++] = 0;
            for (int i = 0; i < numArtificial; i++)
                data.ObjectiveCoeffs[colIndex++] = model.Objective == ObjectiveType.Max ? -1e10 : 1e10;

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
                double signMultiplier = rhs < 0 ? -1.0 : 1.0;
                rhs = Math.Abs(rhs);

                colIndex = 0;
                for (int i = 0; i < numOriginalVars; i++)
                {
                    double coeff = constraint.Coefficients[i] * signMultiplier;
                    if (model.SignRestrictions[i] == SignRestriction.Unrestricted)
                    {
                        data.ConstraintMatrix[row, colIndex++] = coeff;
                        data.ConstraintMatrix[row, colIndex++] = -coeff;
                    }
                    else
                    {
                        data.ConstraintMatrix[row, colIndex++] = coeff;
                    }
                }

                if (constraint.Relation == RelationType.LessOrEqual)
                {
                    data.ConstraintMatrix[row, slackIndex] = 1.0 * signMultiplier;
                    data.BasicVariables[row] = $"s{row + 1}";
                    data.BasicVariableIndices[row] = slackIndex;
                    slackIndex++;
                }
                else if (constraint.Relation == RelationType.GreaterOrEqual)
                {
                    data.ConstraintMatrix[row, slackIndex] = -1.0 * signMultiplier;
                    data.ConstraintMatrix[row, artificialIndex] = 1.0;
                    data.BasicVariables[row] = $"a{row + 1}";
                    data.BasicVariableIndices[row] = artificialIndex;
                    slackIndex++;
                    artificialIndex++;
                }
                else
                {
                    data.ConstraintMatrix[row, artificialIndex] = 1.0;
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

        private int SelectEnteringVariable(CanonicalData data, double[,] basisInverse, int[] basicVars, ObjectiveType objective)
        {
            int basisSize = data.NumConstraints;

            // Compute c_B (costs of basic variables)
            double[] cB = new double[basisSize];
            for (int i = 0; i < basisSize; i++)
                cB[i] = data.ObjectiveCoeffs[basicVars[i]];

            // Compute c_B * B^-1 (simplex multipliers / dual variables)
            double[] simplexMultipliers = new double[basisSize];
            for (int j = 0; j < basisSize; j++)
            {
                for (int i = 0; i < basisSize; i++)
                {
                    simplexMultipliers[j] += cB[i] * basisInverse[i, j];
                }
            }

            // Compute reduced costs for all non-basic variables
            int enteringCol = -1;
            double bestValue = 0;

            for (int j = 0; j < data.TotalVars; j++)
            {
                // Check if j is basic
                bool isBasic = false;
                for (int i = 0; i < basisSize; i++)
                {
                    if (basicVars[i] == j)
                    {
                        isBasic = true;
                        break;
                    }
                }

                if (!isBasic)
                {
                    // Reduced cost = c_j - c_B * B^-1 * A_j
                    double[] aCol = new double[basisSize];
                    for (int i = 0; i < basisSize; i++)
                        aCol[i] = data.ConstraintMatrix[i, j];

                    double reducedCost = data.ObjectiveCoeffs[j];
                    for (int i = 0; i < basisSize; i++)
                    {
                        reducedCost -= simplexMultipliers[i] * aCol[i];
                    }

                    if (objective == ObjectiveType.Max)
                    {
                        // For maximization: enter variable with most POSITIVE reduced cost
                        if (reducedCost > Epsilon && reducedCost > bestValue)
                        {
                            bestValue = reducedCost;
                            enteringCol = j;
                        }
                    }
                    else
                    {
                        // For minimization: enter variable with most NEGATIVE reduced cost
                        if (reducedCost < -Epsilon && reducedCost < bestValue)
                        {
                            bestValue = reducedCost;
                            enteringCol = j;
                        }
                    }
                }
            }

            return enteringCol;
        }

        private void UpdateBasisInverse(double[,] basisInverse, double[] enteringColumn, int leavingRow)
        {
            int n = basisInverse.GetLength(0);

            // Compute eta vector
            double pivot = enteringColumn[leavingRow];
            double[] eta = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (i == leavingRow)
                    eta[i] = 1.0 / pivot;
                else
                    eta[i] = -enteringColumn[i] / pivot;
            }

            // Update B^-1 using product form: B^-1_new = E * B^-1
            // where E is the eta matrix
            double[,] newInverse = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == leavingRow)
                        newInverse[i, j] = eta[i] * basisInverse[leavingRow, j];
                    else
                        newInverse[i, j] = basisInverse[i, j] + eta[i] * basisInverse[leavingRow, j];
                }
            }

            // Copy back
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    basisInverse[i, j] = newInverse[i, j];
        }

        private Tableau BuildDisplayTableau(CanonicalData data, double[,] basisInverse, int[] basicVars, string label)
        {
            int basisSize = data.NumConstraints;

            // Compute current solution values
            double[] xB = new double[basisSize];
            for (int i = 0; i < basisSize; i++)
            {
                for (int j = 0; j < basisSize; j++)
                {
                    xB[i] += basisInverse[i, j] * data.RightHandSide[j];
                }
            }

            // Build a complete tableau for display
            int rows = basisSize + 1;
            int cols = data.TotalVars + 1;

            var tableau = new Tableau
            {
                Values = new double[rows, cols],
                ColumnHeaders = new string[cols],
                BasicVariables = new string[basisSize],
                Label = label
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

            // Compute reduced costs for objective row
            double[] cB = new double[basisSize];
            for (int i = 0; i < basisSize; i++)
                cB[i] = data.ObjectiveCoeffs[basicVars[i]];

            double[] simplexMultipliers = new double[basisSize];
            for (int j = 0; j < basisSize; j++)
                for (int i = 0; i < basisSize; i++)
                    simplexMultipliers[j] += cB[i] * basisInverse[i, j];

            // Objective row
            double objValue = 0;
            for (int j = 0; j < data.TotalVars; j++)
            {
                double reducedCost = data.ObjectiveCoeffs[j];
                for (int i = 0; i < basisSize; i++)
                    reducedCost -= simplexMultipliers[i] * data.ConstraintMatrix[i, j];
                tableau.Values[0, j] = reducedCost;

                // Add contribution to objective from basic variables
                for (int i = 0; i < basisSize; i++)
                {
                    if (basicVars[i] == j)
                    {
                        objValue += data.ObjectiveCoeffs[j] * xB[i];
                        break;
                    }
                }
            }
            tableau.Values[0, cols - 1] = objValue;

            // Constraint rows
            for (int i = 0; i < basisSize; i++)
            {
                // Compute B^-1 * A for each column
                for (int j = 0; j < data.TotalVars; j++)
                {
                    double val = 0;
                    for (int k = 0; k < basisSize; k++)
                        val += basisInverse[i, k] * data.ConstraintMatrix[k, j];
                    tableau.Values[i + 1, j] = val;
                }
                tableau.Values[i + 1, cols - 1] = xB[i];
                tableau.BasicVariables[i] = GetVariableName(basicVars[i], data);
            }

            return tableau;
        }

        private string GetVariableName(int colIndex, CanonicalData data)
        {
            int col = 0;
            int varNum = 1;
            for (int i = 0; i < data.NumOriginalVars; i++)
            {
                if (data.OriginalModel.SignRestrictions[i] == SignRestriction.Unrestricted)
                {
                    if (col == colIndex) return $"x{varNum}+";
                    col++;
                    if (col == colIndex) return $"x{varNum}-";
                    col++;
                }
                else
                {
                    if (col == colIndex) return $"x{varNum}";
                    col++;
                }
                varNum++;
            }
            for (int i = 0; i < data.NumSlack; i++)
            {
                if (col == colIndex) return $"s{i + 1}";
                col++;
            }
            for (int i = 0; i < data.NumArtificial; i++)
            {
                if (col == colIndex) return $"a{i + 1}";
                col++;
            }
            return "?";
        }

        private void ExtractSolution(CanonicalData data, double[,] basisInverse, int[] basicVars, SolutionResult result, LPModel model)
        {
            int basisSize = data.NumConstraints;

            // Compute solution
            double[] xB = new double[basisSize];
            for (int i = 0; i < basisSize; i++)
            {
                for (int j = 0; j < basisSize; j++)
                {
                    xB[i] += basisInverse[i, j] * data.RightHandSide[j];
                }
            }

            // Build full solution vector
            double[] fullSolution = new double[data.TotalVars];
            for (int i = 0; i < basisSize; i++)
            {
                fullSolution[basicVars[i]] = xB[i];
            }

            // Extract original variable values
            int numVars = model.ObjectiveCoefficients.Length;
            result.VariableValues = new double[numVars];

            for (int i = 0; i < numVars; i++)
            {
                if (model.SignRestrictions[i] == SignRestriction.Unrestricted)
                {
                    var indices = data.OriginalVarMap[i];
                    result.VariableValues[i] = fullSolution[indices[0]] - fullSolution[indices[1]];
                }
                else
                {
                    var indices = data.OriginalVarMap[i];
                    result.VariableValues[i] = fullSolution[indices[0]];
                }
            }

            // Objective value
            result.ObjectiveValue = 0;
            for (int i = 0; i < basisSize; i++)
            {
                result.ObjectiveValue += data.ObjectiveCoeffs[basicVars[i]] * xB[i];
            }

            if (model.Objective == ObjectiveType.Min)
            {
                result.ObjectiveValue = -result.ObjectiveValue;
            }
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
