using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LPR381Project.NonLinear
{
    // Non-linear solution data.

    /// <summary>Stores the result of one non-linear run.</summary>
    public class NonLinearSolution
    {
        /// <summary>Parsed objective function.</summary>
        public ObjectiveFunction Function { get; set; } = null!;

        /// <summary>Whether the objective is maximised.</summary>
        public bool Maximise { get; set; }

        /// <summary>Algorithm name.</summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>Hessian analysis at the starting point.</summary>
        public HessianAnalysis? Hessian { get; set; }

        /// <summary>Hessian analysis at the final point.</summary>
        public HessianAnalysis? HessianAtSolution { get; set; }

        /// <summary>Golden section result.</summary>
        public GoldenSectionResult? GoldenSection { get; set; }

        /// <summary>Convergence tolerance.</summary>
        public double Tolerance { get; set; }

        /// <summary>Starting interval lower bound.</summary>
        public double IntervalA { get; set; }

        /// <summary>Starting interval upper bound.</summary>
        public double IntervalB { get; set; }

        /// <summary>Whether the interval was found automatically.</summary>
        public bool IntervalWasAutomatic { get; set; }

        /// <summary>Steepest ascent/descent result.</summary>
        public SteepestResult? Steepest { get; set; }

        /// <summary>Optimal point.</summary>
        public double[] Solution { get; set; } = Array.Empty<double>();

        /// <summary>Function value at the solution.</summary>
        public double ObjectiveValue { get; set; }

        /// <summary>Whether the problem is unbounded.</summary>
        public bool IsUnbounded { get; set; }

        /// <summary>Solver notes.</summary>
        public List<string> Notes { get; } = new List<string>();

        /// <summary>Objective direction for the report.</summary>
        public string ObjectiveWord => Maximise ? "Maximise" : "Minimise";
    }

    // Report writer.

    /// <summary>Builds the non-linear solution report.</summary>
    public static class NonLinearReport
    {
        /// <summary>Report width.</summary>
        private const int Width = 78;

        /// <summary>Line-search tolerance multiplier.</summary>
        public const double LineSearchSharpening = 100.0;

        /// <summary>Builds the full report.</summary>
        public static string Build(NonLinearSolution solution)
        {
            var sb = new StringBuilder();
            bool oneVariable = solution.Function.Dimension == 1;

            Rule(sb, '=');
            sb.AppendLine("  NON-LINEAR PROGRAMMING");
            Rule(sb, '=');
            sb.AppendLine();
            sb.AppendLine("  " + solution.ObjectiveWord + "   " + solution.Function.Signature());
            sb.AppendLine("  Variables         : " + string.Join(", ", solution.Function.Variables));
            sb.AppendLine("  Numerical method  : " + solution.Method);
            sb.AppendLine("  Stopping criteria : " + FmtTolerance(solution.Tolerance)
                          + (oneVariable ? "   (on xHigh - xLow)" : "   (on the gradient)"));
            sb.AppendLine();

            int step = 1;

            if (solution.Hessian != null)
            {
                AppendCurvature(sb, solution, step++);
            }

            if (solution.GoldenSection != null)
            {
                AppendGoldenSection(sb, solution, step++);
            }

            if (solution.Steepest != null)
            {
                AppendSteepest(sb, solution, step++);
            }

            AppendSolution(sb, solution);

            if (solution.Notes.Count > 0)
            {
                Rule(sb, '-');
                sb.AppendLine("  NOTES");
                Rule(sb, '-');
                foreach (string note in solution.Notes)
                {
                    bool first = true;
                    foreach (string line in Wrap(note, Width - 4))
                    {
                        sb.AppendLine((first ? "  - " : "    ") + line);
                        first = false;
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        // Convexity test (LO15).

        private static void AppendCurvature(StringBuilder sb, NonLinearSolution solution, int step)
        {
            HessianAnalysis h = solution.Hessian!;
            IReadOnlyList<string> names = solution.Function.Variables;
            int n = names.Count;

            Rule(sb, '-');
            sb.AppendLine(string.Format("  STEP {0} - IS THE FUNCTION CONVEX OR CONCAVE?", step));
            Rule(sb, '-');

            if (n == 1)
            {
                // Use the second derivative for one variable.
                sb.AppendLine("  Second derivative test, at " + PointText(names, h.Point) + ":");
                sb.AppendLine();
                sb.AppendLine(string.Format("      f''({0}) = {1}",
                    names[0], FmtDerivative(h.Matrix[0, 0], MatrixScale(h.Matrix))));
                sb.AppendLine();
                sb.AppendLine("      f''(x) > 0  ->  convex   (the function has a local minimum)");
                sb.AppendLine("      f''(x) < 0  ->  concave  (the function has a local maximum)");
            }
            else
            {
                sb.AppendLine("  Hessian matrix of second partial derivatives, at "
                              + PointText(names, h.Point) + ":");
                sb.AppendLine();

                double scale = MatrixScale(h.Matrix);

                sb.AppendLine("      H =       " + string.Join("", names.Select(x => Pad(x, 12))).TrimEnd());
                for (int r = 0; r < n; r++)
                {
                    var cells = new StringBuilder();
                    for (int c = 0; c < n; c++)
                    {
                        cells.Append(Pad(FmtDerivative(h.Matrix[r, c], scale), 12));
                    }

                    sb.AppendLine("          " + Pad(names[r], 6) + cells.ToString().TrimEnd());
                }

                sb.AppendLine();
                AppendPrincipalMinors(sb, h, scale);
                sb.AppendLine();
                sb.AppendLine("      Determinant of H:  det(H) = "
                              + FmtDerivative(h.Determinant, Math.Pow(scale, n)));
                sb.AppendLine();
                sb.AppendLine("      det(H) > 0 and d2f/d" + names[1] + "^2 > 0  ->  local minimum (convex)");
                sb.AppendLine("      det(H) > 0 and d2f/d" + names[1] + "^2 < 0  ->  local maximum (concave)");
                sb.AppendLine("      det(H) < 0                     ->  saddle point");
                sb.AppendLine();
                sb.AppendLine(string.Format("      Here det(H) = {0} and d2f/d{1}^2 = {2}",
                    FmtDerivative(h.Determinant, Math.Pow(scale, n)),
                    names[1], FmtDerivative(h.SecondDiagonal, scale)));

                if (h.DeterminantRuleInconclusive)
                {
                    sb.AppendLine();
                    foreach (string line in Wrap(
                        "det(H) = 0, so the determinant rule gives no verdict here. The full set "
                        + "of principal minors above decides it instead: all non-negative for a "
                        + "convex f, all non-positive at odd orders for a concave one.", Width - 6))
                    {
                        sb.AppendLine("      " + line);
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("  RESULT: f is " + h.CurvatureText());
            sb.AppendLine();

            foreach (string line in Wrap(h.Implication(solution.Maximise), Width - 4))
            {
                sb.AppendLine("  " + line);
            }

            sb.AppendLine();
        }

        /// <summary>Prints the principal minors by order.</summary>
        private static void AppendPrincipalMinors(StringBuilder sb, HessianAnalysis h, double scale)
        {
            double[][] byOrder = h.PrincipalMinorsByOrder;
            if (byOrder.Length == 0)
            {
                return;
            }

            sb.AppendLine("      Principal minors:");

            for (int k = 0; k < byOrder.Length; k++)
            {
                string label = k == 0
                    ? "first (diagonal entries)"
                    : k == byOrder.Length - 1
                        ? string.Format("order {0} (det of H)", k + 1)
                        : string.Format("order {0}", k + 1);

                double orderScale = Math.Pow(scale, k + 1);
                sb.AppendLine(string.Format("        {0,-26}{1}",
                    label, string.Join(",  ", byOrder[k].Select(v => FmtDerivative(v, orderScale)))));
            }
        }

        // Golden section search (LO17).

        private static void AppendGoldenSection(StringBuilder sb, NonLinearSolution solution, int step)
        {
            GoldenSectionResult g = solution.GoldenSection!;

            Rule(sb, '-');
            sb.AppendLine(string.Format("  STEP {0} - GOLDEN SECTION SEARCH", step));
            Rule(sb, '-');
            sb.AppendLine(string.Format("  Initial interval : [{0}, {1}]{2}",
                Fmt(solution.IntervalA),
                Fmt(solution.IntervalB),
                solution.IntervalWasAutomatic ? "   (found automatically)" : string.Empty));
            sb.AppendLine("  Golden ratio     : r = (sqrt(5) - 1) / 2 = "
                          + GoldenSectionSearch.Ratio.ToString("0.00000000", CultureInfo.InvariantCulture));
            sb.AppendLine("  Intermediate pts : d = r (xHigh - xLow),   x1 = xLow + d,   x2 = xHigh - d");
            sb.AppendLine("  Stop when        : xHigh - xLow < " + FmtTolerance(solution.Tolerance));
            sb.AppendLine();

            string[] headers = { "it", "xLow", "xHigh", "d", "x1", "x2", "f(x1)", "f(x2)",
                                 "xHigh-xLow", "new bound" };
            int[] widths = { 4, 12, 12, 12, 12, 12, 13, 13, 12, 12 };

            AppendRow(sb, headers, widths);
            sb.AppendLine("  " + new string('-', widths.Sum()));

            foreach (GoldenSectionIteration row in g.Iterations)
            {
                AppendRow(sb, new[]
                {
                    row.Number.ToString(CultureInfo.InvariantCulture),
                    Fmt(row.XLow), Fmt(row.XHigh), Fmt(row.D),
                    Fmt(row.X1), Fmt(row.X2), Fmt(row.F1), Fmt(row.F2),
                    Fmt(row.Width), row.Kept
                }, widths);
            }

            sb.AppendLine();
            sb.AppendLine(string.Format("  Final interval of uncertainty: [{0}, {1}]   length {2}",
                Fmt(g.FinalLow), Fmt(g.FinalHigh), Fmt(g.FinalHigh - g.FinalLow)));
            sb.AppendLine(string.Format("  Iterations: {0}    function evaluations: {1}",
                g.Iterations.Count, g.Evaluations));
            sb.AppendLine();
        }

        // Steepest ascent/descent (LO18).

        private static void AppendSteepest(StringBuilder sb, NonLinearSolution solution, int step)
        {
            SteepestResult s = solution.Steepest!;
            IReadOnlyList<string> names = solution.Function.Variables;

            Rule(sb, '-');
            sb.AppendLine(string.Format("  STEP {0} - STEEPEST {1}", step,
                solution.Maximise ? "ASCENT" : "DESCENT"));
            Rule(sb, '-');
            sb.AppendLine("  Update formula : x(k+1) = x(k) + h * grad f(x(k))");
            sb.AppendLine("  Step size h    : from golden section on g(h) = f(x + h * grad f),");
            sb.AppendLine("                   solved to "
                          + FmtTolerance(solution.Tolerance / LineSearchSharpening)
                          + ", so h is never what limits accuracy");
            sb.AppendLine("  Stop when      : grad f = 0   (||grad f|| < "
                          + FmtTolerance(solution.Tolerance) + ")");
            sb.AppendLine();

            var headers = new List<string> { "it" };
            var widths = new List<int> { 4 };

            foreach (string name in names)
            {
                headers.Add(name);
                widths.Add(12);
            }

            headers.Add("f(x)");
            widths.Add(14);

            foreach (string name in names)
            {
                headers.Add("df/d" + name);
                widths.Add(13);
            }

            headers.Add("||grad f||");
            widths.Add(13);
            headers.Add("h");
            widths.Add(13);

            AppendRow(sb, headers.ToArray(), widths.ToArray());
            sb.AppendLine("  " + new string('-', widths.Sum()));

            foreach (SteepestIteration row in s.Iterations)
            {
                var cells = new List<string> { row.Number.ToString(CultureInfo.InvariantCulture) };
                cells.AddRange(row.Point.Select(Fmt));
                cells.Add(Fmt(row.Value));

                // Show very small gradient values as zero.
                cells.AddRange(row.Gradient.Select(v => FmtSignificant(Snap(v, solution.Tolerance))));
                cells.Add(FmtSignificant(Snap(row.GradientNorm, solution.Tolerance)));

                // No step is taken when the gradient is zero.
                cells.Add(row.StepTaken ? Fmt(row.StepSize) : "-");

                AppendRow(sb, cells.ToArray(), widths.ToArray());
            }

            sb.AppendLine();
            sb.AppendLine(string.Format("  Iterations: {0}    function evaluations: {1}",
                s.Iterations.Count, s.Evaluations));
            sb.AppendLine();

            foreach (string line in Wrap(s.StopReason, Width - 4))
            {
                sb.AppendLine("  " + line);
            }

            sb.AppendLine();
        }

        // Conclusion.

        private static void AppendSolution(StringBuilder sb, NonLinearSolution solution)
        {
            Rule(sb, '=');
            sb.AppendLine("  SOLUTION");
            Rule(sb, '=');

            IReadOnlyList<string> names = solution.Function.Variables;
            string word = solution.Maximise ? "maximum" : "minimum";

            if (solution.IsUnbounded)
            {
                sb.AppendLine("  UNBOUNDED - f improves without limit, so there is no finite "
                              + word + ".");
                sb.AppendLine();
                sb.AppendLine("  Last point examined (where the search gave up, not an answer):");
                sb.AppendLine("      " + PointText(names, solution.Solution)
                              + "   f = " + Fmt(solution.ObjectiveValue));
                sb.AppendLine();
                return;
            }

            // Show the midpoint of the final interval.
            if (solution.GoldenSection != null)
            {
                GoldenSectionResult g = solution.GoldenSection;
                sb.AppendLine("  Optimal point = (xLow + xHigh) / 2");
                sb.AppendLine(string.Format("                = ({0} + {1}) / 2",
                    Fmt(g.FinalLow), Fmt(g.FinalHigh)));
                sb.AppendLine("                = " + Fmt(solution.Solution[0]));
                sb.AppendLine();
            }

            sb.AppendLine(string.Format("  The {0} of the function is at the point {1}",
                word, PointText(names, solution.Solution)));
            sb.AppendLine(string.Format("  and f({0}) = {1}",
                string.Join(", ", solution.Solution.Select(Fmt)),
                Fmt(solution.ObjectiveValue)));
            sb.AppendLine();

            // Use curvature to classify the stationary point.
            if (solution.HessianAtSolution != null)
            {
                HessianAnalysis end = solution.HessianAtSolution;
                sb.AppendLine("  Check at this point: " + end.CurvatureText());
                foreach (string line in Wrap(StationaryVerdict(end, solution.Maximise), Width - 6))
                {
                    sb.AppendLine("      " + line);
                }

                sb.AppendLine();
            }
        }

        /// <summary>Classifies the final point.</summary>
        private static string StationaryVerdict(HessianAnalysis end, bool maximise)
        {
            if (end.Curvature == Curvature.Indefinite)
            {
                return "=> NOT an optimum. f curves up in one direction and down in another here, "
                     + "so this is a saddle rather than a true "
                     + (maximise ? "maximum" : "minimum") + ".";
            }

            if (end.Curvature == Curvature.StrictlyConcave)
            {
                return "=> a strict local MAXIMUM.";
            }

            if (end.Curvature == Curvature.StrictlyConvex)
            {
                return "=> a strict local MINIMUM.";
            }

            return "=> the curvature is flat in at least one direction, so the second-order test "
                 + "is inconclusive here: this may be a non-strict optimum or a flat ridge.";
        }

        // Formatting helpers.

        /// <summary>Formats tolerance values.</summary>
        public static string FmtTolerance(double value)
        {
            return value.ToString("G3", CultureInfo.InvariantCulture);
        }

        /// <summary>Formats standard numeric output.</summary>
        public static string Fmt(double value)
        {
            if (double.IsNaN(value))
            {
                return "NaN";
            }

            if (double.IsPositiveInfinity(value))
            {
                return "+inf";
            }

            if (double.IsNegativeInfinity(value))
            {
                return "-inf";
            }

            double rounded = Math.Round(value, 8, MidpointRounding.AwayFromZero);
            if (rounded == 0.0)
            {
                rounded = 0.0;
            }

            return rounded.ToString("0.########", CultureInfo.InvariantCulture);
        }

        /// <summary>Formats numerical derivatives.</summary>
        public static string FmtDerivative(double value, double scale)
        {
            return FmtSignificant(Snap(value, 1e-6 * Math.Abs(scale)));
        }

        /// <summary>Sets very small values to zero.</summary>
        private static double Snap(double value, double threshold)
        {
            return Math.Abs(value) < threshold ? 0.0 : value;
        }

        /// <summary>Formats compact numeric output.</summary>
        private static string FmtSignificant(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return Fmt(value);
            }

            return value.ToString("G6", CultureInfo.InvariantCulture);
        }

        /// <summary>Finds the largest absolute matrix value.</summary>
        private static double MatrixScale(double[,] m)
        {
            double max = 0.0;
            foreach (double v in m)
            {
                if (!double.IsNaN(v) && !double.IsInfinity(v))
                {
                    max = Math.Max(max, Math.Abs(v));
                }
            }

            return max > 0.0 ? max : 1.0;
        }

        /// <summary>Formats a point for display.</summary>
        public static string PointText(IReadOnlyList<string> names, double[] point)
        {
            var parts = new List<string>();
            for (int i = 0; i < names.Count && i < point.Length; i++)
            {
                parts.Add(string.Format("{0} = {1}", names[i], Fmt(point[i])));
            }

            return "(" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes a horizontal rule.</summary>
        private static void Rule(StringBuilder sb, char c)
        {
            sb.AppendLine(new string(c, Width));
        }

        /// <summary>Writes a table row.</summary>
        private static void AppendRow(StringBuilder sb, string[] cells, int[] widths)
        {
            var line = new StringBuilder("  ");
            for (int i = 0; i < cells.Length; i++)
            {
                line.Append(Pad(cells[i], i < widths.Length ? widths[i] : 12));
            }

            sb.AppendLine(line.ToString().TrimEnd());
        }

        /// <summary>Pads a table cell.</summary>
        private static string Pad(string text, int width)
        {
            if (text.Length >= width)
            {
                return text + " ";
            }

            return text.PadRight(width);
        }

        /// <summary>Wraps text to the report width.</summary>
        private static IEnumerable<string> Wrap(string text, int width)
        {
            var line = new StringBuilder();

            foreach (string word in text.Trim().Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(word);
            }

            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }
    }
}
