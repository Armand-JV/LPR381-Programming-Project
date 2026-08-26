using System;
using System.IO;
using System.Linq;
using LPR381Project.Algorithms;
using LPR381Project.IO;
using LPR381Project.Models;

namespace LPR381Project
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== LPR381 Linear Programming Solver ===");
            Console.WriteLine();

            // Get input file path
            string? inputPath = null;
            if (args.Length > 0)
            {
                inputPath = args[0];
            }
            else
            {
                Console.Write("Enter input file path: ");
                inputPath = Console.ReadLine()?.Trim();
            }

            if (string.IsNullOrEmpty(inputPath))
            {
                Console.WriteLine("No input file provided. Exiting.");
                return;
            }

            // Special batch mode: run all files under tests/ when inputPath == "--runtests"
            if (inputPath == "--runtests")
            {
                Console.WriteLine("Running batch tests in 'tests/' folder...");
                var testDir = Path.Combine(Directory.GetCurrentDirectory(), "tests");
                if (!Directory.Exists(testDir))
                {
                    Console.WriteLine("No tests directory found: " + testDir);
                    return;
                }

                var files = Directory.GetFiles(testDir, "*.txt");
                foreach (var file in files)
                {
                    Console.WriteLine();
                    Console.WriteLine("============================");
                    Console.WriteLine("Test file: " + file);
                    Console.WriteLine("============================");
                    LPModel testModel;
                    try
                    {
                        testModel = InputParser.ParseFile(file);
                        Console.WriteLine();
                        Console.WriteLine("=== Parsed Model ===");
                        Console.WriteLine(testModel.ToString());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing input file: {ex.Message}");
                        continue;
                    }

                    var algorithms = AlgorithmRegistry.GetAll();
                    foreach (var alg in algorithms)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Running {alg.Name}...");
                        SolutionResult result;
                        try
                        {
                            result = alg.Solve(testModel.Clone());
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error running algorithm {alg.Name}: {ex.Message}");
                            continue;
                        }

                        DisplayResult(result);
                    }
                }

                return;
            }

            // Parse the model
            LPModel model;
            try
            {
                model = InputParser.ParseFile(inputPath);
                Console.WriteLine();
                Console.WriteLine("=== Parsed Model ===");
                Console.WriteLine(model.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing input file: {ex.Message}");
                return;
            }

            // Main menu loop
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("=== Select Algorithm ===");
                var algorithms = AlgorithmRegistry.GetAll();
                for (int i = 0; i < algorithms.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {algorithms[i].Name}");
                }
                Console.WriteLine($"  {algorithms.Count + 1}. Non-Linear optimisation (bonus)");
                Console.WriteLine($"  {algorithms.Count + 2}. Exit");
                Console.WriteLine();

                Console.Write("Enter choice (1-" + (algorithms.Count + 2) + "): ");
                string? choiceStr = Console.ReadLine()?.Trim();

                if (!int.TryParse(choiceStr, out int choice))
                {
                    Console.WriteLine("Invalid input. Please enter a number.");
                    continue;
                }

                if (choice == algorithms.Count + 2)
                {
                    Console.WriteLine("Exiting.");
                    break;
                }

                // The non-linear solver uses its own input and does not use the loaded model.
                // Returning from it keeps the current LP loaded.
                if (choice == algorithms.Count + 1)
                {
                    // Run returns false if input ends.
                    if (!NonLinear.NonLinearMenu.Run())
                    {
                        break;
                    }

                    // Ask whether the user wants to run another algorithm.
                    Console.WriteLine();
                    Console.Write("Run another algorithm on the same model? (y/n): ");
                    string? nonLinearContinue = Console.ReadLine()?.Trim().ToLower();
                    if (nonLinearContinue != "y" && nonLinearContinue != "yes")
                    {
                        break;
                    }

                    continue;
                }

                if (choice < 1 || choice > algorithms.Count)
                {
                    Console.WriteLine("Invalid choice. Please try again.");
                    continue;
                }

                // Run selected algorithm
                var selectedAlgorithm = algorithms[choice - 1];
                Console.WriteLine();
                Console.WriteLine($"Running {selectedAlgorithm.Name}...");
                Console.WriteLine();

                // Warn upfront if user chose a primal simplex and the model has binary variables
                bool isPrimal = selectedAlgorithm.Name.Contains("Primal Simplex", StringComparison.OrdinalIgnoreCase);
                bool hasBinary = false;
                if (model.SignRestrictions != null)
                {
                    for (int i = 0; i < model.SignRestrictions.Length; i++)
                    {
                        if (model.SignRestrictions[i] == SignRestriction.Binary)
                        {
                            hasBinary = true;
                            break;
                        }
                    }
                }

                if (isPrimal && hasBinary)
                {
                    Console.WriteLine("Warning: you selected a primal simplex algorithm but the model contains binary variables.");
                    Console.WriteLine("The primal simplex solvers will solve the LP relaxation and may not respect binary integrality.");
                    Console.Write("Proceed with this algorithm? (y/n): ");
                    string? proceed = Console.ReadLine()?.Trim().ToLower();
                    if (proceed != "y" && proceed != "yes")
                    {
                        Console.WriteLine("Skipping algorithm.");
                        continue;
                    }
                }

                SolutionResult result;
                try
                {
                    result = selectedAlgorithm.Solve(model.Clone());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error running algorithm: {ex.Message}");
                    continue;
                }

                // Display result
                DisplayResult(result);

                // Ask if user wants to save to file
                Console.WriteLine();
                Console.Write("Save result to file? (y/n): ");
                string? saveChoice = Console.ReadLine()?.Trim().ToLower();
                if (saveChoice == "y" || saveChoice == "yes")
                {
                    Console.Write("Enter output file path: ");
                    string? outputPath = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        try
                        {
                            OutputWriter.WriteResult(outputPath, model, result);
                            Console.WriteLine($"Result saved to {outputPath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error saving file: {ex.Message}");
                        }
                    }
                }

                // Ask if user wants to run another algorithm
                Console.WriteLine();
                Console.Write("Run another algorithm on the same model? (y/n): ");
                string? continueChoice = Console.ReadLine()?.Trim().ToLower();
                if (continueChoice != "y" && continueChoice != "yes")
                {
                    break;
                }
            }
        }

        static void DisplayResult(SolutionResult result)
        {
            Console.WriteLine($"=== Result ({result.AlgorithmName}) ===");
            Console.WriteLine($"Status: {result.Status}");

            if (result.Status == SolutionStatus.Optimal && result.VariableValues != null)
            {
                Console.WriteLine($"Objective value: {OutputWriter.Round(result.ObjectiveValue)}");
                Console.WriteLine("Variable values:");
                for (int i = 0; i < result.VariableValues.Length; i++)
                {
                    Console.WriteLine($"  x{i + 1} = {OutputWriter.Round(result.VariableValues[i])}");
                }
            }

            if (result.Iterations.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"=== Tableau Iterations ({result.Iterations.Count}) ===");
                foreach (var tableau in result.Iterations)
                {
                    Console.WriteLine();
                    DisplayTableau(tableau);
                }
            }

            if (result.Notes.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== Notes ===");
                foreach (var note in result.Notes)
                {
                    Console.WriteLine(note);
                }
            }
        }

        static void DisplayTableau(Tableau t)
        {
            Console.WriteLine(t.Label);

            // Column headers
            Console.Write("\t");
            foreach (var header in t.ColumnHeaders)
            {
                Console.Write($"{header}\t");
            }
            Console.WriteLine();

            // Rows
            for (int r = 0; r < t.RowCount; r++)
            {
                string rowLabel = r == 0 ? "z" : t.BasicVariables[r - 1];
                Console.Write($"{rowLabel}\t");
                for (int c = 0; c < t.ColumnCount; c++)
                {
                    Console.Write($"{OutputWriter.Round(t.Values[r, c])}\t");
                }
                Console.WriteLine();
            }
        }
    }
}
