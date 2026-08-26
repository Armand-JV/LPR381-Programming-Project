using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LPR381Project.NonLinear
{
    /// <summary>Handles the non-linear solver menu and input.</summary>
    public static class NonLinearMenu
    {
        /// <summary>Default convergence tolerance.</summary>
        private const double DefaultTolerance = 1e-6;

        /// <summary>Minimum allowed tolerance.</summary>
        private const double MinimumTolerance = 1e-10;

        /// <summary>Maximum allowed tolerance.</summary>
        private const double MaximumTolerance = 1.0;

        private class Preset
        {
            /// <summary>Preset function expression.</summary>
            public string Expression { get; set; } = string.Empty;

            /// <summary>Default optimisation direction.</summary>
            public bool Maximise { get; set; }

            /// <summary>Default interval for one variable.</summary>
            public double[]? Interval { get; set; }

            /// <summary>Default starting point.</summary>
            public double[]? Start { get; set; }

            /// <summary>Expected result shown in the menu.</summary>
            public string Comment { get; set; } = string.Empty;
        }

        private static readonly List<Preset> Presets = new List<Preset>
        {
            new Preset
            {
                Expression = "x^2",
                Maximise = false,
                Interval = new[] { -5.0, 5.0 },
                Comment = "the brief's own example - convex, minimum at x = 0"
            },
            new Preset
            {
                Expression = "(x-3)^2 + 2",
                Maximise = false,
                Interval = new[] { -5.0, 10.0 },
                Comment = "convex, minimum at x = 3 where f = 2"
            },
            new Preset
            {
                Expression = "x^4 - 3*x^3 + 2",
                Maximise = false,
                Interval = new[] { 0.0, 4.0 },
                Comment = "quartic - unimodal on [0, 4], minimum near x = 2.25"
            },
            new Preset
            {
                Expression = "-(x1-2)^2 - (x2-3)^2 + 10",
                Maximise = true,
                Start = new[] { 0.0, 0.0 },
                Comment = "concave dome - maximum at (2, 3) where f = 10"
            },
            new Preset
            {
                Expression = "x1^2 + x2^2 - x1*x2 - 4*x1",
                Maximise = false,
                Start = new[] { 0.0, 0.0 },
                Comment = "convex bowl with a cross term - minimum at (8/3, 4/3)"
            },
            new Preset
            {
                Expression = "x1^2 - x2^2",
                Maximise = false,
                Start = new[] { 1.0, 1.0 },
                Comment = "a saddle - no max and no min exist, and the Hessian says why"
            },

            // Worked examples from the course notes.
            new Preset
            {
                Expression = "4*sin(x)*(1+cos(x))",
                Maximise = true,
                Interval = new[] { 0.0, 1.57079633 },
                Comment = "LO17 gutter example - max at x = 1.04162483, f = 5.19599088"
            },
            new Preset
            {
                Expression = "x^2 + y^2 + 2*x + 4",
                Maximise = false,
                Start = new[] { 2.0, 1.0 },
                Comment = "LO18 worked example - min at (-1, 0) where f = 3"
            }
        };

        /// <summary>Runs the non-linear menu.</summary>
        public static bool Run()
        {
            EnableUnicodeConsole();

            // Store messages that must survive the next screen clear.
            string? notice = null;

            while (true)
            {
                SafeClear();
                ShowMenu();

                if (notice != null)
                {
                    Console.WriteLine(notice);
                    Console.WriteLine();
                    notice = null;
                }

                if (!TryPrompt("Enter choice (1-" + (Presets.Count + 2) + "): ", out string choiceText))
                {
                    return false;
                }

                if (!int.TryParse(choiceText, out int choice)
                    || choice < 1 || choice > Presets.Count + 2)
                {
                    notice = "Invalid choice. Enter a number between 1 and "
                             + (Presets.Count + 2) + ".";
                    continue;
                }

                if (choice == Presets.Count + 2)
                {
                    return true;
                }

                string expression;
                Preset? preset = null;

                if (choice == Presets.Count + 1)
                {
                    Console.WriteLine();
                    Console.WriteLine("  Use * for multiplication and ^ for powers, e.g. 2*x1^2 - 3*x1*x2.");
                    Console.WriteLine("  Variables are whatever names you type: x, or x1 x2 x3, or x y z.");
                    Console.WriteLine("  Available functions: sin, cos, tan, exp, ln, log, sqrt, abs.");
                    Console.WriteLine("  Constants: pi and e. Symbols also accepted: \u03C0, \u221A, \u00D7, \u00F7, and x\u00B2 for x^2.");
                    Console.WriteLine();

                    if (!TryPrompt("f = ", out expression))
                    {
                        return false;
                    }
                }
                else
                {
                    preset = Presets[choice - 1];
                    expression = preset.Expression;
                }

                ObjectiveFunction function;
                try
                {
                    function = ExpressionParser.Parse(expression);
                }
                catch (ParseException ex)
                {
                    // Point to the position of the parsing error.
                    int caret = Math.Max(0, Math.Min(ex.Position, expression.Length));
                    notice = "Could not read that function:" + Environment.NewLine
                           + "  " + expression + Environment.NewLine
                           + "  " + new string(' ', caret) + "^" + Environment.NewLine
                           + "  " + ex.Message;
                    continue;
                }

                // Clear the menu before showing the solver setup.
                SafeClear();
                Console.WriteLine("=== Non-Linear Optimisation ===");
                Console.WriteLine();
                Console.WriteLine("  Parsed: " + function.Signature());
                Console.WriteLine(string.Format("  {0} variable(s): {1}   ->   {2}",
                    function.Dimension,
                    string.Join(", ", function.Variables),
                    function.Dimension == 1
                        ? "Golden Section Search"
                        : "Steepest Ascent / Descent"));
                Console.WriteLine();

                if (!TryReadDirection(preset, out bool maximise))
                {
                    return false;
                }

                if (!TryReadTolerance(out double tolerance))
                {
                    return false;
                }

                NonLinearSolution? solution;
                try
                {
                    solution = function.Dimension == 1
                        ? SolveSingleVariable(function, maximise, preset, tolerance)
                        : SolveMultiVariable(function, maximise, preset, tolerance);
                }
                catch (Exception ex)
                {
                    notice = "The solver could not finish: " + ex.Message;
                    continue;
                }

                if (solution == null)
                {
                    return false;
                }

                string report = NonLinearReport.Build(solution);
                SafeClear();
                Console.WriteLine(report);

                if (!TryOfferSave(report))
                {
                    return false;
                }

                if (!TryPrompt("Solve another non-linear function? (y/n): ", out string again))
                {
                    return false;
                }

                if (!again.StartsWith("y", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        /// <summary>Prints the preset list.</summary>
        private static void ShowMenu()
        {
            Console.WriteLine();
            Console.WriteLine("=== Non-Linear Optimisation (bonus) ===");
            Console.WriteLine("  One variable    -> Golden Section Search");
            Console.WriteLine("  Two or more     -> Steepest Ascent / Descent");
            Console.WriteLine("                     (its step length is a Golden Section Search too)");
            Console.WriteLine("  Either way the Hessian is built first, to test concave / convex.");
            Console.WriteLine();

            for (int i = 0; i < Presets.Count; i++)
            {
                Preset p = Presets[i];
                var f = ExpressionParser.Parse(p.Expression);
                Console.WriteLine(string.Format("  {0}. {1,-42} {2}",
                    i + 1, f.Signature(), "- " + p.Comment));
            }

            Console.WriteLine(string.Format("  {0}. Enter your own function", Presets.Count + 1));
            Console.WriteLine(string.Format("  {0}. Back to the algorithm menu", Presets.Count + 2));
            Console.WriteLine();
        }

        // Solve paths.

        private static NonLinearSolution? SolveSingleVariable(
            ObjectiveFunction function, bool maximise, Preset? preset, double tolerance)
        {
            double[] defaultInterval = preset?.Interval ?? new[] { -10.0, 10.0 };

            Console.WriteLine();
            Console.WriteLine("Golden section needs a starting interval [a, b] that contains the optimum.");
            Console.WriteLine("Enter two numbers, or press Enter to search for one automatically.");
            Console.WriteLine("  Expressions are fine here: 0 pi/2, or -pi pi, or 0, sqrt(2).");

            if (!TryPrompt(string.Format("Interval [a b] (default {0} {1}): ",
                    NonLinearReport.Fmt(defaultInterval[0]), NonLinearReport.Fmt(defaultInterval[1])),
                out string intervalText))
            {
                return null;
            }

            var solution = new NonLinearSolution
            {
                Function = function,
                Maximise = maximise,
                Method = "Golden Section Search",
                Tolerance = tolerance
            };

            double a;
            double b;

            if (string.IsNullOrWhiteSpace(intervalText))
            {
                a = defaultInterval[0];
                b = defaultInterval[1];
            }
            else if (intervalText.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                if (!AutoBracket(function, maximise, solution, out a, out b))
                {
                    return solution;
                }
            }
            else if (!TryReadNumbers(intervalText, 2, out double[] parsed))
            {
                Console.WriteLine("Could not read two numbers there - searching for an interval instead.");
                if (!AutoBracket(function, maximise, solution, out a, out b))
                {
                    return solution;
                }
            }
            else
            {
                a = parsed[0];
                b = parsed[1];
            }

            if (Math.Abs(b - a) < 1e-12)
            {
                Console.WriteLine("That interval has zero width - widening it to +/- 1.");
                a -= 1.0;
                b += 1.0;
            }

            solution.IntervalA = Math.Min(a, b);
            solution.IntervalB = Math.Max(a, b);

            // Check curvature before running the search.
            solution.Hessian = HessianAnalyzer.Analyse(function,
                new[] { 0.5 * (solution.IntervalA + solution.IntervalB) });

            GoldenSectionResult search = GoldenSectionSearch.Search(
                x => function.Evaluate(x), solution.IntervalA, solution.IntervalB, maximise,
                tolerance);

            solution.GoldenSection = search;
            solution.Solution = new[] { search.Best };
            solution.ObjectiveValue = search.BestValue;
            solution.HessianAtSolution = HessianAnalyzer.Analyse(function, solution.Solution);

            AddSharedNotes(solution);

            if (!search.Converged)
            {
                solution.Notes.Add("The interval never shrank below the tolerance, so the answer is "
                                 + "the best point found rather than a converged one.");
            }

            if (WithinTolerance(search.Best, solution.IntervalA)
                || WithinTolerance(search.Best, solution.IntervalB))
            {
                solution.Notes.Add("The answer sits on the edge of the starting interval, which "
                                 + "usually means the true optimum is outside it. Widen the interval "
                                 + "and run it again.");
            }

            solution.Notes.Add("Golden section assumes f is unimodal on [a, b] - one peak or one "
                             + "trough, no more. On an interval with several, it converges to one "
                             + "of them and cannot tell you which.");

            return solution;
        }

        /// <summary>Solves functions with two or more variables.</summary>
        private static NonLinearSolution? SolveMultiVariable(
            ObjectiveFunction function, bool maximise, Preset? preset, double tolerance)
        {
            int n = function.Dimension;
            double[] defaultStart = preset?.Start ?? new double[n];
            if (defaultStart.Length != n)
            {
                defaultStart = new double[n];
            }

            Console.WriteLine();
            Console.WriteLine("Steepest " + (maximise ? "ascent" : "descent") + " needs a starting point.");
            Console.WriteLine("  Expressions are fine here too, e.g. pi/4 1, or 0, -pi/2.");

            if (!TryPrompt(string.Format("Starting point ({0}) (default {1}): ",
                    string.Join(" ", function.Variables),
                    string.Join(" ", defaultStart.Select(NonLinearReport.Fmt))),
                out string startText))
            {
                return null;
            }

            double[] start;
            if (string.IsNullOrWhiteSpace(startText))
            {
                start = defaultStart;
            }
            else if (!TryReadNumbers(startText, n, out start))
            {
                Console.WriteLine(string.Format(
                    "Could not read {0} numbers there - using the default instead.", n));
                start = defaultStart;
            }

            var solution = new NonLinearSolution
            {
                Function = function,
                Maximise = maximise,
                Method = "Steepest " + (maximise ? "Ascent" : "Descent")
                       + " with an exact Golden Section line search",
                Tolerance = tolerance
            };

            // Check the Hessian at the starting point.
            solution.Hessian = HessianAnalyzer.Analyse(function, start);

            // Use a tighter tolerance for the line search.
            SteepestResult result = SteepestAscentDescent.Solve(
                function, start, maximise,
                tolerance,
                tolerance / NonLinearReport.LineSearchSharpening);

            solution.Steepest = result;
            solution.Solution = result.Solution;
            solution.ObjectiveValue = result.ObjectiveValue;
            solution.IsUnbounded = result.Unbounded;
            solution.HessianAtSolution = HessianAnalyzer.Analyse(function, result.Solution);

            AddSharedNotes(solution);

            if (result.Unbounded)
            {
                solution.Notes.Add("An unbounded result is a real answer, not a failure: this "
                                 + "function genuinely has no finite "
                                 + (maximise ? "maximum" : "minimum")
                                 + ". Adding bounds on the variables would be the way to make the "
                                 + "question well posed.");
            }
            else if (!result.Converged)
            {
                solution.Notes.Add("The gradient never reached the stopping tolerance, so treat the "
                                 + "final point as the best found rather than as a solved optimum.");
            }

            solution.Notes.Add("Steepest ascent/descent is a local method. It finds the optimum in "
                             + "the basin it started in; a different starting point can land on a "
                             + "different one, unless the Hessian test above showed f is concave "
                             + "or convex everywhere.");

            return solution;
        }

        // Helpers.

        /// <summary>Adds notes used by both solve paths.</summary>
        private static void AddSharedNotes(NonLinearSolution solution)
        {
            solution.Notes.Add("Every derivative here is a central finite difference, not a symbolic "
                             + "one, so the last digit or two of a gradient or Hessian entry is "
                             + "numeric noise rather than exact arithmetic.");

            HessianAnalysis? h = solution.Hessian;
            if (h == null)
            {
                return;
            }

            bool wellPosed = solution.Maximise ? h.IsConcave : h.IsConvex;
            if (!wellPosed && h.Curvature != Curvature.Indefinite)
            {
                solution.Notes.Add(string.Format(
                    "The curvature test says this function is the wrong shape for a {0}. The search "
                    + "still ran and the numbers below are real, but read them as a boundary or "
                    + "local answer.",
                    solution.Maximise ? "maximum" : "minimum"));
            }
        }

        /// <summary>Finds a starting interval automatically.</summary>
        private static bool AutoBracket(
            ObjectiveFunction function, bool maximise, NonLinearSolution solution,
            out double a, out double b)
        {
            bool found = GoldenSectionSearch.TryBracket(
                x => function.Evaluate(x), 0.0, 1.0, maximise, out a, out b);

            solution.IntervalWasAutomatic = true;

            if (!found)
            {
                solution.IntervalA = a;
                solution.IntervalB = b;
                solution.Solution = new[] { b };
                solution.ObjectiveValue = function.Evaluate(b);
                solution.IsUnbounded = true;
                solution.Notes.Add("No bracket exists: f kept improving for 60 doublings of the step, "
                                 + "so this problem is unbounded and has no finite optimum.");
                Console.WriteLine();
                Console.WriteLine("f keeps improving without turning - the problem is unbounded.");
                return false;
            }

            Console.WriteLine(string.Format("Found an interval automatically: [{0}, {1}]",
                NonLinearReport.Fmt(a), NonLinearReport.Fmt(b)));
            return true;
        }

        /// <summary>Reads the optimisation direction.</summary>
        private static bool TryReadDirection(Preset? preset, out bool maximise)
        {
            bool defaultMaximise = preset?.Maximise ?? false;
            maximise = defaultMaximise;

            string defaultWord = defaultMaximise ? "max" : "min";

            while (true)
            {
                if (!TryPrompt(string.Format("Maximise or minimise? (max/min, default {0}): ", defaultWord),
                        out string text))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    maximise = defaultMaximise;
                    return true;
                }

                text = text.Trim();

                if (text.StartsWith("max", StringComparison.OrdinalIgnoreCase))
                {
                    maximise = true;
                    return true;
                }

                if (text.StartsWith("min", StringComparison.OrdinalIgnoreCase))
                {
                    maximise = false;
                    return true;
                }

                Console.WriteLine("Please answer max or min.");
            }
        }

        /// <summary>Reads the convergence tolerance.</summary>
        private static bool TryReadTolerance(out double tolerance)
        {
            tolerance = DefaultTolerance;

            Console.WriteLine("Convergence tolerance - how precisely to locate the optimum.");
            Console.WriteLine("  Press Enter for " + NonLinearReport.FmtTolerance(DefaultTolerance)
                              + " (accurate), or try 0.05 for a short, readable table.");

            while (true)
            {
                if (!TryPrompt(string.Format("Tolerance (default {0}): ",
                        NonLinearReport.FmtTolerance(DefaultTolerance)), out string text))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    tolerance = DefaultTolerance;
                    return true;
                }

                if (!TryReadNumber(text, out double value))
                {
                    Console.WriteLine("That is not a number. Try 0.05, or 1e-6, or pi/1000.");
                    continue;
                }

                if (value < MinimumTolerance || value > MaximumTolerance)
                {
                    Console.WriteLine(string.Format(
                        "Please pick a tolerance between {0} and {1}.",
                        NonLinearReport.FmtTolerance(MinimumTolerance),
                        NonLinearReport.FmtTolerance(MaximumTolerance)));
                    Console.WriteLine("  Below the floor, finite-difference noise is larger than "
                                      + "the tolerance itself and the search would chase rounding "
                                      + "error.");
                    continue;
                }

                tolerance = value;
                return true;
            }
        }

        /// <summary>Clears the console when possible.</summary>
        private static void SafeClear()
        {
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.Clear();
                }
            }
            catch (IOException)
            {
                // Continue without clearing.
            }
        }

        /// <summary>Offers to save the report.</summary>
        private static bool TryOfferSave(string report)
        {
            if (!TryPrompt("Save this report to a file? (y/n): ", out string answer))
            {
                return false;
            }

            if (!answer.StartsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!TryPrompt("Output file path (default nonlinear_output.txt): ", out string path))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                path = "nonlinear_output.txt";
            }

            try
            {
                File.WriteAllText(path.Trim(), report);
                Console.WriteLine("Saved to " + Path.GetFullPath(path.Trim()));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not save the file: " + ex.Message);
            }

            return true;
        }

        /// <summary>Parses a fixed number of values, each of which may be an expression.</summary>
        /// <remarks>
        /// Splits on commas when the line has any, so a single entry is allowed to
        /// contain spaces - "0, pi / 2". Without a comma it splits on whitespace, which
        /// is what the prompts ask for and what nearly everyone types; "0 pi/2" works,
        /// "0 pi / 2" does not, and the comma is the way out of that.
        /// </remarks>
        private static bool TryReadNumbers(string text, int count, out double[] values)
        {
            values = Array.Empty<double>();

            char[] separators = text.Contains(',') ? new[] { ',' } : new[] { ' ', '\t' };
            string[] parts = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != count)
            {
                return false;
            }

            var parsed = new double[count];
            for (int i = 0; i < count; i++)
            {
                if (!TryReadNumber(parts[i], out parsed[i]))
                {
                    return false;
                }
            }

            values = parsed;
            return true;
        }

        /// <summary>Reads one value, accepting a plain number or a constant expression.</summary>
        /// <remarks>
        /// Plain numbers are tried first so that scientific notation keeps going through
        /// double.Parse exactly as it always did - the tolerance prompt suggests 1e-6,
        /// and that path must not change. Only when that fails is the text handed to the
        /// expression parser, which is what makes "pi/2" and "sqrt(2)" work here.
        /// </remarks>
        private static bool TryReadNumber(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return ExpressionParser.TryParseConstant(text, out value);
        }

        /// <summary>Switches the console to UTF-8 so typed symbols survive.</summary>
        /// <remarks>
        /// Windows consoles start on a legacy code page that cannot carry pi, the root
        /// sign or a superscript - the bytes are mangled on the way in and the parser is
        /// handed nonsense it can only reject. Switching to UTF-8 fixes that for someone
        /// typing at a real console, which is the case this is for.
        ///
        /// It is deliberately skipped when input is redirected. Assigning
        /// Console.InputEncoding rebuilds the shared stdin reader and throws away
        /// anything the pipe has already buffered, so on a piped run it does not just
        /// fail to help - it swallows the rest of the script. Piped input therefore
        /// keeps the old behaviour and the ASCII spellings ("pi", "sqrt(2)", "x^2"),
        /// which parse identically and are what a test script uses anyway.
        ///
        /// Both setters still throw when no console is attached, so both stay guarded.
        /// Losing the symbols costs the user nothing else.
        /// </remarks>
        private static void EnableUnicodeConsole()
        {
            if (_consoleEncodingSet)
            {
                return;
            }

            _consoleEncodingSet = true;

            if (Console.IsInputRedirected)
            {
                return;
            }

            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch (IOException)
            {
                // No console attached. ASCII spellings still work.
            }
            catch (PlatformNotSupportedException)
            {
                // Same again, on a platform that will not allow the change at all.
            }
        }

        private static bool _consoleEncodingSet;

        /// <summary>Reads a line of user input.</summary>
        private static bool TryPrompt(string prompt, out string value)
        {
            Console.Write(prompt);
            string? line = Console.ReadLine();

            if (line == null)
            {
                Console.WriteLine();
                value = string.Empty;
                return false;
            }

            value = line.Trim();
            return true;
        }

        /// <summary>Checks whether a value is near an interval boundary.</summary>
        private static bool WithinTolerance(double a, double b) => Math.Abs(a - b) < 1e-6;
    }
}
