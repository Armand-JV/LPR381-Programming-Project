using System;
using System.Collections.Generic;
using System.Linq;
using LPR381Project.Models;

namespace LPR381Project.Algorithms.IntegerAlgorithms
{
    /// <summary>
    /// /// Placeholder for Branch & Bound Knapsack solver referenced by the menu.
    /// Implement the real algorithm here when available.
    /// Solves a binary maximisation knapsack problem using
    /// Branch and Bound with a fractional-knapsack upper bound.
    /// </summary>
    public class BranchAndBoundKnapsackSolver : IAlgorithm
    {
        private const double Tolerance = 0.000001;

        public string Name => "Branch & Bound Knapsack";

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

            int variableCount = model.ObjectiveCoefficients.Length;
            double capacity = model.Constraints[0].Rhs;
            double[] profits = model.ObjectiveCoefficients;
            double[] weights = model.Constraints[0].Coefficients;

            // Sort items by profit-to-weight ratio for the fractional bound.
            List<Item> items = new List<Item>();

            for (int i = 0; i < variableCount; i++)
            {
                double ratio;

                if (Math.Abs(weights[i]) <= Tolerance)
                {
                    ratio = profits[i] > 0
                        ? double.PositiveInfinity
                        : 0;
                }
                else
                {
                    ratio = profits[i] / weights[i];
                }

                items.Add(new Item
                {
                    OriginalIndex = i,
                    Profit = profits[i],
                    Weight = weights[i],
                    Ratio = ratio
                });
            }

            items = items
                .OrderByDescending(item => item.Ratio)
                .ThenByDescending(item => item.Profit)
                .ToList();

            result.Notes.Add("Items sorted by descending profit-to-weight ratio.");

            for (int i = 0; i < items.Count; i++)
            {
                result.Notes.Add(
                    $"Order {i + 1}: x{items[i].OriginalIndex + 1}, " +
                    $"profit = {items[i].Profit:0.000}, " +
                    $"weight = {items[i].Weight:0.000}, " +
                    $"ratio = {FormatNumber(items[i].Ratio)}");
            }

            int nextNodeNumber = 1;
            double bestValue = 0;
            double bestWeight = 0;
            int[] bestSelection = new int[variableCount];

            Node root = new Node
            {
                NodeNumber = nextNodeNumber++,
                Level = 0,
                CurrentProfit = 0,
                CurrentWeight = 0,
                Selection = CreateUndecidedSelection(variableCount)
            };

            root.UpperBound = CalculateUpperBound(
                root,
                items,
                capacity);

            List<Node> liveNodes = new List<Node> { root };

            AddNodeIteration(
                result,
                root,
                variableCount,
                capacity,
                "Root node created");

            result.Notes.Add(
                $"Node {root.NodeNumber}: Root node created. " +
                $"Upper bound = {root.UpperBound:0.000}.");

