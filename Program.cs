using System;
using System.IO;
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
                Console.WriteLine($"  {algorithms.Count + 1}. Exit");
                Console.WriteLine();

                Console.Write("Enter choice (1-" + (algorithms.Count + 1) + "): ");
                string? choiceStr = Console.ReadLine()?.Trim();

                if (!int.TryParse(choiceStr, out int choice))
                {
                    Console.WriteLine("Invalid input. Please enter a number.");
                    continue;
                }

                if (choice == algorithms.Count + 1)
                {
                    Console.WriteLine("Exiting.");
                    break;
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
