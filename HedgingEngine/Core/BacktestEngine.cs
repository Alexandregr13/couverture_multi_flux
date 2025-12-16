// using System;
// using System.Linq;
// using System.IO;
// using System.Threading.Tasks;
// using System.Collections.Generic;
// using ParameterInfo;
// using ParameterInfo.JsonUtils;
// using MarketData;
// using HedgingEngine.Models;
// using HedgingEngine.Portfolio;
// using HedgingEngine.Utilities;
// using HedgingEngine.Services;

// namespace HedgingEngine.Core
// {
//     public class BacktestEngine
//     {
//         private GrpcPricerClient? _grpcClient;
//         private const string GrpcServerAddress = "http://localhost:50051";

//         public async Task RunAsync(string financialParamFile, string marketDataFile, string outputFile)
//         {
//             Console.WriteLine("[BacktestEngine] Chargement des paramètres...");
//             var testParams = JsonIO.FromJson(File.ReadAllText(financialParamFile));
//             var hedgingParams = new HedgingParams(testParams);
//             var dataFeeds = MarketDataReader.ReadDataFeeds(marketDataFile).ToList();
            
//             Console.WriteLine($"[BacktestEngine] {dataFeeds.Count} observations chargées");
            
//             _grpcClient = new GrpcPricerClient(GrpcServerAddress);
//             await _grpcClient.TestConnectionAsync();
            
//             var portfolio = await RunHedgingAsync(hedgingParams, dataFeeds);
            
//             Console.WriteLine($"\n✅ Simulation terminée");
//             Console.WriteLine($"✅ Valeur finale du portefeuille: {portfolio.History.Last().PortfolioValue:F6}");
            
//             // Export results
//             var outputData = portfolio.History.Select(h => new
//             {
//                 Date = h.Date.ToString("yyyy-MM-dd"),
//                 Value = h.PortfolioValue,
//                 Cash = h.Cash,
//                 Deltas = h.Compositions
//             }).ToList();
            
//             File.WriteAllText(outputFile, System.Text.Json.JsonSerializer.Serialize(outputData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
//             Console.WriteLine($"[BacktestEngine] Résultats exportés vers {outputFile}");
//         }

//         private async Task<Portfolio.Portfolio> RunHedgingAsync(HedgingParams parameters, List<DataFeed> dataFeeds)
//         {
//             var allPast = new List<List<double>>();
//             Portfolio.Portfolio? portfolio = null;
//             DateTime? previousDate = null;
            
//             for (int t = 0; t < dataFeeds.Count; t++)
//             {
//                 var feed = dataFeeds[t];
//                 var spots = parameters.UnderlyingIds.Select(id => feed.SpotList[id]).ToList();
                
//                 // Accumuler TOUTES les observations
//                 allPast.Add(spots);
                
//                 double mathTime = parameters.DateConverter.ConvertToMathDistance(parameters.CreationDate, feed.Date);
//                 bool isMonitoringDate = parameters.IsMonitoringDate(feed.Date);
                
//                 double deltaTime = 0;
//                 if (previousDate.HasValue)
//                 {
//                     deltaTime = parameters.DateConverter.ConvertToMathDistance(previousDate.Value, feed.Date);
//                 }
                
//                 bool shouldRebalance = (t % parameters.RebalancingPeriod) == 0;
                
//                 if (t == 0)
//                 {
//                     Console.WriteLine($"[t={t}] Initialisation (monitoring={isMonitoringDate})");
//                     portfolio = await InitializePortfolioAsync(parameters, allPast, feed, mathTime, isMonitoringDate);
//                 }
//                 else if (shouldRebalance)
//                 {
//                     Console.WriteLine($"[t={t}] Rebalancement (monitoring={isMonitoringDate}, time={mathTime:F6})");
//                     await RebalancePortfolioAsync(parameters, portfolio!, allPast, feed, mathTime, deltaTime, isMonitoringDate);
//                 }
//                 else
//                 {
//                     // Pas de rebalancement : capitalisation simple
//                     double portfolioValue = portfolio!.GetPortfolioValue(feed, deltaTime, parameters.InterestRate);
//                     portfolio.UpdateCompo(portfolio.Compositions, feed, portfolioValue);
//                 }
                
