using HedgingEngine.Core;

namespace HedgingEngine
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Usage: HedgingEngine <financial-params.json> <market-data.csv> <output.json>");
                return 1;
            }

            var backtest = new BacktestEngine();
            await backtest.RunAsync(args[0], args[1], args[2]);
            return 0;
        }
    }
}
