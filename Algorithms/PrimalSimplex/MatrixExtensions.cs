using System;
using System.Collections.Generic;
using System.Linq;

namespace Algorithms.PrimalSimplex
{
    internal static class MatrixExtensions
    {
        public static decimal[] Multiply(decimal[,] mat, decimal[] vec)
        {
            int m = mat.GetLength(0);
            int n = mat.GetLength(1);
            if (vec.Length != n) throw new ArgumentException("Vector length mismatch");
            var res = new decimal[m];
            for (int i = 0; i < m; i++)
            {
                decimal sum = 0m;
                for (int j = 0; j < n; j++) sum += mat[i, j] * vec[j];
                res[i] = sum;
            }
            return res;
        }

        public static decimal[,] SubMatrix(decimal[,] mat, int rows, int cols)
        {
            var res = new decimal[rows, cols];
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) res[i, j] = mat[i, j];
            return res;
        }

        // Inverse using Gaussian elimination (suitable for small matrices)
        public static decimal[,] Inverse(decimal[,] input)
        {
            int n = input.GetLength(0);
            if (n != input.GetLength(1)) throw new ArgumentException("Matrix must be square");

            // Create augmented matrix
            var aug = new decimal[n, 2 * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) aug[i, j] = input[i, j];
                for (int j = 0; j < n; j++) aug[i, n + j] = (i == j) ? 1m : 0m;
            }

            // Forward elimination
            for (int col = 0; col < n; col++)
            {
                // find pivot
                int pivot = col;
                for (int r = col; r < n; r++) if (Math.Abs(aug[r, col]) > Math.Abs(aug[pivot, col])) pivot = r;
                if (aug[pivot, col] == 0m) throw new InvalidOperationException("Matrix is singular and cannot be inverted.");

                if (pivot != col)
                {
                    for (int c = 0; c < 2 * n; c++)
                    {
                        var tmp = aug[col, c];
                        aug[col, c] = aug[pivot, c];
                        aug[pivot, c] = tmp;
                    }
                }

                // normalize pivot row
                var pivotVal = aug[col, col];
                for (int c = 0; c < 2 * n; c++) aug[col, c] /= pivotVal;

                // eliminate others
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    var factor = aug[r, col];
                    if (factor == 0m) continue;
                    for (int c = 0; c < 2 * n; c++) aug[r, c] -= factor * aug[col, c];
                }
            }

            var inv = new decimal[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = aug[i, n + j];
            return inv;
        }

        public static decimal[] MultiplyRowVector(decimal[] row, decimal[,] mat)
        {
            int n = row.Length;
            if (mat.GetLength(0) != n) throw new ArgumentException("Dimensions mismatch");
            int m = mat.GetLength(1);
            var res = new decimal[m];
            for (int j = 0; j < m; j++)
            {
                decimal sum = 0m;
                for (int i = 0; i < n; i++) sum += row[i] * mat[i, j];
                res[j] = sum;
            }
            return res;
        }

        public static decimal Dot(decimal[] a, decimal[] b)
        {
            if (a.Length != b.Length) throw new ArgumentException("Vector lengths differ");
            decimal s = 0m;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        public static decimal[,] GetColumns(decimal[,] A, int[] cols)
        {
            int m = A.GetLength(0);
            int n = cols.Length;
            var res = new decimal[m, n];
            for (int j = 0; j < n; j++)
            {
                int col = cols[j];
                for (int i = 0; i < m; i++) res[i, j] = A[i, col];
            }
            return res;
        }
    }
}