//                 previousDate = feed.Date;
//             }
            
//             return portfolio!;
//         }

//         private async Task<Portfolio.Portfolio> InitializePortfolioAsync(
//             HedgingParams parameters, List<List<double>> past, DataFeed feed, double mathTime, bool isMonitoringDate)
//         {
//             var pricingOutput = await _grpcClient!.GetPriceAndDeltasAsync(past, mathTime, isMonitoringDate);
            
//             var deltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            
//             Console.WriteLine($"  Prix initial: {pricingOutput.Price:F6} ± {pricingOutput.PriceStdDev:F6}");
//             Console.WriteLine($"  Deltas: [{string.Join(", ", pricingOutput.Deltas.Select(d => d.ToString("F4")))}]");
            
//             return new Portfolio.Portfolio(deltas, feed, pricingOutput.Price);
//         }

//         private async Task RebalancePortfolioAsync(
//             HedgingParams parameters, Portfolio.Portfolio portfolio, List<List<double>> past,
//             DataFeed feed, double mathTime, double deltaTime, bool isMonitoringDate)
//         {
//             double portfolioValue = portfolio.GetPortfolioValue(feed, deltaTime, parameters.InterestRate);
            
//             var pricingOutput = await _grpcClient!.GetPriceAndDeltasAsync(past, mathTime, isMonitoringDate);
            
//             var newDeltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            
//             Console.WriteLine($"  Valeur portfolio: {portfolioValue:F6}");
//             Console.WriteLine($"  Nouveau prix: {pricingOutput.Price:F6}");
            
//             portfolio.UpdateCompo(newDeltas, feed, portfolioValue);
//         }
//     }
// }


using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using ParameterInfo;
using ParameterInfo.JsonUtils;
using MarketData;
using HedgingEngine.Models;
using HedgingEngine.Portfolio;
using HedgingEngine.Utilities;
using HedgingEngine.Services;
using GrpcPricing.Protos;

namespace HedgingEngine.Core
{
    public class BacktestEngine
    {
        private GrpcPricerClient? _grpcClient;
        private const string GrpcServerAddress = "http://localhost:50051";
        private const bool UseMockPricer = true; // 🔧 Activer le mode mock

