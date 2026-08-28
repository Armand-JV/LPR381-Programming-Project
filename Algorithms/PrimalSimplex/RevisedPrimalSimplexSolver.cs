using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Algorithms.PrimalSimplex
{
    public class RevisedPrimalSimplexSolver
    {
        public int RoundingDigits { get; set; } = 6;

        public RevisedPrimalSimplexSolver() { }

        public void RunAndPrint(LpModel model, TextWriter output = null)
        {
            output ??= Console.Out;
            var log = Run(model);
            foreach (var line in log) output.WriteLine(line);
        }

        public List<string> Run(LpModel model)
        {
            var lines = new List<string>();
            int m = model.A.GetLength(0);
            int n = model.A.GetLength(1);

            // initial basis: slack variables (last m columns)
            var basis = new int[m];
            for (int i = 0; i < m; i++) basis[i] = n - m + i; // indices of slack

            var nonbasis = Enumerable.Range(0, n).Where(j => !basis.Contains(j)).ToList();

            int iter = 0;
            const int maxIter = 200;

            while (true)
            {
                iter++;
                if (iter > maxIter) throw new InvalidOperationException("Maximum iterations exceeded");

                lines.Add(string.Empty);
                lines.Add($"==================== Iteration {iter} ====================");
                lines.Add($"Problem type: {(model.IsMax ? "Maximization" : "Minimization")}");

                // Build B and B^-1
                var B = MatrixExtensions.GetColumns(model.A, basis);
                decimal[,] Binv;
                try
                {
                    Binv = MatrixExtensions.Inverse(B);
                }
                catch (Exception ex)
                {
                    lines.Add($"Error inverting B: {ex.Message}");
                    break;
                }

                // xb = B^-1 * b
                var xb = MatrixExtensions.Multiply(Binv, model.B);

                // c_B
                var cB = new decimal[m];
                for (int i = 0; i < m; i++) cB[i] = model.C[basis[i]];

                // y^T = c_B^T * B^-1
                var yT = MatrixExtensions.MultiplyRowVector(cB, Binv);

                // reduced costs
                var reduced = new decimal[n];
                for (int j = 0; j < n; j++)
                {
                    var col = new decimal[m];
                    for (int i = 0; i < m; i++) col[i] = model.A[i, j];
                    var yTa = MatrixExtensions.Dot(yT, col);
                    reduced[j] = model.C[j] - yTa;
                    if (!model.IsMax) reduced[j] = -reduced[j]; // convert to maximization internally
                }

                lines.Add(string.Empty);
                lines.Add("Basis information:");
                lines.Add("  Basic variables: " + string.Join(", ", basis.Select(i => model.VarNames[i])));
                lines.Add("  Non-basic variables: " + string.Join(", ", nonbasis.Select(i => model.VarNames[i])));
                lines.Add(string.Empty);
                lines.Add("B matrix (basic columns):");
                lines.AddRange(FormatMatrix(B, model.VarNames, basis));
                lines.Add(string.Empty);
                lines.Add("B^-1 matrix:");
                lines.AddRange(FormatMatrix(Binv, model.VarNames, basis));
                lines.Add(string.Empty);
                lines.Add("Basic solution (x_B):");
                for (int i = 0; i < m; i++) lines.Add($"  {model.VarNames[basis[i]]} = {RoundStr(xb[i])}");

                lines.Add(string.Empty);
                lines.Add("Basic costs (c_B):");
                for (int i = 0; i < m; i++) lines.Add($"  {model.VarNames[basis[i]]} = {RoundStr(cB[i])}");

                lines.Add(string.Empty);
                lines.Add("Dual prices (y^T):");
                for (int i = 0; i < m; i++) lines.Add($"  y{i + 1} = {RoundStr(yT[i])}");

                lines.Add(string.Empty);
                lines.Add("Reduced costs:");
                for (int j = 0; j < n; j++) lines.Add($"  {model.VarNames[j]} = {RoundStr(reduced[j])}");

                // Choose entering variable: positive reduced cost (since we've normalized for maximization)
                decimal bestVal = 0m;
                int entering = -1;
                for (int j = 0; j < n; j++)
                {
                    if (nonbasis.Contains(j) && reduced[j] > bestVal)
                    {
                        bestVal = reduced[j];
                        entering = j;
                    }
                }

                // Bland's rule tie-breaking: choose smallest index among positive reduced costs
                if (entering == -1)
                {
                    var candidates = nonbasis.Where(j => reduced[j] > 0m).OrderBy(j => j).ToList();
                    if (candidates.Count > 0) entering = candidates[0];
                }

                if (entering == -1)
                {
                    // optimal
                    lines.Add("Optimal reached");
                    var solution = new decimal[n];
                    for (int i = 0; i < m; i++) solution[basis[i]] = xb[i];
                    lines.Add(string.Empty);
                    lines.Add("Solution:");
                    for (int j = 0; j < n; j++) lines.Add($"{model.VarNames[j]} = {RoundStr(solution[j])}");
                    decimal obj = 0m;
                    for (int j = 0; j < n; j++) obj += model.C[j] * solution[j];
                    if (!model.IsMax) obj = -obj;
                    lines.Add($"Objective = {RoundStr(obj)}");
                    break;
                }

                lines.Add(string.Empty);
                lines.Add("Pivot selection:");
                lines.Add($"  Entering variable: {model.VarNames[entering]} (index {entering})");

                // direction d = B^-1 * a_entering
                var a_enter = new decimal[m];
                for (int i = 0; i < m; i++) a_enter[i] = model.A[i, entering];
                var d = MatrixExtensions.Multiply(Binv, a_enter);
                lines.Add(string.Empty);
                lines.Add("Direction (d = B^-1 a_entering):");
                for (int i = 0; i < m; i++) lines.Add($"  {model.VarNames[basis[i]]} = {RoundStr(d[i])}");

                // Ratio test
                decimal minRatio = decimal.MaxValue;
                int leavingPos = -1;
                lines.Add(string.Empty);
                lines.Add("Ratio test:");
                for (int i = 0; i < m; i++)
                {
                    if (d[i] > 0m)
                    {
                        var ratio = xb[i] / d[i];
                        lines.Add($"  {model.VarNames[basis[i]]}: {RoundStr(xb[i])} / {RoundStr(d[i])} = {RoundStr(ratio)}");
                        if (ratio < minRatio - 1e-28m)
                        {
                            minRatio = ratio;
                            leavingPos = i;
                        }
                        else if (Math.Abs((double)(ratio - minRatio)) < 1e-28)
                        {
                            // tie: Bland's rule choose smallest index
                            if (basis[i] < basis[leavingPos]) leavingPos = i;
                        }
                    }
                }

                if (leavingPos == -1)
                {
                    lines.Add("Unbounded (no positive entries in direction)");
                    break;
                }

                lines.Add($"  Leaving variable: {model.VarNames[basis[leavingPos]]} at position {leavingPos}");

                // pivot: replace
                var leavingIndex = basis[leavingPos];
                basis[leavingPos] = entering;
                nonbasis.Remove(entering);
                nonbasis.Add(leavingIndex);
                nonbasis.Sort();
                // continue loop
            }

            return lines;
        }

        private string RoundStr(decimal v)
        {
            return Math.Round(v, RoundingDigits).ToString($"F{RoundingDigits}", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        }

        private string FormatBasis(int[] basis, string[] varNames)
        {
            return "Basis: " + string.Join(", ", basis.Select(i => varNames[i]));
        }

        private IEnumerable<string> FormatMatrix(decimal[,] mat, string[] varNames, int[] basis)
        {
            int rows = mat.GetLength(0);
            int cols = mat.GetLength(1);
            var headers = new string[cols + 1];
            headers[0] = string.Empty;
            for (int j = 0; j < cols; j++) headers[j + 1] = varNames[basis[j]];

            var table = new List<string[]> { headers };
            for (int i = 0; i < rows; i++)
            {
                var row = new string[cols + 1];
                row[0] = varNames[basis[i]];
                for (int j = 0; j < cols; j++) row[j + 1] = RoundStr(mat[i, j]);
                table.Add(row);
            }

            var widths = new int[cols + 1];
            for (int j = 0; j <= cols; j++)
            {
                widths[j] = table.Max(row => row[j].Length);
            }

            string border = "+" + string.Join("+", widths.Select(width => new string('-', width + 2))) + "+";
            var lines = new List<string>();
            lines.Add(border);
            lines.Add(FormatGridRow(table[0], widths, false));
            lines.Add(border);
            for (int i = 1; i < table.Count; i++)
            {
                lines.Add(FormatGridRow(table[i], widths, true));
            }
            lines.Add(border);
            return lines;
        }

        private static string FormatGridRow(string[] cells, int[] widths, bool rightAlignValues)
        {
            var formattedCells = cells.Select((cell, index) =>
                rightAlignValues && index > 0
                    ? cell.PadLeft(widths[index])
                    : cell.PadRight(widths[index]));
            return "| " + string.Join(" | ", formattedCells) + " |";
        }
    }
}
