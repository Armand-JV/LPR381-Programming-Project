using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LPR381Project.Models;
using DecimalLpModel = Algorithms.PrimalSimplex.LpModel;
using DecimalRevisedPrimalSimplexSolver = Algorithms.PrimalSimplex.RevisedPrimalSimplexSolver;

namespace LPR381Project.Algorithms.PrimalSimplex
{
    public sealed class RevisedPrimalSimplexSolver : IAlgorithm
    {
        public string Name => "Revised Primal Simplex";

        public SolutionResult Solve(LPModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var decimalModel = ToDecimalModel(model);
            var log = new DecimalRevisedPrimalSimplexSolver().Run(decimalModel);
            var result = new SolutionResult
            {
                AlgorithmName = Name,
                Status = GetStatus(log)
            };

            result.Notes.AddRange(log);
            ReadSolution(log, model.ObjectiveCoefficients.Length, result);
            return result;
        }

        private static DecimalLpModel ToDecimalModel(LPModel model)
        {
            int variableCount = model.ObjectiveCoefficients.Length;
            int constraintCount = model.Constraints.Count;
            var matrix = new decimal[constraintCount, variableCount + constraintCount];
            var rhs = new decimal[constraintCount];

            for (int i = 0; i < constraintCount; i++)
            {
                var constraint = model.Constraints[i];
                if (constraint.Coefficients.Length != variableCount)
                {
                    throw new ArgumentException(
                        $"Constraint {i + 1} has {constraint.Coefficients.Length} coefficients but objective has {variableCount}.",
                        nameof(model));
                }

                for (int j = 0; j < variableCount; j++)
                {
                    matrix[i, j] = Convert.ToDecimal(constraint.Coefficients[j]);
                }

                matrix[i, variableCount + i] = 1m;
                rhs[i] = Convert.ToDecimal(constraint.Rhs);
            }

            var objective = new decimal[variableCount + constraintCount];
            for (int j = 0; j < variableCount; j++)
            {
                objective[j] = Convert.ToDecimal(model.ObjectiveCoefficients[j]);
            }

            var variableNames = Enumerable.Range(0, variableCount)
                .Select(i => $"x{i + 1}")
                .Concat(Enumerable.Range(0, constraintCount).Select(i => $"s{i + 1}"))
                .ToArray();

            return new DecimalLpModel
            {
                IsMax = model.Objective == ObjectiveType.Max,
                C = objective,
                A = matrix,
                B = rhs,
                VarNames = variableNames
            };
        }

        private static SolutionStatus GetStatus(IReadOnlyList<string> log)
        {
            if (log.Any(line => line == "Optimal reached")) return SolutionStatus.Optimal;
            if (log.Any(line => line.StartsWith("Unbounded", StringComparison.Ordinal))) return SolutionStatus.Unbounded;
            if (log.Any(line => line.StartsWith("Error inverting B:", StringComparison.Ordinal))) return SolutionStatus.Infeasible;
            return SolutionStatus.NotSolved;
        }

        private static void ReadSolution(IReadOnlyList<string> log, int variableCount, SolutionResult result)
        {
            if (result.Status != SolutionStatus.Optimal) return;

            int solutionStart = -1;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i] == "Solution:")
                {
                    solutionStart = i + 1;
                    break;
                }
            }

            if (solutionStart < 0) return;

            var values = new double[variableCount];
            for (int i = 0; i < variableCount && solutionStart + i < log.Count; i++)
            {
                string[] parts = log[solutionStart + i].Split('=', 2);
                if (parts.Length != 2 || !double.TryParse(
                        parts[1].Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out values[i]))
                {
                    return;
                }
            }

            result.VariableValues = values;
            string objectiveLine = log.FirstOrDefault(line => line.StartsWith("Objective = ", StringComparison.Ordinal));
            if (objectiveLine != null && double.TryParse(
                    objectiveLine.Substring("Objective = ".Length),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double objective))
            {
                result.ObjectiveValue = objective;
            }
        }
    }
}