        public async Task RunAsync(string financialParamFile, string marketDataFile, string outputFile)
        {
            Console.WriteLine("[BacktestEngine] Chargement des paramètres...");
            var testParams = JsonIO.FromJson(File.ReadAllText(financialParamFile));
            var hedgingParams = new HedgingParams(testParams);
            var dataFeeds = MarketDataReader.ReadDataFeeds(marketDataFile).ToList();
            
            Console.WriteLine($"[BacktestEngine] {dataFeeds.Count} observations chargées");
            
            if (!UseMockPricer)
            {
                _grpcClient = new GrpcPricerClient(GrpcServerAddress);
                await _grpcClient.TestConnectionAsync();
            }
            else
            {
                Console.WriteLine("⚠️  MODE MOCK ACTIVÉ - Pas de connexion gRPC");
            }
            
            var portfolio = await RunHedgingAsync(hedgingParams, dataFeeds);
            
            Console.WriteLine($"\n✅ Simulation terminée");
            Console.WriteLine($"✅ Valeur finale du portefeuille: {portfolio.History.Last().PortfolioValue:F6}");
            
            // Export results
            var outputData = portfolio.History.Select(h => new
            {
                Date = h.Date.ToString("yyyy-MM-dd"),
                Value = h.PortfolioValue,
                Cash = h.Cash,
                Deltas = h.Compositions
            }).ToList();
            
            File.WriteAllText(outputFile, System.Text.Json.JsonSerializer.Serialize(outputData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[BacktestEngine] Résultats exportés vers {outputFile}");
        }

        private async Task<Portfolio.Portfolio> RunHedgingAsync(HedgingParams parameters, List<DataFeed> dataFeeds)
        {
            var allPast = new List<List<double>>();
            Portfolio.Portfolio? portfolio = null;
            DateTime? previousDate = null;
            
            for (int t = 0; t < dataFeeds.Count; t++)
            {
                var feed = dataFeeds[t];
                var spots = parameters.UnderlyingIds.Select(id => feed.SpotList[id]).ToList();
                
                allPast.Add(spots);
                
                double mathTime = parameters.DateConverter.ConvertToMathDistance(parameters.CreationDate, feed.Date);
                bool isMonitoringDate = parameters.IsMonitoringDate(feed.Date);
                
                double deltaTime = 0;
                if (previousDate.HasValue)
                {
                    deltaTime = parameters.DateConverter.ConvertToMathDistance(previousDate.Value, feed.Date);
                }
                
                bool shouldRebalance = (t % parameters.RebalancingPeriod) == 0;
                
                if (t == 0)
                {
                    Console.WriteLine($"[t={t}] Initialisation (monitoring={isMonitoringDate})");
                    portfolio = await InitializePortfolioAsync(parameters, allPast, feed, mathTime, isMonitoringDate);
                }
                else if (shouldRebalance)
                {
                    Console.WriteLine($"[t={t}] Rebalancement (monitoring={isMonitoringDate}, time={mathTime:F6})");
                    await RebalancePortfolioAsync(parameters, portfolio!, allPast, feed, mathTime, deltaTime, isMonitoringDate);
                }
                else
                {
                    // Pas de rebalancement : capitalisation simple
                    double portfolioValue = portfolio!.GetPortfolioValue(feed, deltaTime, parameters.InterestRate);
                    portfolio.UpdateCompo(portfolio.Compositions, feed, portfolioValue);
                }
                
                previousDate = feed.Date;
            }
            
            return portfolio!;
        }

        private async Task<Portfolio.Portfolio> InitializePortfolioAsync(
            HedgingParams parameters, List<List<double>> past, DataFeed feed, double mathTime, bool isMonitoringDate)
        {
            PricingOutput pricingOutput;
            
            if (UseMockPricer)
            {
                pricingOutput = CreateMockPricingOutput(parameters.UnderlyingIds.Length);
            }
            else
            {
                pricingOutput = await _grpcClient!.GetPriceAndDeltasAsync(past, mathTime, isMonitoringDate);
            }
            
            var deltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            
            Console.WriteLine($"  Prix initial: {pricingOutput.Price:F6} ± {pricingOutput.PriceStdDev:F6}");
            Console.WriteLine($"  Deltas: [{string.Join(", ", pricingOutput.Deltas.Select(d => d.ToString("F4")))}]");
            
            return new Portfolio.Portfolio(deltas, feed, pricingOutput.Price);
        }

        private async Task RebalancePortfolioAsync(
            HedgingParams parameters, Portfolio.Portfolio portfolio, List<List<double>> past,
            DataFeed feed, double mathTime, double deltaTime, bool isMonitoringDate)
        {
            double portfolioValue = portfolio.GetPortfolioValue(feed, deltaTime, parameters.InterestRate);
            
            PricingOutput pricingOutput;
            
            if (UseMockPricer)
            {
                pricingOutput = CreateMockPricingOutput(parameters.UnderlyingIds.Length);
            }
            else
            {
                pricingOutput = await _grpcClient!.GetPriceAndDeltasAsync(past, mathTime, isMonitoringDate);
            }
            
            var newDeltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            
            Console.WriteLine($"  Valeur portfolio: {portfolioValue:F6}");
            Console.WriteLine($"  Nouveau prix: {pricingOutput.Price:F6}");
            
            portfolio.UpdateCompo(newDeltas, feed, portfolioValue);
        }

        // 🎭 Mock pricer : retourne des valeurs fixes
        private PricingOutput CreateMockPricingOutput(int nAssets)
        {
            var output = new PricingOutput
            {
                Price = 0.5,
                PriceStdDev = 0.01
            };
            
            // Deltas égaux (stratégie équipondérée)
            for (int i = 0; i < nAssets; i++)
            {
                output.Deltas.Add(0.2); // 20% par actif si 5 actifs
                output.DeltasStdDev.Add(0.005);
            }
            
            return output;
        }
    }
}