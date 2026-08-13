namespace LPR381Project.Models
{
    public enum RelationType
    {
        LessOrEqual,
        GreaterOrEqual,
        Equal
    }

    public enum SignRestriction
    {
        Positive,
        Negative,
        Unrestricted,
        Integer,
        Binary
    }

    public enum ObjectiveType
    {
        Max,
        Min
    }

    public enum SolutionStatus
    {
        NotSolved,
        Optimal,
        Infeasible,
        Unbounded
    }
}
