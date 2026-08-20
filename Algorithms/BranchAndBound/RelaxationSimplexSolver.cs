using System;
using System.Collections.Generic;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.BranchAndBound
{
    /// <summary>
    /// Two-phase Simplex solver used for Branch and Bound LP relaxations.
    /// </summary>
    public sealed class RelaxationSimplexSolver : IAlgorithm
    {
        public string Name
        {
            get { return "LP Relaxation (Two-Phase Simplex)"; }
        }

        private const double Eps = 1e-9;
        private const double RatioTieTolerance = 1e-12;
        private const int MaxIterations = 5000;

        /// <summary>Controls whether tableau iterations are saved.</summary>
        public bool RecordIterations { get; set; } = true;

        public SolutionResult Solve(LPModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var result = new SolutionResult
            {
                AlgorithmName = Name,
                Status = SolutionStatus.NotSolved
            };

            var form = BuildCanonicalForm(model);

            // Phase 1: make the artificial variables zero.
            if (form.ArtificialColumns.Count > 0)
            {
                // Maximise the negative sum of the artificial variables.
                var phaseOneObjective = new double[form.N];
                foreach (int artificialColumn in form.ArtificialColumns)
                {
                    phaseOneObjective[artificialColumn] = -1.0;
                }

                form.BuildObjectiveRow(phaseOneObjective);
                Snapshot(form, result, "Phase 1 - Canonical Form");

                var allColumns = new bool[form.N];
                for (int j = 0; j < form.N; j++) allColumns[j] = true;

                SimplexOutcome phaseOne = RunSimplex(form, allColumns, result, "Phase 1");

                if (phaseOne == SimplexOutcome.IterationLimit)
                {
                    result.Notes.Add("Phase 1 hit the iteration limit; the relaxation may be cycling.");
                    return result;
                }

                // A negative result means the model is infeasible.
                if (form.T[0, form.N] < -1e-7)
                {
                    result.Status = SolutionStatus.Infeasible;
                    result.Notes.Add("Phase 1 could not drive every artificial variable to zero - the model is infeasible.");
                    return result;
                }

                DriveArtificialsOutOfBasis(form);
            }

            // Phase 2 - solve the original objective.
            form.BuildObjectiveRow(form.MaxFormObjective);
            Snapshot(form, result, form.ArtificialColumns.Count > 0 ? "Phase 2 - Canonical Form" : "Canonical Form");

            // Do not allow artificial variables to enter in Phase 2.
            var phaseTwoColumns = new bool[form.N];
            for (int j = 0; j < form.N; j++) phaseTwoColumns[j] = true;
            foreach (int artificialColumn in form.ArtificialColumns) phaseTwoColumns[artificialColumn] = false;

            string phaseTwoLabel = form.ArtificialColumns.Count > 0 ? "Phase 2" : "";
            SimplexOutcome phaseTwo = RunSimplex(form, phaseTwoColumns, result, phaseTwoLabel);

            if (phaseTwo == SimplexOutcome.Unbounded)
            {
                result.Status = SolutionStatus.Unbounded;
                result.Notes.Add("The entering column has no positive entry in the ratio test - the relaxation is unbounded.");
                return result;
            }

            if (phaseTwo == SimplexOutcome.IterationLimit)
            {
                result.Notes.Add("Phase 2 hit the iteration limit; the relaxation may be cycling.");
                return result;
            }

            result.Status = SolutionStatus.Optimal;
            ExtractSolution(form, model, result);
            return result;
        }

        // ------------------------------------------------------------------
        //  Simplex calculations
        // ------------------------------------------------------------------

        private enum SimplexOutcome { Optimal, Unbounded, IterationLimit }

        private SimplexOutcome RunSimplex(Form form, bool[] allowedColumns, SolutionResult result, string phaseLabel)
        {
            for (int iteration = 1; iteration <= MaxIterations; iteration++)
            {
                int entering = SelectEnteringColumn(form, allowedColumns);
                if (entering < 0) return SimplexOutcome.Optimal;

                int leaving = SelectLeavingRow(form, entering);
                if (leaving < 0) return SimplexOutcome.Unbounded;

                form.Pivot(leaving, entering);

                string label = string.IsNullOrEmpty(phaseLabel)
                    ? string.Format("Iteration {0}", iteration)
                    : string.Format("{0} - Iteration {1}", phaseLabel, iteration);
                Snapshot(form, result, label);
            }

            return SimplexOutcome.IterationLimit;
        }

        /// <summary>Selects the most negative value in the objective row.</summary>
        private int SelectEnteringColumn(Form form, bool[] allowedColumns)
        {
            int entering = -1;
            double mostNegative = -Eps;

            for (int j = 0; j < form.N; j++)
            {
                if (!allowedColumns[j]) continue;
                if (form.T[0, j] < mostNegative)
                {
                    mostNegative = form.T[0, j];
                    entering = j;
                }
            }

            return entering;
        }

        /// <summary>Uses the minimum ratio test to select the leaving row.</summary>
        private int SelectLeavingRow(Form form, int enteringColumn)
        {
            int leaving = -1;
            double bestRatio = double.MaxValue;

            for (int r = 1; r <= form.M; r++)
            {
                double denominator = form.T[r, enteringColumn];
                if (denominator <= Eps) continue;

                double ratio = form.T[r, form.N] / denominator;

                bool better = leaving < 0
                              || ratio < bestRatio - RatioTieTolerance
                              || (ratio <= bestRatio + RatioTieTolerance && form.Basis[r - 1] < form.Basis[leaving - 1]);

                if (better)
                {
                    bestRatio = ratio;
                    leaving = r;
                }
            }

            return leaving;
        }

        /// <summary>Pivots artificial variables out of the basis where possible.</summary>
        private void DriveArtificialsOutOfBasis(Form form)
        {
            for (int r = 1; r <= form.M; r++)
            {
                if (!form.IsArtificial(form.Basis[r - 1])) continue;

                for (int j = 0; j < form.N; j++)
                {
                    if (form.IsArtificial(j)) continue;
                    if (Math.Abs(form.T[r, j]) > Eps)
                    {
                        form.Pivot(r, j);
                        break;
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        //  Canonical form construction
        // ------------------------------------------------------------------

        private Form BuildCanonicalForm(LPModel model)
        {
            int variableCount = model.ObjectiveCoefficients.Length;
            int constraintCount = model.Constraints.Count;

            // Convert minimisation problems to maximisation while solving.
            double sense = model.Objective == ObjectiveType.Max ? 1.0 : -1.0;

            var headers = new List<string>();
            var objective = new List<double>();
            var variableColumns = new int[variableCount][];
            var variableSigns = new double[variableCount];

            for (int j = 0; j < variableCount; j++)
            {
                double c = model.ObjectiveCoefficients[j] * sense;

                switch (model.SignRestrictions[j])
                {
                    case SignRestriction.Unrestricted:
                        // Split unrestricted x into x+ and x-.
                        variableColumns[j] = new[] { headers.Count, headers.Count + 1 };
                        variableSigns[j] = 1.0;
                        headers.Add(string.Format("x{0}+", j + 1));
                        headers.Add(string.Format("x{0}-", j + 1));
                        objective.Add(c);
                        objective.Add(-c);
                        break;

                    case SignRestriction.Negative:
                        // Replace negative x with -xp.
                        variableColumns[j] = new[] { headers.Count };
                        variableSigns[j] = -1.0;
                        headers.Add(string.Format("x{0}p", j + 1));
                        objective.Add(-c);
                        break;

                    default: // Positive, integer and binary variables are x >= 0 after relaxation.
                        variableColumns[j] = new[] { headers.Count };
                        variableSigns[j] = 1.0;
                        headers.Add(string.Format("x{0}", j + 1));
                        objective.Add(c);
                        break;
                }
            }

            int structuralCount = headers.Count;

            // Make the RHS positive and flip the relation if needed.
            var rowCoefficients = new double[constraintCount][];
            var rowRelations = new RelationType[constraintCount];
            var rowRhs = new double[constraintCount];

            for (int i = 0; i < constraintCount; i++)
            {
                Constraint constraint = model.Constraints[i];
                double multiplier = constraint.Rhs < 0 ? -1.0 : 1.0;

                rowRhs[i] = constraint.Rhs * multiplier;
                rowRelations[i] = multiplier < 0 ? FlipRelation(constraint.Relation) : constraint.Relation;

                var row = new double[structuralCount];
                for (int j = 0; j < variableCount; j++)
                {
                    double a = constraint.Coefficients[j] * multiplier;
                    int[] columns = variableColumns[j];

                    if (columns.Length == 2)
                    {
                        row[columns[0]] = a;
                        row[columns[1]] = -a;
                    }
                    else
                    {
                        row[columns[0]] = a * variableSigns[j];
                    }
                }
                rowCoefficients[i] = row;
            }

            // Add slack or surplus variables, then artificial variables.
            int slackCount = 0;
            int artificialCount = 0;
            for (int i = 0; i < constraintCount; i++)
            {
                if (rowRelations[i] == RelationType.LessOrEqual) slackCount++;
                else if (rowRelations[i] == RelationType.GreaterOrEqual) { slackCount++; artificialCount++; }
                else artificialCount++;
            }

            int totalColumns = structuralCount + slackCount + artificialCount;
            var form = new Form(constraintCount, totalColumns);

            int nextSlack = structuralCount;
            int nextArtificial = structuralCount + slackCount;

            var slackHeaders = new List<string>();
            var artificialHeaders = new List<string>();

            for (int i = 0; i < constraintCount; i++)
            {
                for (int j = 0; j < structuralCount; j++)
                {
                    form.T[i + 1, j] = rowCoefficients[i][j];
                }
                form.T[i + 1, totalColumns] = rowRhs[i];

                switch (rowRelations[i])
                {
                    case RelationType.LessOrEqual:
                        form.T[i + 1, nextSlack] = 1.0;
                        slackHeaders.Add(string.Format("s{0}", i + 1));
                        form.Basis[i] = nextSlack;
                        nextSlack++;
                        break;

                    case RelationType.GreaterOrEqual:
                        form.T[i + 1, nextSlack] = -1.0; // surplus
                        slackHeaders.Add(string.Format("e{0}", i + 1));
                        nextSlack++;

                        form.T[i + 1, nextArtificial] = 1.0;
                        artificialHeaders.Add(string.Format("a{0}", i + 1));
                        form.ArtificialColumns.Add(nextArtificial);
                        form.Basis[i] = nextArtificial;
                        nextArtificial++;
                        break;

                    default: // Equal
                        form.T[i + 1, nextArtificial] = 1.0;
                        artificialHeaders.Add(string.Format("a{0}", i + 1));
                        form.ArtificialColumns.Add(nextArtificial);
                        form.Basis[i] = nextArtificial;
                        nextArtificial++;
                        break;
                }
            }

            headers.AddRange(slackHeaders);
            headers.AddRange(artificialHeaders);
            headers.Add("RHS");

            form.Headers = headers.ToArray();
            form.VariableColumns = variableColumns;
            form.VariableSigns = variableSigns;
            form.Sense = sense;

            form.MaxFormObjective = new double[totalColumns];
            for (int j = 0; j < structuralCount; j++)
            {
                form.MaxFormObjective[j] = objective[j];
            }

            return form;
        }

        private static RelationType FlipRelation(RelationType relation)
        {
            if (relation == RelationType.LessOrEqual) return RelationType.GreaterOrEqual;
            if (relation == RelationType.GreaterOrEqual) return RelationType.LessOrEqual;
            return RelationType.Equal;
        }

        private void ExtractSolution(Form form, LPModel model, SolutionResult result)
        {
            // Basic variables use the RHS value. Other variables are zero.
            var columnValues = new double[form.N];
            for (int r = 1; r <= form.M; r++)
            {
                columnValues[form.Basis[r - 1]] = form.T[r, form.N];
            }

            int variableCount = model.ObjectiveCoefficients.Length;
            result.VariableValues = new double[variableCount];

            for (int j = 0; j < variableCount; j++)
            {
                int[] columns = form.VariableColumns[j];
                result.VariableValues[j] = columns.Length == 2
                    ? columnValues[columns[0]] - columnValues[columns[1]]
                    : form.VariableSigns[j] * columnValues[columns[0]];
            }

            result.ObjectiveValue = form.T[0, form.N] * form.Sense;
        }

        private void Snapshot(Form form, SolutionResult result, string label)
        {
            if (!RecordIterations) return;
            result.Iterations.Add(form.ToTableau(label));
        }

        // ------------------------------------------------------------------
        //  Tableau data
        // ------------------------------------------------------------------

        private sealed class Form
        {
            public readonly double[,] T;   // Tableau values.
            public readonly int[] Basis;   // Basic column for each constraint row.
            public readonly int M;         // Number of constraints.
            public readonly int N;         // Number of columns excluding RHS.

            public string[] Headers = Array.Empty<string>();
            public double[] MaxFormObjective = Array.Empty<double>();
            public int[][] VariableColumns = Array.Empty<int[]>();
            public double[] VariableSigns = Array.Empty<double>();
            public List<int> ArtificialColumns = new List<int>();
            public double Sense = 1.0;

            public Form(int constraintCount, int columnCount)
            {
                M = constraintCount;
                N = columnCount;
                T = new double[constraintCount + 1, columnCount + 1];
                Basis = new int[constraintCount];
            }

            public bool IsArtificial(int column)
            {
                return ArtificialColumns.Contains(column);
            }

            /// <summary>Builds the objective row and makes the basic columns zero.</summary>
            public void BuildObjectiveRow(double[] maxFormObjective)
            {
                for (int j = 0; j < N; j++) T[0, j] = -maxFormObjective[j];
                T[0, N] = 0.0;

                for (int r = 1; r <= M; r++)
                {
                    double factor = T[0, Basis[r - 1]];
                    if (Math.Abs(factor) <= Eps) continue;

                    for (int j = 0; j <= N; j++)
                    {
                        T[0, j] -= factor * T[r, j];
                    }
                }
            }

            public void Pivot(int pivotRow, int pivotColumn)
            {
                double pivotElement = T[pivotRow, pivotColumn];
                for (int j = 0; j <= N; j++) T[pivotRow, j] /= pivotElement;

                for (int r = 0; r <= M; r++)
                {
                    if (r == pivotRow) continue;

                    double factor = T[r, pivotColumn];
                    if (Math.Abs(factor) <= Eps) continue;

                    for (int j = 0; j <= N; j++)
                    {
                        T[r, j] -= factor * T[pivotRow, j];
                    }
                }

                Basis[pivotRow - 1] = pivotColumn;
            }

            /// <summary>Creates a copy of the current tableau for the iteration log.</summary>
            public Tableau ToTableau(string label)
            {
                var basicNames = new string[M];
                for (int r = 0; r < M; r++) basicNames[r] = Headers[Basis[r]];

                // Show very small values as 0.
                var values = new double[M + 1, N + 1];
                for (int r = 0; r <= M; r++)
                {
                    for (int j = 0; j <= N; j++)
                    {
                        double v = T[r, j];
                        values[r, j] = Math.Abs(v) < 1e-12 ? 0.0 : v;
                    }
                }

                return new Tableau
                {
                    Label = label,
                    ColumnHeaders = (string[])Headers.Clone(),
                    BasicVariables = basicNames,
                    Values = values
                };
            }
        }
    }
}
