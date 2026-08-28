using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Algorithms.PrimalSimplex
{
    public class LpModel
    {
        public bool IsMax { get; init; }
        public decimal[] C { get; init; }
        public decimal[,] A { get; init; }
        public decimal[] B { get; init; }
        public string[] VarNames { get; init; }

        // Parse the simple plain-text format used in tests\SantasPrimal Simplex.txt
        // First line: max|min <coeffs...>
        // Next lines: <coeffs...><<=|=><rhs>
        public static LpModel ParseFromFile(string path)
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0) throw new InvalidDataException("Empty input file.");

            // parse objective
            var first = lines[0].Trim();
            if (string.IsNullOrEmpty(first)) throw new InvalidDataException("First line is empty.");

            var parts = first.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) throw new InvalidDataException("Objective line must have 'max|min' and coefficients.");

            bool isMax = parts[0].Equals("max", StringComparison.OrdinalIgnoreCase);
            var cList = new List<decimal>();
            for (int i = 1; i < parts.Length; i++)
            {
                var token = parts[i].Trim();
                if (token == "+") continue;
                if (decimal.TryParse(token, NumberStyles.Number|NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var val))
                {
                    cList.Add(val);
                }
                else
                {
                    throw new InvalidDataException($"Could not parse objective coefficient '{token}'");
                }
            }

            var aRows = new List<decimal[]>();
            var bList = new List<decimal>();
            foreach (var raw in lines[1..])
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                // Find <= or =
                var idxLe = line.IndexOf("<=", StringComparison.Ordinal);
                var idxEq = line.IndexOf("=", StringComparison.Ordinal);
                int splitIdx = -1;
                string op = null;
                if (idxLe >= 0)
                {
                    splitIdx = idxLe;
                    op = "<=";
                }
                else if (idxEq >= 0)
                {
                    splitIdx = idxEq;
                    op = "=";
                }
                if (splitIdx < 0) throw new InvalidDataException($"Could not find constraint separator in line: {line}");

                var left = line.Substring(0, splitIdx).Trim();
                var right = line.Substring(splitIdx + op.Length).Trim();

                var tokens = left.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var coeffs = new List<decimal>();
                foreach (var t in tokens)
                {
                    if (t == "+") continue;
                    if (decimal.TryParse(t, NumberStyles.Number|NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v)) coeffs.Add(v);
                    else throw new InvalidDataException($"Could not parse coefficient '{t}' in line '{line}'");
                }

                if (!decimal.TryParse(right, NumberStyles.Number|NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var rhs))
                    throw new InvalidDataException($"Could not parse RHS '{right}' in line '{line}'");

                aRows.Add(coeffs.ToArray());
                bList.Add(rhs);
            }

            int m = aRows.Count;
            int n = cList.Count;
            // ensure constraint coefficients count matches objective length
            for (int i = 0; i < m; i++)
            {
                var row = aRows[i];
                if (row.Length != n) throw new InvalidDataException($"Constraint {i + 1} has {row.Length} coefficients but objective has {n}.");
            }

            // Build A matrix and add slack variables (one per constraint)
            var A = new decimal[m, n + m];
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) A[i, j] = aRows[i][j];
                // slack variable
                A[i, n + i] = 1m;
            }

            var c = new decimal[n + m];
            for (int j = 0; j < n; j++) c[j] = cList[j];
            for (int j = n; j < n + m; j++) c[j] = 0m; // slack cost 0

            var varNames = new string[n + m];
            for (int j = 0; j < n; j++) varNames[j] = $"x{j + 1}";
            for (int j = 0; j < m; j++) varNames[n + j] = $"s{j + 1}";

            return new LpModel
            {
                IsMax = isMax,
                C = c,
                A = A,
                B = bList.ToArray(),
                VarNames = varNames
            };
        }
    }
}
