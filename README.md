# LPR381-Programming-Project

This repository contains the LPR381 programming project — a Linear Programming solver with multiple algorithms and analysis tools.

## Quick Start

### Prerequisites
- .NET 10 SDK installed
- PowerShell or Command Prompt

### Running the Program

**Option 1: Run with input file as argument**
```powershell
cd "C:\path\to\LPR381-Programming-Project"
dotnet run -- input.txt
```

**Option 2: Run without arguments (prompts for file path)**
```powershell
dotnet run
```

### Building the Project
```powershell
dotnet build
```

### Running the Compiled DLL Directly
```powershell
dotnet .\bin\Debug\net10.0\LPR381Project.dll input.txt
```

## Input File Format

The input file format follows the project brief specification:

```
max +2 +3 +3 +5 +2 +4
+11 +8 +6 +14 +10 +10 <=40
bin bin bin bin bin bin
```

**Format details:**
- **Line 1:** `max` or `min` followed by objective function coefficients (one per decision variable)
- **Lines 2 to n-1:** Constraint rows with coefficients, relation operator (`<=`, `>=`, `=`), and right-hand side value
- **Last line:** Sign restrictions, one token per variable:
  - `+` : non-negative (≥ 0)
  - `-` : non-positive (≤ 0)
  - `urs` : unrestricted (can be any real number)
  - `int` : integer variable
  - `bin` : binary variable (0 or 1)

### Example Input Files

**Linear Programming (maximization with ≤ constraints):**
```
max +3 +2
+2 +1 <=100
+1 +1 <=80
+ +
```

**Minimization with ≥ constraints:**
```
min +2 +3
+1 +1 >=4
+2 +1 >=5
+ +
```

**Integer Programming:**
```
max +5 +4
+3 +2 <=10
+1 +1 <=4
int int
```

## Using the CLI

1. **Start the program** with an input file:
   ```powershell
   dotnet run -- test_input.txt
   ```

2. **Parsed model** will be displayed showing objective, constraints, and sign restrictions.

3. **Select an algorithm** from the menu:
   ```
   === Select Algorithm ===
     1. Primal Simplex
     2. Revised Primal Simplex
     3. Branch & Bound Simplex
     4. Cutting Plane
     5. Branch & Bound Knapsack
     6. Exit

   Enter choice (1-6):
   ```

4. **View results** including:
   - Solution status (Optimal, Infeasible, Unbounded)
   - Objective value
   - Variable values
   - All tableau iterations

5. **Save results to file:**
   ```
   Save result to file? (y/n): y
   Enter output file path: output.txt
   ```

   **Important:** Enter a file path (e.g., `output.txt` or `C:\path\to\output.txt`), NOT a directory path.

6. **Run another algorithm** or exit:
   ```
   Run another algorithm on the same model? (y/n):
   ```

## Saving Output to a File

When prompted to save, provide a **file path** not a directory:

| Correct ✅ | Incorrect ❌ |
|-----------|-------------|
| `output.txt` | `C:\Users\name\Documents` |
| `results.txt` | `.\output` |
| `C:\path\to\output.txt` | `output` (directory name) |

The output file contains:
- Canonical form of the model
- All tableau iterations
- Final solution status and variable values
- Algorithm notes

## Project Structure

```
LPR381-Programming-Project/
├── Algorithms/
│   ├── IAlgorithm.cs              # Interface for all algorithms
│   ├── AlgorithmRegistry.cs       # Lists available algorithms
│   ├── PrimalSimplex/
│   │   ├── PrimalSimplexSolver.cs      # Full tableau simplex
│   │   └── RevisedPrimalSimplexSolver.cs # Product form inverse
│   ├── BranchAndBound/
│   │   └── BranchAndBoundSimplexSolver.cs
│   ├── IntegerAlgorithms/
│   │   ├── CuttingPlaneSolver.cs
│   │   └── BranchAndBoundKnapsackSolver.cs
│   └── Sensitivity/
│       ├── SensitivityAnalyzer.cs
│       ├── DualityAnalyzer.cs
│       └── ISensitivityAnalyzer.cs
├── Models/
│   ├── LPModel.cs                 # Linear programming model
│   ├── Constraint.cs              # Constraint definition
│   ├── Tableau.cs                 # Simplex tableau
│   ├── SolutionResult.cs          # Algorithm output
│   └── Enums.cs                   # Enumerations
├── IO/
│   ├── InputParser.cs             # Parses input files
│   └── OutputWriter.cs            # Writes results to file
├── Program.cs                     # Main CLI entry point
├── LPR381Project.csproj           # Project file
└── README.md                      # This file
```

## Available Algorithms

| # | Algorithm | Description |
|---|-----------|-------------|
| 1 | Primal Simplex | Full tableau method with all iterations displayed |
| 2 | Revised Primal Simplex | Efficient method using basis inverse (product form) |
| 3 | Branch & Bound Simplex | For integer programming problems |
| 4 | Cutting Plane | Gomory cuts for integer programming |
| 5 | Branch & Bound Knapsack | Specialized for knapsack problems |

## Troubleshooting

### Build Error: "The project file could not be loaded"
- Ensure the `.csproj` file is valid XML
- Check for extra characters after `</Project>` tag

### Build Error: "File is being used by another process"
- Close any running instances of the program
- Then rebuild with `dotnet build`

### Runtime Error: "Input file not found"
- Verify the file path is correct
- Use relative path from project directory or full absolute path

### Runtime Error: "Access to the path is denied"
- When saving output, enter a **file path** (e.g., `output.txt`), not a directory path

### Parse Error: "Expected 'max' or 'min' on line 1"
- Ensure first line starts with `max` or `min` (lowercase)
- Check objective coefficients are separated by spaces with explicit signs (e.g., `+2 +3`)

### Parse Error: "Constraint line is missing a relation"
- Each constraint must include `<=`, `>=`, or `=`
- Spaces around the operator are optional (`<=40` and `<= 40` both work)

## Development

### Visual Studio
1. Open `LPR381Project.csproj` or add to a solution
2. Right-click project → Set as Startup Project
3. To pass arguments: Project Properties → Debug → Application arguments
4. Run with F5 (debug) or Ctrl+F5 (no debug)

### Adding New Algorithms
1. Create a new class implementing `IAlgorithm`
2. Add the class to `AlgorithmRegistry.GetAll()`
3. The algorithm will automatically appear in the CLI menu

## License

This project is for educational purposes as part of LPR381 coursework.