            while (liveNodes.Count > 0)
            {
                // Select the live node with the highest upper bound.
                Node current = liveNodes
                    .OrderByDescending(node => node.UpperBound)
                    .ThenBy(node => node.NodeNumber)
                    .First();

                liveNodes.Remove(current);

                result.Notes.Add(
                    $"Backtracking/selection: Node {current.NodeNumber} " +
                    $"selected with bound {current.UpperBound:0.000}.");

                if (current.UpperBound <= bestValue + Tolerance)
                {
                    result.Notes.Add(
                        $"Node {current.NodeNumber} fathomed by bound: " +
                        $"{current.UpperBound:0.000} cannot improve the current " +
                        $"best value {bestValue:0.000}.");

                    continue;
                }

                if (current.Level >= variableCount)
                {
                    if (current.CurrentProfit > bestValue + Tolerance)
                    {
                        UpdateBestCandidate(
                            current,
                            ref bestValue,
                            ref bestWeight,
                            bestSelection,
                            result);
                    }

                    result.Notes.Add(
                        $"Node {current.NodeNumber} fathomed: " +
                        "all variables have been assigned.");

                    continue;
                }

                Item branchItem = items[current.Level];

                // Branch 1: Include the current item.
                Node includeNode = CreateChildNode(
                    current,
                    branchItem,
                    true,
                    nextNodeNumber++);

                if (includeNode.CurrentWeight > capacity + Tolerance)
                {
                    includeNode.UpperBound = double.NegativeInfinity;

                    AddNodeIteration(
                        result,
                        includeNode,
                        variableCount,
                        capacity,
                        $"Include x{branchItem.OriginalIndex + 1}");

                    result.Notes.Add(
                        $"Node {includeNode.NodeNumber}: " +
                        $"x{branchItem.OriginalIndex + 1} = 1. " +
                        $"Node fathomed as infeasible because weight " +
                        $"{includeNode.CurrentWeight:0.000} exceeds capacity " +
                        $"{capacity:0.000}.");
                }
                else
                {
                    includeNode.UpperBound = CalculateUpperBound(
                        includeNode,
                        items,
                        capacity);

                    AddNodeIteration(
                        result,
                        includeNode,
                        variableCount,
                        capacity,
                        $"Include x{branchItem.OriginalIndex + 1}");

                    result.Notes.Add(
                        $"Node {includeNode.NodeNumber}: " +
                        $"x{branchItem.OriginalIndex + 1} = 1, " +
                        $"weight = {includeNode.CurrentWeight:0.000}, " +
                        $"value = {includeNode.CurrentProfit:0.000}, " +
                        $"bound = {includeNode.UpperBound:0.000}.");

                    if (includeNode.CurrentProfit > bestValue + Tolerance)
                    {
                        UpdateBestCandidate(
                            includeNode,
                            ref bestValue,
                            ref bestWeight,
                            bestSelection,
                            result);
                    }

                    if (includeNode.Level >= variableCount)
                    {
                        result.Notes.Add(
                            $"Node {includeNode.NodeNumber} fathomed: " +
                            "complete feasible solution.");
                    }
                    else if (includeNode.UpperBound >
                             bestValue + Tolerance)
                    {
                        liveNodes.Add(includeNode);
                    }
                    else
                    {
                        result.Notes.Add(
                            $"Node {includeNode.NodeNumber} fathomed by bound.");
                    }
                }

                // Branch 2: Exclude the current item.
                Node excludeNode = CreateChildNode(
                    current,
                    branchItem,
                    false,
                    nextNodeNumber++);

                excludeNode.UpperBound = CalculateUpperBound(
                    excludeNode,
                    items,
                    capacity);

                AddNodeIteration(
                    result,
                    excludeNode,
                    variableCount,
                    capacity,
                    $"Exclude x{branchItem.OriginalIndex + 1}");

                result.Notes.Add(
                    $"Node {excludeNode.NodeNumber}: " +
                    $"x{branchItem.OriginalIndex + 1} = 0, " +
                    $"weight = {excludeNode.CurrentWeight:0.000}, " +
                    $"value = {excludeNode.CurrentProfit:0.000}, " +
                    $"bound = {excludeNode.UpperBound:0.000}.");

                if (excludeNode.Level >= variableCount)
                {
                    if (excludeNode.CurrentProfit >
                        bestValue + Tolerance)
                    {
                        UpdateBestCandidate(
                            excludeNode,
                            ref bestValue,
                            ref bestWeight,
                            bestSelection,
                            result);
                    }

                    result.Notes.Add(
                        $"Node {excludeNode.NodeNumber} fathomed: " +
                        "complete feasible solution.");
                }
                else if (excludeNode.UpperBound >
                         bestValue + Tolerance)
                {
                    liveNodes.Add(excludeNode);
                }
                else
                {
                    result.Notes.Add(
                        $"Node {excludeNode.NodeNumber} fathomed by bound.");
                }
            }

            result.Status = SolutionStatus.Optimal;
            result.VariableValues = bestSelection
                .Select(value => (double)value)
                .ToArray();
            result.ObjectiveValue = bestValue;

            result.Notes.Add("Search completed.");
            result.Notes.Add(
                $"Best candidate weight = {bestWeight:0.000}.");
            result.Notes.Add(
                $"Best candidate objective value = {bestValue:0.000}.");
            result.Notes.Add(
                "Best candidate: " +
                string.Join(
                    ", ",
                    bestSelection.Select(
                        (value, index) => $"x{index + 1} = {value}")));

            return result;
        }

        private static bool ValidateModel(
            LPModel model,
            SolutionResult result)
        {
            if (model.Objective != ObjectiveType.Max)
            {
                result.Notes.Add(
                    "Branch & Bound Knapsack requires a maximisation model.");

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
                model.Constraints.Count != 1)
            {
                result.Notes.Add(
                    "Branch & Bound Knapsack requires exactly one " +
                    "capacity constraint.");

                return false;
            }

            Constraint constraint = model.Constraints[0];

            if (constraint.Relation != RelationType.LessOrEqual)
            {
                result.Notes.Add(
                    "The knapsack capacity constraint must use <=.");

                return false;
            }

            if (constraint.Rhs < 0)
            {
                result.Notes.Add(
                    "The knapsack capacity cannot be negative.");

                return false;
            }

            if (constraint.Coefficients.Length !=
                model.ObjectiveCoefficients.Length)
            {
                result.Notes.Add(
                    "The number of weights does not match the number " +
                    "of objective coefficients.");

                return false;
            }

            if (model.SignRestrictions == null ||
                model.SignRestrictions.Length !=
                model.ObjectiveCoefficients.Length)
            {
                result.Notes.Add(
                    "Every knapsack variable must have a binary restriction.");

                return false;
            }

            for (int i = 0;
                 i < model.SignRestrictions.Length;
                 i++)
            {
                if (model.SignRestrictions[i] !=
                    SignRestriction.Binary)
                {
                    result.Notes.Add(
                        $"x{i + 1} is not binary. All knapsack variables " +
                        "must use the 'bin' restriction.");

                    return false;
                }

                if (constraint.Coefficients[i] < 0)
                {
                    result.Notes.Add(
                        $"The weight of x{i + 1} cannot be negative.");

                    return false;
                }
            }

            return true;
        }

