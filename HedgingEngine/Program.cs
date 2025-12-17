using HedgingEngine.Core;

namespace HedgingEngine
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Error, invalid syntax. It should be like this: BacktestConsole.exe <test-params> <mkt-data> <output-file>");
                return 1;
            }

            var backtest = new BacktestEngine();
            await backtest.RunAsync(args[0], args[1], args[2]);
            return 0;
        }
    }
}
