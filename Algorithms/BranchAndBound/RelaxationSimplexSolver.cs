using System;
using System.Collections.Generic;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.BranchAndBound
{
    /// <summary>
    /// OWNER: Person 2 (Branch &amp; Bound Simplex)
    ///
    /// A self-contained two-phase Simplex used as the default LP-relaxation
    /// engine for <see cref="BranchAndBoundSimplexSolver"/>.
    ///
    /// WHY THIS EXISTS: Branch &amp; Bound can only fathom correctly if the
    /// relaxation solver (a) reports Infeasible as Infeasible, and (b) hands
    /// back an untouched snapshot of every tableau iteration. Until Person 1's
    /// PrimalSimplexSolver does both, B&amp;B carries its own engine so this
    /// section is not blocked. It lives entirely inside the BranchAndBound
    /// namespace - no shared file is modified.
    ///
    /// TO SWITCH BACK at integration time, change the one line in
    /// BranchAndBoundSimplexSolver's default constructor:
    ///     : this(new RelaxationSimplexSolver())
    ///  -> : this(new PrimalSimplex.PrimalSimplexSolver())
    ///
    /// Method: two-phase Simplex (NOT Big-M). Phase 1 minimises the sum of the
    /// artificial variables; if that minimum is greater than zero the model is
    /// genuinely infeasible. This avoids the precision loss a large Big-M
    /// penalty causes, which matters because B&amp;B compares node bounds
    /// against the incumbent in order to prune.
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

        /// <summary>When false the solver skips building tableau snapshots. Branch &amp;
        /// Bound trees can visit many nodes; turning this off keeps the output
        /// readable when only the final answer is wanted.</summary>
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

            // ---- Phase 1: drive the artificial variables to zero ----
            if (form.ArtificialColumns.Count > 0)
            {
                var phaseOneObjective = new double[form.N];
                foreach (int artificialColumn in form.ArtificialColumns)
                {
                    // maximise -(sum of artificials) == minimise (sum of artificials)
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

                // The phase 1 optimum is -(sum of artificials). Anything below zero
                // means an artificial is stuck at a positive value, so no point
                // satisfies every constraint at once.
                if (form.T[0, form.N] < -1e-7)
                {
                    result.Status = SolutionStatus.Infeasible;
                    result.Notes.Add("Phase 1 could not drive every artificial variable to zero - the model is infeasible.");
                    return result;
                }

                DriveArtificialsOutOfBasis(form);
            }

            // ---- Phase 2: optimise the real objective ----
            form.BuildObjectiveRow(form.MaxFormObjective);
            Snapshot(form, result, form.ArtificialColumns.Count > 0 ? "Phase 2 - Canonical Form" : "Canonical Form");

            // Artificial columns must never re-enter the basis during phase 2.
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
        //  Simplex engine
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

        /// <summary>Dantzig rule: the most negative entry in the objective row. Row 0
        /// holds (z_j - c_j) in maximisation form, so a negative entry means the
        /// column can still improve the objective.</summary>
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

        /// <summary>Minimum ratio test. Ties break towards the smallest basic variable
        /// index (Bland's rule) so a degenerate model cannot cycle forever.</summary>
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

        /// <summary>After a feasible phase 1 an artificial can still sit in the basis at
        /// value zero. Pivot it out on any real column so phase 2 starts from a clean
        /// basis; a row with no such column is redundant and is left as it is.</summary>
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

            // Everything is solved as a maximisation internally; a min model is
            // maximised as -c and the objective value is flipped back at the end.
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
                        // x = x+ - x-, both non-negative
                        variableColumns[j] = new[] { headers.Count, headers.Count + 1 };
                        variableSigns[j] = 1.0;
                        headers.Add(string.Format("x{0}+", j + 1));
                        headers.Add(string.Format("x{0}-", j + 1));
                        objective.Add(c);
                        objective.Add(-c);
                        break;

                    case SignRestriction.Negative:
                        // x <= 0, so substitute x = -xp with xp >= 0
                        variableColumns[j] = new[] { headers.Count };
                        variableSigns[j] = -1.0;
                        headers.Add(string.Format("x{0}p", j + 1));
                        objective.Add(-c);
                        break;

                    default: // Positive, Integer and Binary are all x >= 0 once relaxed
                        variableColumns[j] = new[] { headers.Count };
                        variableSigns[j] = 1.0;
                        headers.Add(string.Format("x{0}", j + 1));
                        objective.Add(c);
                        break;
                }
            }

            int structuralCount = headers.Count;

            // Normalise every row to a non-negative RHS first. Negating a row also
            // flips its relation - getting that wrong produces an invalid basis.
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

            // Slack / surplus columns come first, then the artificials.
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
            // Basic columns take their RHS value; every other column is zero.
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

            // Row 0's RHS carries the maximisation-form objective; flip it back for a min model.
            result.ObjectiveValue = form.T[0, form.N] * form.Sense;
        }

        private void Snapshot(Form form, SolutionResult result, string label)
        {
            if (!RecordIterations) return;
            result.Iterations.Add(form.ToTableau(label));
        }

        // ------------------------------------------------------------------
        //  Tableau state
        // ------------------------------------------------------------------

        private sealed class Form
        {
            public readonly double[,] T;   // (M+1) x (N+1); row 0 = objective, last column = RHS
            public readonly int[] Basis;   // column index of the basic variable per constraint row
            public readonly int M;         // constraint count
            public readonly int N;         // column count excluding RHS

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

            /// <summary>Lays the given maximisation-form objective into row 0 as
            /// (z_j - c_j), then prices out the current basis so every basic column
            /// reads zero.</summary>
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

            /// <summary>Builds a fresh Tableau on every call. Nothing the caller receives
            /// aliases live solver state, so a later pivot can never rewrite an
            /// iteration that was already recorded.</summary>
            public Tableau ToTableau(string label)
            {
                var basicNames = new string[M];
                for (int r = 0; r < M; r++) basicNames[r] = Headers[Basis[r]];

                // Copy out, flattening negative zero and pivot noise so the displayed
                // tableau reads "0" rather than "-0" or "-2.7E-17".
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
