using LPR381Project.Models;

namespace LPR381Project.Algorithms
{
    /// <summary>
    /// Contract every solving algorithm must implement so the menu can list,
    /// select and run any of them the same way, regardless of who wrote it.
    /// Implement this interface for whichever algorithm(s) you own - do not
    /// change the signature, since MenuController and OutputWriter both
    /// depend on it.
    /// </summary>
    public interface IAlgorithm
    {
        /// <summary>Name shown in the menu and written to the output file, e.g. "Primal Simplex".</summary>
        string Name { get; }

        /// <summary>
        /// Solve the given model and return the full result, including the
        /// canonical-form tableau and every subsequent iteration. Must not
        /// mutate <paramref name="model"/> - call model.Clone() first if you
        /// need a working copy (e.g. for Branch &amp; Bound / Cutting Plane).
        /// </summary>
        SolutionResult Solve(LPModel model);
    }
}
