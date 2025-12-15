using System;
using HedgingEngine.Core;

namespace HedgingEngine
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Error, invalid syntax. It should be like this: PCPM.exe <test-params> <mkt-data> <output-file>");
                return 1; 
            }

            var backtest = new BacktestEngine();
            backtest.Run(args[0], args[1], args[2]);
            return 0;
        }
    }
}