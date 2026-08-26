using System;
using System.Collections.Generic;
using System.Globalization;

namespace LPR381Project.NonLinear
{
    /// <summary>Stores one steepest ascent/descent iteration.</summary>
    public class SteepestIteration
    {
        /// <summary>Iteration number.</summary>
        public int Number { get; set; }

        /// <summary>Starting point.</summary>
        public double[] Point { get; set; } = Array.Empty<double>();

        /// <summary>Function value at the current point.</summary>
        public double Value { get; set; }

        /// <summary>Gradient at the current point.</summary>
        public double[] Gradient { get; set; } = Array.Empty<double>();

        /// <summary>Gradient norm.</summary>
        public double GradientNorm { get; set; }

        /// <summary>Step size.</summary>
        public double StepSize { get; set; }

        /// <summary>Whether a step was taken.</summary>
        public bool StepTaken { get; set; }

        /// <summary>Next point.</summary>
        public double[] NextPoint { get; set; } = Array.Empty<double>();

        /// <summary>Function value at the next point.</summary>
        public double NextValue { get; set; }

    }

    public class SteepestResult
    {
        /// <summary>Final point.</summary>
        public double[] Solution { get; set; } = Array.Empty<double>();

        /// <summary>Function value at the solution.</summary>
        public double ObjectiveValue { get; set; }

        /// <summary>Whether the search converged.</summary>
        public bool Converged { get; set; }

        /// <summary>Whether the search is unbounded.</summary>
        public bool Unbounded { get; set; }

        /// <summary>Reason the search stopped.</summary>
        public string StopReason { get; set; } = string.Empty;

        /// <summary>Number of function evaluations.</summary>
        public int Evaluations { get; set; }

        /// <summary>Iteration history.</summary>
        public List<SteepestIteration> Iterations { get; } = new List<SteepestIteration>();
    }

    /// <summary>Performs steepest ascent or descent.</summary>
    public static class SteepestAscentDescent
    {
        /// <summary>Maximum distance used to detect divergence.</summary>
        private const double DivergenceLimit = 1e12;

        /// <summary>Runs the steepest ascent/descent search.</summary>
        public static SteepestResult Solve(
            ObjectiveFunction f,
            double[] start,
            bool maximise,
            double gradientTolerance = 1e-6,
            double lineSearchTolerance = 1e-8,
            int maxIterations = 200)
        {
            var result = new SteepestResult();
            int startingEvaluations = f.EvaluationCount;

            double[] x = (double[])start.Clone();
            double value = f.Evaluate(x);

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                double[] gradient = NumericCalculus.Gradient(f, x);
                double norm = NumericCalculus.Norm(gradient);

                var row = new SteepestIteration
                {
                    Number = iteration,
                    Point = (double[])x.Clone(),
                    Value = value,
                    Gradient = (double[])gradient.Clone(),
                    GradientNorm = norm
                };

                if (!IsFinite(norm))
                {
                    row.NextPoint = (double[])x.Clone();
                    row.NextValue = value;
                    result.Iterations.Add(row);
                    result.StopReason = "The gradient could not be evaluated (the function is not "
                                      + "differentiable at this point, or it overflowed).";
                    break;
                }

                // Stop when the gradient is close to zero.
                if (norm < gradientTolerance)
                {
                    row.NextPoint = (double[])x.Clone();
                    row.NextValue = value;
                    result.Iterations.Add(row);
                    result.Converged = true;
                    result.StopReason = string.Format(
                        CultureInfo.InvariantCulture,
                        "Converged: the gradient is zero here (||grad f|| = {0:G6}, below the "
                        + "tolerance {1:G6}), so this is a stationary point.",
                        norm, gradientTolerance);
                    break;
                }

                // Reduce the line search to one variable.
                double[] current = x;
                double[] direction = gradient;
                Func<double, double> g = h =>
                {
                    var probe = new double[current.Length];
                    for (int i = 0; i < current.Length; i++)
                    {
                        probe[i] = current[i] + h * direction[i];
                    }

                    return f.Evaluate(probe);
                };

                if (!TryBracketStep(g, maximise, norm, out double bracketEnd))
                {
                    row.NextPoint = (double[])x.Clone();
                    row.NextValue = value;
                    result.Iterations.Add(row);
                    result.Unbounded = true;
                    result.StopReason = "The function keeps improving along this direction without "
                                      + "turning, so the problem is unbounded.";
                    break;
                }

                // Use a positive step for ascent and a negative step for descent.
                double low = Math.Min(0.0, bracketEnd);
                double high = Math.Max(0.0, bracketEnd);

                GoldenSectionResult line = GoldenSectionSearch.Search(
                    g, low, high, maximise, lineSearchTolerance);

                row.StepSize = line.Best;
                row.StepTaken = true;

                // Apply the chosen step to all variables.
                var next = new double[x.Length];
                for (int i = 0; i < x.Length; i++)
                {
                    next[i] = x[i] + line.Best * gradient[i];
                }

                double nextValue = f.Evaluate(next);
                row.NextPoint = (double[])next.Clone();
                row.NextValue = nextValue;
                result.Iterations.Add(row);

                // Stop if the point moves beyond the divergence limit.
                if (NumericCalculus.Norm(next) >= DivergenceLimit || !IsFinite(nextValue))
                {
                    x = next;
                    value = nextValue;
                    result.Unbounded = true;
                    result.StopReason = "The search ran away from the origin without ever turning, "
                                      + "so the problem is unbounded and has no finite optimum.";
                    break;
                }

                // Stop if the line search cannot improve the objective.
                if (!GoldenSectionSearch.Prefers(nextValue, value, maximise))
                {
                    result.StopReason = string.Format(
                        CultureInfo.InvariantCulture,
                        "Stopped: the step size no longer improves on f = {0:G6}. The point is "
                        + "optimal to within the resolution of the numeric gradient "
                        + "(||grad f|| = {1:G6}).", value, norm);
                    result.Converged = true;
                    break;
                }

                x = next;
                value = nextValue;

                if (iteration == maxIterations)
                {
                    result.StopReason = string.Format(
                        CultureInfo.InvariantCulture,
                        "Stopped at the iteration limit of {0} without the gradient reaching the "
                        + "tolerance.", maxIterations);
                }
            }

            result.Solution = x;
            result.ObjectiveValue = value;
            result.Evaluations = f.EvaluationCount - startingEvaluations;

            if (string.IsNullOrEmpty(result.StopReason))
            {
                result.StopReason = "Stopped at the iteration limit.";
            }

            return result;
        }

        /// <summary>Finds a bracket for the line-search step.</summary>
        private static bool TryBracketStep(
            Func<double, double> g, bool maximise, double gradientNorm, out double bracketEnd,
            int maxDoublings = 60)
        {
            double sign = maximise ? 1.0 : -1.0;
            double limit = DivergenceLimit / Math.Max(gradientNorm, 1e-300);

            double previousValue = g(0.0);
            double h = sign / Math.Max(gradientNorm, 1e-300);

            for (int i = 0; i < maxDoublings; i++)
            {
                double value = g(h);

                if (!GoldenSectionSearch.Prefers(value, previousValue, maximise))
                {
                    bracketEnd = h;
                    return true;
                }

                // No turning point was found before the step limit.
                if (Math.Abs(h) >= limit)
                {
                    bracketEnd = h;
                    return false;
                }

                previousValue = value;
                h = sign * Math.Min(Math.Abs(h) * 2.0, limit);
            }

            bracketEnd = h;
            return false;
        }

        /// <summary>Checks that a value is finite.</summary>
        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