        private static int[] CreateUndecidedSelection(int count)
        {
            int[] selection = new int[count];

            for (int i = 0; i < count; i++)
            {
                selection[i] = -1;
            }

            return selection;
        }

        private static Node CreateChildNode(
            Node parent,
            Item item,
            bool include,
            int nodeNumber)
        {
            int[] childSelection =
                (int[])parent.Selection.Clone();

            childSelection[item.OriginalIndex] = include ? 1 : 0;

            return new Node
            {
                NodeNumber = nodeNumber,
                Level = parent.Level + 1,
                CurrentProfit = parent.CurrentProfit +
                                (include ? item.Profit : 0),
                CurrentWeight = parent.CurrentWeight +
                                (include ? item.Weight : 0),
                Selection = childSelection
            };
        }

        private static double CalculateUpperBound(
            Node node,
            List<Item> items,
            double capacity)
        {
            if (node.CurrentWeight > capacity + Tolerance)
            {
                return double.NegativeInfinity;
            }

            double bound = node.CurrentProfit;
            double remainingCapacity =
                capacity - node.CurrentWeight;

            for (int i = node.Level; i < items.Count; i++)
            {
                Item item = items[i];

                // Items with no positive profit cannot increase the bound.
                if (item.Profit <= 0)
                {
                    continue;
                }

                if (Math.Abs(item.Weight) <= Tolerance)
                {
                    bound += item.Profit;
                    continue;
                }

                if (item.Weight <= remainingCapacity + Tolerance)
                {
                    remainingCapacity -= item.Weight;
                    bound += item.Profit;
                }
                else
                {
                    bound += item.Profit *
                             (remainingCapacity / item.Weight);

                    break;
                }
            }

            return bound;
        }

        private static void UpdateBestCandidate(
            Node node,
            ref double bestValue,
            ref double bestWeight,
            int[] bestSelection,
            SolutionResult result)
        {
            bestValue = node.CurrentProfit;
            bestWeight = node.CurrentWeight;

            for (int i = 0; i < bestSelection.Length; i++)
            {
                bestSelection[i] =
                    node.Selection[i] == 1 ? 1 : 0;
            }

            result.Notes.Add(
                $"New best candidate found at Node {node.NodeNumber}: " +
                $"value = {bestValue:0.000}, " +
                $"weight = {bestWeight:0.000}.");
        }

        private static void AddNodeIteration(
            SolutionResult result,
            Node node,
            int variableCount,
            double capacity,
            string decision)
        {
            string[] headers = new string[variableCount + 4];

            for (int i = 0; i < variableCount; i++)
            {
                headers[i] = $"x{i + 1}";
            }

            headers[variableCount] = "Weight";
            headers[variableCount + 1] = "Value";
            headers[variableCount + 2] = "Bound";
            headers[variableCount + 3] = "Capacity";

            double[,] values =
                new double[1, variableCount + 4];

            for (int i = 0; i < variableCount; i++)
            {
                values[0, i] = node.Selection[i];
            }

            values[0, variableCount] = node.CurrentWeight;
            values[0, variableCount + 1] = node.CurrentProfit;
            values[0, variableCount + 2] = node.UpperBound;
            values[0, variableCount + 3] = capacity;

            result.Iterations.Add(new Tableau
            {
                Label =
                    $"Node {node.NodeNumber} - {decision}",
                ColumnHeaders = headers,
                BasicVariables = Array.Empty<string>(),
                Values = values
            });
        }

        private static string FormatNumber(double value)
        {
            if (double.IsPositiveInfinity(value))
            {
                return "Infinity";
            }

            if (double.IsNegativeInfinity(value))
            {
                return "-Infinity";
            }

            return value.ToString("0.000");
        }

        private class Item
        {
            public int OriginalIndex { get; set; }
            public double Profit { get; set; }
            public double Weight { get; set; }
            public double Ratio { get; set; }
        }

        private class Node
        {
            public int NodeNumber { get; set; }
            public int Level { get; set; }
            public double CurrentProfit { get; set; }
            public double CurrentWeight { get; set; }
            public double UpperBound { get; set; }
            public int[] Selection { get; set; } = Array.Empty<int>();
        }
    }
}