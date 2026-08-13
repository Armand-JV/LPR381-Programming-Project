using System.Collections.Generic;

namespace LPR381Project.Models
{
    /// <summary>
    /// Everything an algorithm produces from one Solve() call: every tableau
    /// iteration (for on-screen display and file export), the final variable
    /// values, the objective value and the outcome status. Sensitivity
    /// Analysis and Duality consume this object once a model has been
    /// solved to optimality.
    /// </summary>
    public class SolutionResult
    {
        public string? AlgorithmName { get; set; }
        public SolutionStatus Status { get; set; }
        public List<Tableau> Iterations { get; set; }
        public double[]? VariableValues { get; set; }
        public double ObjectiveValue { get; set; }

        /// <summary>Free-form notes for anything that doesn't fit neatly into a tableau,
        /// e.g. which sub-problems were fathomed and why, or the best candidate found
        /// during Branch &amp; Bound.</summary>
        public List<string> Notes { get; set; }

        public SolutionResult()
        {
            Status = SolutionStatus.NotSolved;
            Iterations = new List<Tableau>();
            Notes = new List<string>();
        }
    }
}
