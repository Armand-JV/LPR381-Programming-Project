using System;
using System.Collections.Generic;
using System.Globalization;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.BranchAndBound
{
    /// <summary>
    /// Solves integer and binary LP problems using Branch and Bound.
    /// </summary>
    public sealed class BranchAndBoundSimplexSolver : IAlgorithm
    {
        private readonly IAlgorithm _lpSolver;

        /// <summary>Tolerance used when checking for whole numbers.</summary>
        private const double IntegerTolerance = 1e-6;

        /// <summary>Tolerance used when comparing objective values.</summary>
        private const double BoundTolerance = 1e-6;

        /// <summary>Tolerance used when two variables sit the same distance from 0.5.</summary>
        private const double TieTolerance = 1e-9;

        /// <summary>Maximum number of sub-problems to solve.</summary>
        private const int MaxNodes = 5000;

        public string Name
        {
            get { return "Branch & Bound Simplex"; }
        }

        /// <summary>
        /// Uses the self-contained two-phase relaxation solver by default. Any
        /// <see cref="IAlgorithm"/> can be injected instead, but a B&amp;B engine has to
        /// report an infeasible sub-problem as <see cref="SolutionStatus.Infeasible"/>
        /// and hand back tableau snapshots it will not keep mutating - otherwise the
        /// fathoming decisions and the iteration log are both wrong.
        /// </summary>
        public BranchAndBoundSimplexSolver()
            : this(new RelaxationSimplexSolver())
        {
        }

        public BranchAndBoundSimplexSolver(IAlgorithm lpSolver)
        {
            _lpSolver = lpSolver ?? throw new ArgumentNullException(nameof(lpSolver));
        }

        public SolutionResult Solve(LPModel model)
        {
            ValidateModel(model);

            var result = new SolutionResult
            {
                AlgorithmName = Name,
                Status = SolutionStatus.NotSolved
            };

            int variableCount = model.ObjectiveCoefficients.Length;
            bool maximise = model.Objective == ObjectiveType.Max;

            var integerVariables = new List<int>();
            for (int j = 0; j < variableCount; j++)
            {
                if (model.SignRestrictions[j] == SignRestriction.Integer ||
                    model.SignRestrictions[j] == SignRestriction.Binary)
                {
                    integerVariables.Add(j);
                }
            }

            if (integerVariables.Count == 0)
            {
                return SolveAsPureLinearProgram(model, result);
            }

            var rootNode = new Node
            {
                Id = 0,
                ParentId = -1,
                Depth = 0,
                BranchDescription = "LP relaxation",
                Model = BuildRootRelaxation(model)
            };

            var stack = new Stack<Node>();
            stack.Push(rootNode);

            int nextNodeId = 1;
            int nodesExplored = 0;
            int fathomedInfeasible = 0;
            int fathomedByBound = 0;
            int fathomedIntegral = 0;
            int candidatesFound = 0;
            int previousDepth = -1;
            bool relaxationUnbounded = false;

            double[]? bestVariableValues = null;
            double bestObjective = maximise ? double.NegativeInfinity : double.PositiveInfinity;
            int bestNodeId = -1;

            result.Notes.Add("=== Branch & Bound node log ===");
            result.Notes.Add(string.Format(
                "Integer-restricted variables: {0}", DescribeIntegerVariables(model, integerVariables)));
            result.Notes.Add("Search: depth-first (last-in-first-out stack), branching on the most fractional variable.");
            result.Notes.Add("");

            while (stack.Count > 0)
            {
                if (nodesExplored >= MaxNodes)
                {
                    result.Notes.Add(string.Format(
                        "Node limit ({0}) reached - the search was cut short and the best candidate below may not be optimal.",
                        MaxNodes));
                    break;
                }

                Node node = stack.Pop();

                // Check if the search has moved back to another branch.
                if (previousDepth >= 0 && node.Depth <= previousDepth)
                {
                    result.Notes.Add(node.Depth == previousDepth
                        ? string.Format(
                            "  -- backtrack: branch exhausted, taking the sibling node at depth {0} --", node.Depth)
                        : string.Format(
                            "  -- backtrack: branch exhausted at depth {0}, climbing back to depth {1} --",
                            previousDepth, node.Depth));
                }
                previousDepth = node.Depth;

                nodesExplored++;

                string nodeLabel = string.Format("Sub-problem {0} ({1})", node.Id, node.BranchDescription);
                string origin = node.ParentId < 0
                    ? "root"
                    : string.Format("from Sub-problem {0}", node.ParentId);

                SolutionResult relaxation;
                try
                {
                    // Solve a copy of the current LP relaxation.
                    relaxation = _lpSolver.Solve(node.Model.Clone());
                }
                catch (Exception ex)
                {
                    result.Notes.Add(string.Format(
                        "{0} [{1}]: LP relaxation solver failed - {2} -> fathomed", nodeLabel, origin, ex.Message));
                    continue;
                }

                // Add the sub-problem name to each tableau.
                foreach (Tableau tableau in relaxation.Iterations)
                {
                    tableau.Label = string.Format("{0} - {1}", nodeLabel, tableau.Label);
                    result.Iterations.Add(tableau);
                }

                // Fathom if infeasible.
                if (relaxation.Status == SolutionStatus.Infeasible)
                {
                    fathomedInfeasible++;
                    result.Notes.Add(string.Format(
                        "{0} [{1}]: INFEASIBLE -> fathomed (branch constraints cannot be satisfied)", nodeLabel, origin));
                    continue;
                }

                if (relaxation.Status == SolutionStatus.Unbounded)
                {
                    relaxationUnbounded = true;
                    result.Notes.Add(string.Format(
                        "{0} [{1}]: UNBOUNDED relaxation -> the integer model is unbounded", nodeLabel, origin));
                    continue;
                }

                if (relaxation.Status != SolutionStatus.Optimal || relaxation.VariableValues == null)
                {
                    result.Notes.Add(string.Format(
                        "{0} [{1}]: relaxation returned {2} -> fathomed (cannot branch on an unsolved node)",
                        nodeLabel, origin, relaxation.Status));
                    continue;
                }

                double bound = relaxation.ObjectiveValue;

                // Fathom if this bound cannot improve the best solution.
                if (bestVariableValues != null && !IsBetter(bound, bestObjective, maximise))
                {
                    fathomedByBound++;
                    result.Notes.Add(string.Format(
                        "{0} [{1}]: bound z = {2} cannot beat incumbent z = {3} -> fathomed (bound)",
                        nodeLabel, origin, Format(bound), Format(bestObjective)));
                    continue;
                }

                // Check if this is an integer solution.
                int branchVariable = SelectBranchingVariable(relaxation.VariableValues, integerVariables);
                if (branchVariable < 0)
                {
                    fathomedIntegral++;
                    candidatesFound++;

                    if (bestVariableValues == null || IsBetter(bound, bestObjective, maximise))
                    {
                        bestObjective = bound;
                        bestVariableValues = (double[])relaxation.VariableValues.Clone();
                        bestNodeId = node.Id;

                        result.Notes.Add(string.Format(
                            "{0} [{1}]: integer feasible, z = {2} -> NEW BEST CANDIDATE ({3})",
                            nodeLabel, origin, Format(bound), DescribePoint(relaxation.VariableValues)));
                    }
                    else
                    {
                        result.Notes.Add(string.Format(
                            "{0} [{1}]: integer feasible, z = {2} -> fathomed (does not improve on z = {3})",
                            nodeLabel, origin, Format(bound), Format(bestObjective)));
                    }
                    continue;
                }

                // Branch on the selected fractional variable.
                double branchValue = relaxation.VariableValues[branchVariable];
                double floorValue = Math.Floor(branchValue);
                double ceilingValue = Math.Ceiling(branchValue);

                result.Notes.Add(string.Format(
                    "{0} [{1}]: z = {2}, x{3} = {4} is fractional -> branch into x{3} <= {5} and x{3} >= {6}",
                    nodeLabel, origin, Format(bound), branchVariable + 1, Format(branchValue),
                    Format(floorValue), Format(ceilingValue)));

                var downBranch = CreateChild(node, nextNodeId++, branchVariable,
                                             RelationType.LessOrEqual, floorValue, variableCount);
                var upBranch = CreateChild(node, nextNodeId++, branchVariable,
                                           RelationType.GreaterOrEqual, ceilingValue, variableCount);

                // Push >= first so the <= branch is solved first.
                stack.Push(upBranch);
                stack.Push(downBranch);
            }

            result.Notes.Add("");
            result.Notes.Add("=== Branch & Bound summary ===");
            result.Notes.Add(string.Format("Sub-problems solved      : {0}", nodesExplored));
            result.Notes.Add(string.Format("Fathomed - infeasible    : {0}", fathomedInfeasible));
            result.Notes.Add(string.Format("Fathomed - bound         : {0}", fathomedByBound));
            result.Notes.Add(string.Format("Fathomed - integer       : {0}", fathomedIntegral));
            result.Notes.Add(string.Format("Integer candidates found : {0}", candidatesFound));

            if (bestVariableValues != null)
            {
                result.Status = SolutionStatus.Optimal;
                result.VariableValues = bestVariableValues;
                result.ObjectiveValue = bestObjective;

                result.Notes.Add("");
                result.Notes.Add(string.Format(
                    "BEST CANDIDATE: found at Sub-problem {0}, z = {1}", bestNodeId, Format(bestObjective)));
                for (int j = 0; j < bestVariableValues.Length; j++)
                {
                    result.Notes.Add(string.Format("  x{0} = {1}", j + 1, Format(bestVariableValues[j])));
                }
            }
            else if (relaxationUnbounded)
            {
                result.Status = SolutionStatus.Unbounded;
                result.Notes.Add("");
                result.Notes.Add("No best candidate: the LP relaxation is unbounded, so the integer model has no finite optimum.");
            }
            else
            {
                result.Status = SolutionStatus.Infeasible;
                result.Notes.Add("");
                result.Notes.Add("No best candidate: every sub-problem was fathomed without producing an integer feasible point.");
            }

            return result;
        }

        // ------------------------------------------------------------------
        //  Model preparation
        // ------------------------------------------------------------------

        /// <summary>Builds the LP relaxation and adds x <= 1 for binary variables.</summary>
        private LPModel BuildRootRelaxation(LPModel model)
        {
            LPModel relaxed = model.Clone();
            int variableCount = model.ObjectiveCoefficients.Length;

            for (int j = 0; j < variableCount; j++)
            {
                if (model.SignRestrictions[j] == SignRestriction.Binary)
                {
                    var coefficients = new double[variableCount];
                    coefficients[j] = 1.0;
                    relaxed.Constraints.Add(new Constraint(coefficients, RelationType.LessOrEqual, 1.0));
                }
            }

            for (int j = 0; j < variableCount; j++)
            {
                if (relaxed.SignRestrictions[j] == SignRestriction.Integer ||
                    relaxed.SignRestrictions[j] == SignRestriction.Binary)
                {
                    relaxed.SignRestrictions[j] = SignRestriction.Positive;
                }
            }

            return relaxed;
        }

        /// <summary>Creates a child node with its branch constraint.</summary>
        private Node CreateChild(Node parent, int childId, int variableIndex,
                                 RelationType relation, double bound, int variableCount)
        {
            LPModel childModel = parent.Model.Clone();

            var coefficients = new double[variableCount];
            coefficients[variableIndex] = 1.0;
            childModel.Constraints.Add(new Constraint(coefficients, relation, bound));

            string relationSymbol = relation == RelationType.LessOrEqual ? "<=" : ">=";

            return new Node
            {
                Id = childId,
                ParentId = parent.Id,
                Depth = parent.Depth + 1,
                BranchDescription = string.Format("x{0} {1} {2}", variableIndex + 1, relationSymbol, Format(bound)),
                Model = childModel
            };
        }

        /// <summary>
        /// Selects a fractional integer variable to branch on: closest to 0.5 wins, ties go to the
        /// left-most variable. Returns -1 if all are whole numbers.
        /// </summary>
        private int SelectBranchingVariable(double[] values, List<int> integerVariables)
        {
            int chosen = -1;
            double smallestDistanceToHalf = double.MaxValue;

            foreach (int j in integerVariables)
            {
                double fraction = values[j] - Math.Floor(values[j]);
                if (fraction <= IntegerTolerance || fraction >= 1.0 - IntegerTolerance)
                {
                    continue; // already integral
                }

                double distanceToHalf = Math.Abs(fraction - 0.5);
                if (distanceToHalf < smallestDistanceToHalf - TieTolerance)
                {
                    smallestDistanceToHalf = distanceToHalf;
                    chosen = j;
                }
            }

            return chosen;
        }

        /// <summary>Solves normally when there are no integer or binary variables.</summary>
        private SolutionResult SolveAsPureLinearProgram(LPModel model, SolutionResult result)
        {
            SolutionResult relaxation = _lpSolver.Solve(model.Clone());

            foreach (Tableau tableau in relaxation.Iterations)
            {
                tableau.Label = string.Format("Sub-problem 0 (LP relaxation) - {0}", tableau.Label);
                result.Iterations.Add(tableau);
            }

            result.Status = relaxation.Status;
            result.VariableValues = relaxation.VariableValues;
            result.ObjectiveValue = relaxation.ObjectiveValue;

            result.Notes.Add("No variable carries an int or bin restriction, so there is nothing to branch on.");
            result.Notes.Add("The LP relaxation optimum below is already the optimal solution; no sub-problems were created.");
            result.Notes.AddRange(relaxation.Notes);

            return result;
        }

        // ------------------------------------------------------------------
        //  Validation and formatting
        // ------------------------------------------------------------------

        /// <summary>Checks that the model contains the required data.</summary>
        private void ValidateModel(LPModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            int variableCount = model.ObjectiveCoefficients.Length;

            if (variableCount == 0)
            {
                throw new InvalidOperationException(
                    "Branch & Bound needs at least one decision variable, but the objective function has none.");
            }

            if (model.SignRestrictions.Length != variableCount)
            {
                throw new InvalidOperationException(string.Format(
                    "Branch & Bound needs one sign restriction per decision variable: expected {0}, found {1}.",
                    variableCount, model.SignRestrictions.Length));
            }

            if (model.Constraints.Count == 0)
            {
                throw new InvalidOperationException(
                    "Branch & Bound needs at least one constraint; an unconstrained integer model has no bounded optimum.");
            }

            for (int i = 0; i < model.Constraints.Count; i++)
            {
                int found = model.Constraints[i].Coefficients.Length;
                if (found != variableCount)
                {
                    throw new InvalidOperationException(string.Format(
                        "Constraint {0} has {1} technological coefficients but the model has {2} decision variables.",
                        i + 1, found, variableCount));
                }
            }
        }

        /// <summary>Checks if a value improves the current best objective.</summary>
        private static bool IsBetter(double candidate, double incumbent, bool maximise)
        {
            return maximise
                ? candidate > incumbent + BoundTolerance
                : candidate < incumbent - BoundTolerance;
        }

        private string DescribeIntegerVariables(LPModel model, List<int> integerVariables)
        {
            var parts = new List<string>();
            foreach (int j in integerVariables)
            {
                parts.Add(string.Format("x{0} ({1})", j + 1,
                    model.SignRestrictions[j] == SignRestriction.Binary ? "bin" : "int"));
            }
            return string.Join(", ", parts);
        }

        private string DescribePoint(double[] values)
        {
            var parts = new List<string>();
            for (int j = 0; j < values.Length; j++)
            {
                parts.Add(string.Format("x{0}={1}", j + 1, Format(values[j])));
            }
            return string.Join(", ", parts);
        }

        /// <summary>Formats values to a maximum of 3 decimal places.</summary>
        private static string Format(double value)
        {
            double rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
            if (rounded == 0.0)
            {
                return "0"; // avoids a negative zero printing as "-0"
            }

            return rounded.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>Stores one Branch and Bound sub-problem.</summary>
        private sealed class Node
        {
            public int Id;
            public int ParentId;
            public int Depth;
            public string BranchDescription = "";
            public LPModel Model = new LPModel();
        }
    }
}
