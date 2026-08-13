using System;
using System.Linq;

namespace LPR381Project.Models
{
    public class Constraint
    {
        public double[] Coefficients { get; }
        public RelationType Relation { get; }
        public double Rhs { get; }

        public Constraint(double[] coefficients, RelationType relation, double rhs)
        {
            Coefficients = coefficients ?? throw new ArgumentNullException(nameof(coefficients));
            Relation = relation;
            Rhs = rhs;
        }

        public override string ToString()
        {
            string coeffs = string.Join(" ", Coefficients.Select(c => c.ToString("G", System.Globalization.CultureInfo.InvariantCulture)));
            string rel = Relation == RelationType.LessOrEqual ? "<=" : Relation == RelationType.GreaterOrEqual ? ">=" : "=";
            return $"{coeffs} {rel} {Rhs.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }
}
