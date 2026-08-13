using System.Collections.Generic;
using System.Text;

namespace LPR381Project.Models
{
    public class LPModel
    {
        public ObjectiveType Objective { get; set; }
        public double[] ObjectiveCoefficients { get; set; } = new double[0];
        public List<Constraint> Constraints { get; set; } = new List<Constraint>();
        public SignRestriction[] SignRestrictions { get; set; } = new SignRestriction[0];

        public LPModel()
        {
        }

        public LPModel Clone()
        {
            var clone = new LPModel();
            clone.Objective = this.Objective;
            clone.ObjectiveCoefficients = (double[])this.ObjectiveCoefficients.Clone();
            clone.SignRestrictions = (SignRestriction[])this.SignRestrictions.Clone();
            foreach (var c in this.Constraints)
            {
                clone.Constraints.Add(new Constraint((double[])c.Coefficients.Clone(), c.Relation, c.Rhs));
            }
            return clone;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Objective == ObjectiveType.Max ? "max" : "min");
            sb.Append(" ");
            for (int i = 0; i < ObjectiveCoefficients.Length; i++)
            {
                sb.Append(ObjectiveCoefficients[i].ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                if (i < ObjectiveCoefficients.Length - 1) sb.Append(" ");
            }
            sb.AppendLine();
            foreach (var c in Constraints)
            {
                sb.AppendLine(c.ToString());
            }
            if (SignRestrictions != null && SignRestrictions.Length > 0)
            {
                for (int i = 0; i < SignRestrictions.Length; i++)
                {
                    string s = SignRestrictions[i] switch
                    {
                        SignRestriction.Positive => "+",
                        SignRestriction.Negative => "-",
                        SignRestriction.Unrestricted => "urs",
                        SignRestriction.Integer => "int",
                        SignRestriction.Binary => "bin",
                        _ => "?",
                    };
                    sb.Append(s);
                    if (i < SignRestrictions.Length - 1) sb.Append(" ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
