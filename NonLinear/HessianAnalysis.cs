using System;
using System.Collections.Generic;

namespace LPR381Project.NonLinear
{
    // Numerical derivatives.

    /// <summary>Calculates numerical derivatives.</summary>
    public static class NumericCalculus
    {
        /// <summary>Step size for first derivatives.</summary>
        private const double GradientStep = 1e-6;

        /// <summary>Step size for second derivatives.</summary>
        private const double HessianStep = 1e-4;

        /// <summary>Calculates the gradient using central differences.</summary>
        public static double[] Gradient(ObjectiveFunction f, double[] x)
        {
            int n = x.Length;
            var g = new double[n];

            for (int i = 0; i < n; i++)
            {
                double h = StepFor(x[i], GradientStep);

                double[] forward = (double[])x.Clone();
                double[] backward = (double[])x.Clone();
                forward[i] += h;
                backward[i] -= h;

                g[i] = (f.Evaluate(forward) - f.Evaluate(backward)) / (2.0 * h);
            }

            return g;
        }

        /// <summary>Calculates the Hessian using central differences.</summary>
        public static double[,] Hessian(ObjectiveFunction f, double[] x)
        {
            int n = x.Length;
            var h = new double[n];
            for (int i = 0; i < n; i++)
            {
                h[i] = StepFor(x[i], HessianStep);
            }

            double centre = f.Evaluate(x);
            var hessian = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                double[] plus = (double[])x.Clone();
                double[] minus = (double[])x.Clone();
                plus[i] += h[i];
                minus[i] -= h[i];

                hessian[i, i] = (f.Evaluate(plus) - 2.0 * centre + f.Evaluate(minus)) / (h[i] * h[i]);

                for (int j = i + 1; j < n; j++)
                {
                    double pp = Shifted(f, x, i, h[i], j, h[j]);
                    double pm = Shifted(f, x, i, h[i], j, -h[j]);
                    double mp = Shifted(f, x, i, -h[i], j, h[j]);
                    double mm = Shifted(f, x, i, -h[i], j, -h[j]);

                    double mixed = (pp - pm - mp + mm) / (4.0 * h[i] * h[j]);
                    hessian[i, j] = mixed;
                    hessian[j, i] = mixed;
                }
            }

            return hessian;
        }

        /// <summary>Evaluates a point shifted in two dimensions.</summary>
        private static double Shifted(ObjectiveFunction f, double[] x, int i, double di, int j, double dj)
        {
            double[] point = (double[])x.Clone();
            point[i] += di;
            point[j] += dj;
            return f.Evaluate(point);
        }

        /// <summary>Scales the step size to the variable value.</summary>
        private static double StepFor(double value, double baseStep)
        {
            return baseStep * Math.Max(1.0, Math.Abs(value));
        }

