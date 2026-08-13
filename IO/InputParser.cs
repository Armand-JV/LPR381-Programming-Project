using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LPR381Project.Models;

namespace LPR381Project.IO
{
    /// <summary>
    /// Reads the model input file format described in the project brief:
    ///
    ///   Line 1:        max|min  {signed coefficient} {signed coefficient} ...
    ///   Line 2..n-1:   {signed coefficient} ...  {relation}{rhs}
    ///   Last line:     {sign restriction} {sign restriction} ...   (+, -, urs, int, bin)
    ///
    /// Example:
    ///   max +2 +3 +3 +5 +2 +4
    ///   +11 +8 +6 +14 +10 +10 &lt;=40
    ///   bin bin bin bin bin bin
    ///
    /// Owner: Person 1 (Core App + Simplex). This class is the single
    /// source of truth for reading a model - other modules should always
    /// go through LPModel, never re-parse the file themselves.
    /// </summary>
    public static class InputParser
    {
        // Matches the relation operator anywhere in a constraint line, so the
        // line can be split into "coefficients" / "relation" / "rhs" whether
        // or not there is a space before the right-hand-side value
        // (both "<= 40" and "<=40" are accepted).
        private static readonly Regex RelationRegex = new Regex(@"(<=|>=|=)", RegexOptions.Compiled);

        public static LPModel ParseFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(string.Format("Input file not found: {0}", path));
            }

            var lines = File.ReadAllLines(path)
                             .Select(l => l.Trim())
                             .Where(l => l.Length > 0)
                             .ToList();

            if (lines.Count < 3)
            {
                throw new FormatException(
                    "Input file must contain an objective line, at least one constraint line, " +
                    "and a sign-restriction line.");
            }

            var model = new LPModel();

            // ---- Line 1: objective ----
            var objTokens = lines[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string objectiveWord = objTokens[0].Trim().ToLower();
            if (objectiveWord == "max")
            {
                model.Objective = ObjectiveType.Max;
            }
            else if (objectiveWord == "min")
            {
                model.Objective = ObjectiveType.Min;
            }
            else
            {
                throw new FormatException(string.Format("Expected 'max' or 'min' on line 1, found '{0}'.", objTokens[0]));
            }

            model.ObjectiveCoefficients = objTokens.Skip(1).Select(ParseSignedNumber).ToArray();
            int numVars = model.ObjectiveCoefficients.Length;
            if (numVars == 0)
            {
                throw new FormatException("Objective function has no decision variable coefficients.");
            }

            // ---- Middle lines: constraints (any number of them) ----
            for (int i = 1; i < lines.Count - 1; i++)
            {
                model.Constraints.Add(ParseConstraintLine(lines[i], numVars));
            }

            // ---- Last line: sign restrictions, one per decision variable ----
            var signTokens = lines[lines.Count - 1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (signTokens.Length != numVars)
            {
                throw new FormatException(string.Format(
                    "Expected {0} sign restrictions (one per decision variable), found {1}.",
                    numVars, signTokens.Length));
            }
            model.SignRestrictions = signTokens.Select(ParseSignRestriction).ToArray();

            return model;
        }

        private static Constraint ParseConstraintLine(string line, int numVars)
        {
            Match match = RelationRegex.Match(line);
            if (!match.Success)
            {
                throw new FormatException(string.Format("Constraint line is missing a relation (<=, >=, =): '{0}'", line));
            }

            string lhs = line.Substring(0, match.Index).Trim();
            string relationSymbol = match.Value;
            string rhsText = line.Substring(match.Index + match.Length).Trim();

            var coeffTokens = lhs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (coeffTokens.Length != numVars)
            {
                throw new FormatException(string.Format(
                    "Expected {0} technological coefficients, found {1} in line: '{2}'",
                    numVars, coeffTokens.Length, line));
            }

            double[] coefficients = coeffTokens.Select(ParseSignedNumber).ToArray();

            RelationType relation;
            if (relationSymbol == "<=") relation = RelationType.LessOrEqual;
            else if (relationSymbol == ">=") relation = RelationType.GreaterOrEqual;
            else relation = RelationType.Equal;

            double rhs = double.Parse(rhsText, CultureInfo.InvariantCulture);

            return new Constraint(coefficients, relation, rhs);
        }

        private static double ParseSignedNumber(string token)
        {
            return double.Parse(token, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        private static SignRestriction ParseSignRestriction(string token)
        {
            switch (token.Trim().ToLower())
            {
                case "+": return SignRestriction.Positive;
                case "-": return SignRestriction.Negative;
                case "urs": return SignRestriction.Unrestricted;
                case "int": return SignRestriction.Integer;
                case "bin": return SignRestriction.Binary;
                default:
                    throw new FormatException(string.Format("Unknown sign restriction '{0}'.", token));
            }
        }
    }
}
