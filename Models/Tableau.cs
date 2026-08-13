using System.Text;

namespace LPR381Project.Models
{
    /// <summary>
    /// A single simplex tableau "snapshot". Shared by every simplex-based
    /// algorithm (Primal Simplex, Revised Primal Simplex, Branch &amp; Bound,
    /// Cutting Plane) so that the menu / output writer only needs to know
    /// about one type in order to display or export any of them.
    /// </summary>
    public class Tableau
    {
        /// <summary>Row-major matrix. Row 0 is the objective (z) row; the remaining
        /// rows are one per constraint. The last column is the RHS column.</summary>
        public double[,]? Values { get; set; }

        /// <summary>Column headers in the same column order as Values,
        /// e.g. x1, x2, s1, s2, RHS.</summary>
        public string[]? ColumnHeaders { get; set; }

        /// <summary>Name of the basic variable for each constraint row (length = RowCount - 1).</summary>
        public string[]? BasicVariables { get; set; }

        /// <summary>Label shown above the tableau, e.g. "Iteration 0 (Canonical Form)"
        /// or "Sub-problem 2, Iteration 1".</summary>
        public string? Label { get; set; }

        public int RowCount
        {
            get { return Values == null ? 0 : Values.GetLength(0); }
        }

        public int ColumnCount
        {
            get { return Values == null ? 0 : Values.GetLength(1); }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Label);
            sb.AppendLine("\t" + string.Join("\t", ColumnHeaders));
            for (int r = 0; r < RowCount; r++)
            {
                string rowLabel = r == 0 ? "z" : BasicVariables[r - 1];
                sb.Append(rowLabel + "\t");
                for (int c = 0; c < ColumnCount; c++)
                {
                    sb.Append(Values[r, c].ToString("0.000") + "\t");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
