using System;
using System.IO;
using Algorithms.PrimalSimplex;

namespace Programs
{
    internal static class RevisedPrimalSimplexCli
    {
        // Simple CLI: RevisedPrimalSimplexCli <input-file> [roundingDigits]
        public static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: RevisedPrimalSimplexCli <input-file> [roundingDigits]");
                return 1;
            }

            var path = args[0];
            if (!File.Exists(path))
            {
                Console.WriteLine($"Input file not found: {path}");
                return 1;
            }

            int rounding = 6;
            if (args.Length > 1 && int.TryParse(args[1], out var r)) rounding = r;

            try
            {
                var model = LpModel.ParseFromFile(path);
                var solver = new RevisedPrimalSimplexSolver { RoundingDigits = rounding };
                solver.RunAndPrint(model);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}
