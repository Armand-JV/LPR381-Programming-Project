using System;
using System.Globalization;
using System.IO;
using System.Text;
using LPR381Project.Models;

namespace LPR381Project.IO
{
    /// <summary>
    /// Writes the canonical form, every tableau iteration, and the final
    /// result to an output text file. All decimal values are rounded to
    /// 3 decimal places for display/export, as required by the brief
    /// (full precision is still kept internally by the algorithms).
    ///
    /// Owner: Person 1 (Core App + Simplex).
    /// </summary>
    public static class OutputWriter
    {
        public static void WriteResult(string path, LPModel model, SolutionResult result)
        {
            File.WriteAllText(path, BuildReport(model, result));
        }

        /// <summary>Appends free-form text (e.g. a sensitivity analysis answer) to an
        /// existing output file, so results from several menu actions can build up
        /// in the same file during one session.</summary>
        public static void AppendText(string path, string text)
        {
            File.AppendAllText(path, text + Environment.NewLine + Environment.NewLine);
        }

        public static string BuildReport(LPModel model, SolutionResult result)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== Canonical Form / Model ===");
            sb.AppendLine(model.ToString());
            sb.AppendLine();

            sb.AppendLine(string.Format("=== Algorithm: {0} ===", result.AlgorithmName));
            foreach (var tableau in result.Iterations)
            {
                sb.AppendLine(FormatTableau(tableau));
                sb.AppendLine();
            }

            sb.AppendLine("=== Result ===");
            sb.AppendLine(string.Format("Status: {0}", result.Status));
            if (result.Status == SolutionStatus.Optimal && result.VariableValues != null)
            {
                sb.AppendLine(string.Format("Objective value: {0}", Round(result.ObjectiveValue)));
                for (int i = 0; i < result.VariableValues.Length; i++)
                {
                    sb.AppendLine(string.Format("x{0} = {1}", i + 1, Round(result.VariableValues[i])));
                }
            }

            if (result.Notes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Notes ===");
                foreach (var note in result.Notes)
                {
                    sb.AppendLine(note);
                }
            }

            return sb.ToString();
        }

        private static string FormatTableau(Tableau t)
        {
            var sb = new StringBuilder();
            sb.AppendLine(t.Label);
            sb.AppendLine("\t" + string.Join("\t", t.ColumnHeaders));
            for (int r = 0; r < t.RowCount; r++)
            {
                string rowLabel = r == 0 ? "z" : t.BasicVariables[r - 1];
                sb.Append(rowLabel + "\t");
                for (int c = 0; c < t.ColumnCount; c++)
                {
                    sb.Append(Round(t.Values[r, c]).ToString(CultureInfo.InvariantCulture) + "\t");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Rounds to 3 decimal places, per the brief's output-format rule.</summary>
        public static double Round(double value)
        {
            return Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }
    }
}