        /// <summary>Calculates the vector norm.</summary>
        public static double Norm(double[] v)
        {
            double sum = 0.0;
            foreach (double value in v)
            {
                sum += value * value;
            }

            return Math.Sqrt(sum);
        }
    }

    // Convexity test (LO15).

    /// <summary>Possible curvature classifications.</summary>
    public enum Curvature
    {
        /// <summary>Strictly convex.</summary>
        StrictlyConvex,

        /// <summary>Convex.</summary>
        Convex,

        /// <summary>Strictly concave.</summary>
        StrictlyConcave,

        /// <summary>Concave.</summary>
        Concave,

        /// <summary>Indefinite or saddle.</summary>
        Indefinite
    }

    /// <summary>Stores the Hessian analysis result.</summary>
    public class HessianAnalysis
    {
        /// <summary>Point used for the Hessian.</summary>
        public double[] Point { get; set; } = Array.Empty<double>();

        /// <summary>Hessian matrix.</summary>
        public double[,] Matrix { get; set; } = new double[0, 0];

        /// <summary>Hessian eigenvalues.</summary>
        public double[] Eigenvalues { get; set; } = Array.Empty<double>();

        /// <summary>Principal minors grouped by order.</summary>
        public double[][] PrincipalMinorsByOrder { get; set; } = Array.Empty<double[]>();

        /// <summary>Hessian determinant.</summary>
        public double Determinant { get; set; }

        /// <summary>Curvature result.</summary>
        public Curvature Curvature { get; set; }

        /// <summary>Whether the Hessian is constant.</summary>
        public bool IsConstantHessian { get; set; }

        /// <summary>Formats the curvature result.</summary>
        public string CurvatureText()
        {
            switch (Curvature)
            {
                case Curvature.StrictlyConvex: return "CONVEX (Hessian positive definite)";
                case Curvature.Convex: return "CONVEX (Hessian positive semi-definite)";
                case Curvature.StrictlyConcave: return "CONCAVE (Hessian negative definite)";
                case Curvature.Concave: return "CONCAVE (Hessian negative semi-definite)";
                default: return "NEITHER - indefinite Hessian, this is a saddle point region";
            }
        }

        /// <summary>Whether the function is convex.</summary>
        public bool IsConvex => Curvature == Curvature.Convex || Curvature == Curvature.StrictlyConvex;

        /// <summary>Whether the function is concave.</summary>
        public bool IsConcave => Curvature == Curvature.Concave || Curvature == Curvature.StrictlyConcave;

        /// <summary>Second diagonal Hessian value.</summary>
        public double SecondDiagonal =>
            Matrix.GetLength(0) >= 2 ? Matrix[1, 1] : 0.0;

        /// <summary>Whether the determinant test is inconclusive.</summary>
        public bool DeterminantRuleInconclusive
        {
            get
            {
                double scale = 1.0;
                foreach (double v in Matrix)
                {
                    scale = Math.Max(scale, Math.Abs(v));
                }

                int n = Matrix.GetLength(0);
                return Math.Abs(Determinant) <= 1e-6 * Math.Pow(scale, Math.Max(n, 1));
            }
        }

        /// <summary>Explains what the curvature means for the requested objective.</summary>
        public string Implication(bool maximise)
        {
            string scope = IsConstantHessian
                ? "everywhere (the Hessian is constant, so f is quadratic)"
                : "in the region tested";

            if (Curvature == Curvature.Indefinite)
            {
                return "Indefinite " + scope + ", so a stationary point here is a saddle, not an "
                     + "optimum. Steepest ascent/descent will still climb, but it can only promise "
                     + "a local answer.";
            }

            if (maximise)
            {
                return IsConcave
                    ? "Concave " + scope + ", so any stationary point is the GLOBAL maximum."
                    : "Convex " + scope + ", so f curves upward and has no interior maximum - it is "
                    + "minimisation that is well posed for this function. Ascent will run away to "
                    + "the boundary.";
            }

            return IsConvex
                ? "Convex " + scope + ", so any stationary point is the GLOBAL minimum."
                : "Concave " + scope + ", so f curves downward and has no interior minimum - it is "
                + "maximisation that is well posed for this function. Descent will run away to "
                + "the boundary.";
        }
    }

    /// <summary>Builds and classifies the Hessian.</summary>
    public static class HessianAnalyzer
    {
        /// <summary>Analyses the Hessian at a point.</summary>
        public static HessianAnalysis Analyse(ObjectiveFunction f, double[] point)
        {
            double[,] h = NumericCalculus.Hessian(f, point);
            int n = point.Length;

            var analysis = new HessianAnalysis
            {
                Point = (double[])point.Clone(),
                Matrix = h,
                Eigenvalues = SymmetricEigenvalues(h),
                PrincipalMinorsByOrder = PrincipalMinorsByOrder(h),
                Determinant = Determinant(h),
                IsConstantHessian = IsHessianConstant(f, point, h)
            };

            analysis.Curvature = Classify(analysis.Eigenvalues);
            return analysis;
        }

        /// <summary>Classifies curvature from the eigenvalues.</summary>
        private static Curvature Classify(double[] eigenvalues)
        {
            double scale = 1.0;
            foreach (double e in eigenvalues)
            {
                scale = Math.Max(scale, Math.Abs(e));
            }

            // Ignore eigenvalues within the numerical noise tolerance.
            double tolerance = 1e-6 * scale;

            bool anyPositive = false;
            bool anyNegative = false;
            bool anyZero = false;

            foreach (double e in eigenvalues)
            {
                if (e > tolerance)
                {
                    anyPositive = true;
                }
                else if (e < -tolerance)
                {
                    anyNegative = true;
                }
                else
                {
                    anyZero = true;
                }
            }

            if (anyPositive && anyNegative)
            {
                return Curvature.Indefinite;
            }

            if (anyPositive)
            {
                return anyZero ? Curvature.Convex : Curvature.StrictlyConvex;
            }

            if (anyNegative)
            {
                return anyZero ? Curvature.Concave : Curvature.StrictlyConcave;
            }

            // All-zero eigenvalues are treated as convex.
            return Curvature.Convex;
        }

        /// <summary>Checks whether the Hessian is constant.</summary>
        private static bool IsHessianConstant(ObjectiveFunction f, double[] point, double[,] reference)
        {
            int n = point.Length;
            double scale = MaxAbs(reference);
            double tolerance = 1e-4 * Math.Max(1.0, scale);

            double[] offsets = { 1.7, -2.3 };

            foreach (double offset in offsets)
            {
                var probe = new double[n];
                for (int i = 0; i < n; i++)
                {
                    // Use a different shift for each variable.
                    probe[i] = point[i] + offset * (i + 1);
                }

                double[,] other;
                try
                {
                    other = NumericCalculus.Hessian(f, probe);
                }
                catch (Exception)
                {
                    return false;
                }

                for (int r = 0; r < n; r++)
                {
                    for (int c = 0; c < n; c++)
                    {
                        if (!IsFinite(other[r, c]) ||
                            Math.Abs(other[r, c] - reference[r, c]) > tolerance)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>Calculates all principal minors.</summary>
        public static double[][] PrincipalMinorsByOrder(double[,] h)
        {
            int n = h.GetLength(0);
            var byOrder = new double[n][];

            for (int k = 1; k <= n; k++)
            {
                var minors = new List<double>();
                foreach (int[] subset in Subsets(n, k))
                {
                    var sub = new double[k, k];
                    for (int r = 0; r < k; r++)
                    {
                        for (int c = 0; c < k; c++)
                        {
                            sub[r, c] = h[subset[r], subset[c]];
                        }
                    }

                    minors.Add(Determinant(sub));
                }

                byOrder[k - 1] = minors.ToArray();
            }

            return byOrder;
        }

        /// <summary>Generates all k-sized index combinations.</summary>
        private static IEnumerable<int[]> Subsets(int n, int k)
        {
            var index = new int[k];
            for (int i = 0; i < k; i++)
            {
                index[i] = i;
            }

            while (true)
            {
                yield return (int[])index.Clone();

                // Move to the next combination.
                int pos = k - 1;
                while (pos >= 0 && index[pos] == n - k + pos)
                {
                    pos--;
                }

                if (pos < 0)
                {
                    yield break;
                }

                index[pos]++;
                for (int i = pos + 1; i < k; i++)
                {
                    index[i] = index[i - 1] + 1;
                }
            }
        }

        /// <summary>Calculates the determinant using Gaussian elimination.</summary>
        public static double Determinant(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            var a = (double[,])matrix.Clone();
            double det = 1.0;

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int row = col + 1; row < n; row++)
                {
                    if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                    {
                        pivot = row;
                    }
                }

                if (Math.Abs(a[pivot, col]) < 1e-14)
                {
                    return 0.0;
                }

                if (pivot != col)
                {
                    for (int c = 0; c < n; c++)
                    {
                        (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]);
                    }

                    // A row swap changes the determinant sign.
                    det = -det;
                }

                det *= a[col, col];

                for (int row = col + 1; row < n; row++)
                {
                    double factor = a[row, col] / a[col, col];
                    for (int c = col; c < n; c++)
                    {
                        a[row, c] -= factor * a[col, c];
                    }
                }
            }

            return det;
        }

        /// <summary>Calculates symmetric-matrix eigenvalues using Jacobi rotations.</summary>
        public static double[] SymmetricEigenvalues(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            var a = (double[,])matrix.Clone();

            if (n == 1)
            {
                return new[] { a[0, 0] };
            }

            const int maxSweeps = 100;

            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                // Find the largest off-diagonal value.
                int p = 0, q = 1;
                double largest = 0.0;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (Math.Abs(a[i, j]) > largest)
                        {
                            largest = Math.Abs(a[i, j]);
                            p = i;
                            q = j;
                        }
                    }
                }

                double offDiagonalFloor = 1e-12 * Math.Max(1.0, MaxAbs(a));
                if (largest <= offDiagonalFloor)
                {
                    break;
                }

                // Calculate the Jacobi rotation.
                double theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                if (theta == 0.0)
                {
                    t = 1.0;
                }

                double c = 1.0 / Math.Sqrt(t * t + 1.0);
                double s = t * c;

                for (int k = 0; k < n; k++)
                {
                    if (k == p || k == q)
                    {
                        continue;
                    }

                    double akp = a[k, p];
                    double akq = a[k, q];
                    a[k, p] = c * akp - s * akq;
                    a[p, k] = a[k, p];
                    a[k, q] = s * akp + c * akq;
                    a[q, k] = a[k, q];
                }

                double app = a[p, p];
                double aqq = a[q, q];
                double apq = a[p, q];

                a[p, p] = c * c * app - 2.0 * s * c * apq + s * s * aqq;
                a[q, q] = s * s * app + 2.0 * s * c * apq + c * c * aqq;
                a[p, q] = 0.0;
                a[q, p] = 0.0;
            }

            var eigenvalues = new double[n];
            for (int i = 0; i < n; i++)
            {
                eigenvalues[i] = a[i, i];
            }

            Array.Sort(eigenvalues);
            return eigenvalues;
        }

        /// <summary>Finds the largest absolute matrix value.</summary>
        private static double MaxAbs(double[,] m)
        {
            double max = 0.0;
            foreach (double v in m)
            {
                if (IsFinite(v))
                {
                    max = Math.Max(max, Math.Abs(v));
                }
            }

            return max;
        }

        /// <summary>Checks that a value is finite.</summary>
        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
