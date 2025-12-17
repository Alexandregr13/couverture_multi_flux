using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HedgingEngine.Portfolio;

namespace HedgingEngine.IO
{
    public static class OutputWriter
    {
        public static void WritePortfolioHistory(HedgingEngine.Portfolio.Portfolio portfolio, string outputFile)
        {
            var outputData = portfolio.History.Select(h => new
            {
                date = h.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                value = h.PortfolioValue,
                deltas = h.Compositions?.Values.ToArray() ?? Array.Empty<double>(),
                deltasStdDev = h.DeltaStdDev?.ToArray() ?? Array.Empty<double>(),
                price = h.Price,
                priceStdDev = h.PriceStdDev
            }).ToList();

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true
            };
            
            File.WriteAllText(outputFile, JsonSerializer.Serialize(outputData, options));
        }
    }
}