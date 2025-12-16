using System;
using System.Threading.Tasks;
using HedgingEngine.Core;

namespace HedgingEngine
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Erreur: 3 arguments requis");
                Console.WriteLine("Usage: HedgingEngine <financial-params.json> <market-data.csv> <output.json>");
                Console.ResetColor();
                return 1;
            }

            try
            {
                var backtest = new BacktestEngine();
                await backtest.RunAsync(args[0], args[1], args[2]);
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ ERREUR FATALE: {ex.Message}");
                Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
                Console.ResetColor();
                return 1;
            }
        }
    }
}