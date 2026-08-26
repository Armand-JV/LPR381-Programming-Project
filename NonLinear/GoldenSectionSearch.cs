using System;
using System.Collections.Generic;

namespace LPR381Project.NonLinear
{
    /// <summary>Stores one golden section iteration.</summary>
    public class GoldenSectionIteration
    {
        /// <summary>Iteration number.</summary>
        public int Number { get; set; }

        /// <summary>Lower interval bound.</summary>
        public double XLow { get; set; }

        /// <summary>Upper interval bound.</summary>
        public double XHigh { get; set; }

        /// <summary>Golden section offset.</summary>
        public double D { get; set; }

        /// <summary>First interior point.</summary>
        public double X1 { get; set; }

        /// <summary>Second interior point.</summary>
        public double X2 { get; set; }

        /// <summary>Function value at x1.</summary>
        public double F1 { get; set; }

        /// <summary>Function value at x2.</summary>
        public double F2 { get; set; }

        /// <summary>Current interval width.</summary>
        public double Width => XHigh - XLow;

        /// <summary>Bound changed this iteration.</summary>
        public string Kept { get; set; } = string.Empty;
    }

    public class GoldenSectionResult
    {
        /// <summary>Best point found.</summary>
        public double Best { get; set; }

        /// <summary>Function value at the best point.</summary>
        public double BestValue { get; set; }

        /// <summary>Final lower bound.</summary>
        public double FinalLow { get; set; }

        /// <summary>Final upper bound.</summary>
        public double FinalHigh { get; set; }

        /// <summary>Whether the search converged.</summary>
        public bool Converged { get; set; }

        /// <summary>Number of function evaluations.</summary>
        public int Evaluations { get; set; }

        /// <summary>Iteration history.</summary>
        public List<GoldenSectionIteration> Iterations { get; } = new List<GoldenSectionIteration>();
    }

    /// <summary>Performs golden section search for one variable.</summary>
    public static class GoldenSectionSearch
    {
        /// <summary>Golden section ratio.</summary>
        public static readonly double Ratio = (Math.Sqrt(5.0) - 1.0) / 2.0;

        /// <summary>Narrows the interval until the tolerance is reached.</summary>
        public static GoldenSectionResult Search(
            Func<double, double> f,
            double xLow,
            double xHigh,
            bool maximise,
            double tolerance = 1e-6,
            int maxIterations = 200)
        {
            if (xHigh < xLow)
            {
                (xLow, xHigh) = (xHigh, xLow);
            }

            var result = new GoldenSectionResult { FinalLow = xLow, FinalHigh = xHigh };
            int evaluations = 0;

            double Eval(double x)
            {
                evaluations++;
                return f(x);
            }

            if (xHigh - xLow <= tolerance)
            {
                double only = 0.5 * (xLow + xHigh);
                result.Best = only;
                result.BestValue = Eval(only);
                result.Converged = true;
                result.Evaluations = evaluations;
                return result;
            }

            double d = Ratio * (xHigh - xLow);
            double x1 = xLow + d;
            double x2 = xHigh - d;
            double f1 = Eval(x1);
            double f2 = Eval(x2);

            int iteration = 0;
            while (xHigh - xLow > tolerance && iteration < maxIterations)
            {
                iteration++;

                result.Iterations.Add(new GoldenSectionIteration
                {
                    Number = iteration,
                    XLow = xLow,
                    XHigh = xHigh,
                    D = d,
                    X1 = x1,
                    X2 = x2,
                    F1 = f1,
                    F2 = f2,
                    Kept = KeepLeft(f1, f2, maximise)
                        ? "xHigh = x1"
                        : "xLow = x2"
                });

                if (KeepLeft(f1, f2, maximise))
                {
                    // Move the upper bound to x1.
                    xHigh = x1;
                    d = Ratio * (xHigh - xLow);

                    // Reuse the old x2 as the new x1.
                    x1 = x2;
                    f1 = f2;
                    x2 = xHigh - d;
                    f2 = Eval(x2);
                }
                else
                {
                    // Move the lower bound to x2.
                    xLow = x2;
                    d = Ratio * (xHigh - xLow);

                    // Reuse the old x1 as the new x2.
                    x2 = x1;
                    f2 = f1;
                    x1 = xLow + d;
                    f1 = Eval(x1);
                }
            }

            double best = 0.5 * (xLow + xHigh);
            result.Best = best;
            result.BestValue = Eval(best);
            result.FinalLow = xLow;
            result.FinalHigh = xHigh;
            result.Converged = (xHigh - xLow) <= tolerance;
            result.Evaluations = evaluations;
            return result;
        }

        /// <summary>Checks whether to keep the left interval.</summary>
        private static bool KeepLeft(double f1, double f2, bool maximise)
        {
            return Prefers(f2, f1, maximise);
        }

        /// <summary>Checks whether one value is better than another.</summary>
        public static bool Prefers(double candidate, double incumbent, bool maximise)
        {
            if (double.IsNaN(candidate) || double.IsInfinity(candidate))
            {
                return false;
            }

            if (double.IsNaN(incumbent) || double.IsInfinity(incumbent))
            {
                return true;
            }

            return maximise ? candidate > incumbent : candidate < incumbent;
        }

        /// <summary>Finds a starting interval around an optimum.</summary>
        public static bool TryBracket(
            Func<double, double> f,
            double start,
            double initialStep,
            bool maximise,
            out double xLow,
            out double xHigh,
            int maxExpansions = 60)
        {
            xLow = start;
            xHigh = start;

            double fStart = f(start);
            double step = Math.Abs(initialStep) > 0 ? Math.Abs(initialStep) : 1.0;

            // Find the improving direction.
            double right = f(start + step);
            double left = f(start - step);

            double direction;
            if (Prefers(right, fStart, maximise) && !Prefers(left, right, maximise))
            {
                direction = 1.0;
            }
            else if (Prefers(left, fStart, maximise))
            {
                direction = -1.0;
            }
            else
            {
                // The start is already bracketed by its neighbours.
                xLow = start - step;
                xHigh = start + step;
                return true;
            }

            double previous = start;
            double current = start + direction * step;
            double fCurrent = direction > 0 ? right : left;

            for (int i = 0; i < maxExpansions; i++)
            {
                step *= 2.0;
                double next = current + direction * step;
                double fNext = f(next);

                if (!Prefers(fNext, fCurrent, maximise))
                {
                    // Stop when the function no longer improves.
                    xLow = Math.Min(previous, next);
                    xHigh = Math.Max(previous, next);
                    return true;
                }

                previous = current;
                current = next;
                fCurrent = fNext;
            }

            // No bracket was found, so the direction may be unbounded.
            xLow = Math.Min(previous, current);
            xHigh = Math.Max(previous, current);
            return false;
        }
    }
}
