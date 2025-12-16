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

        public async Task RunAsync(string financialParamFile, string marketDataFile, string outputFile)
        {
            var testParams = JsonIO.FromJson(File.ReadAllText(financialParamFile));
            var hedgingParams = new HedgingParams(testParams);
            var dataFeeds = MarketDataReader.ReadDataFeeds(marketDataFile).ToList();
            
            _grpcClient = new GrpcPricerClient("http://localhost:50051");
            await _grpcClient.TestConnectionAsync();
            
            var portfolio = await RunHedgingAsync(hedgingParams, dataFeeds);
            
            var outputData = portfolio.History.Select(h => new
            {
                OutputDate = h.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                PortfolioValue = double.IsNaN(h.PortfolioValue) || double.IsInfinity(h.PortfolioValue) ? 0.0 : h.PortfolioValue,
                Delta = h.Compositions.Values.Select(v => double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v).ToArray(),
                DeltaStdDev = h.DeltaStdDev.Select(v => double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v).ToArray(),
                Price = double.IsNaN(h.Price) || double.IsInfinity(h.Price) ? 0.0 : h.Price,
                PriceStdDev = double.IsNaN(h.PriceStdDev) || double.IsInfinity(h.PriceStdDev) ? 0.0 : h.PriceStdDev
            }).ToList();
            
            File.WriteAllText(outputFile, System.Text.Json.JsonSerializer.Serialize(outputData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        private async Task<Portfolio.Portfolio> RunHedgingAsync(HedgingParams parameters, List<DataFeed> dataFeeds)
        {
            var monitoringPast = new List<List<double>>();
            Portfolio.Portfolio? portfolio = null;
            DateTime? previousDate = null;
            bool optionExpired = false;
            
            for (int t = 0; t < dataFeeds.Count; t++)
            {
                var feed = dataFeeds[t];
                var spots = parameters.UnderlyingIds.Select(id => feed.SpotList[id]).ToList();
                
                double mathTime = parameters.DateConverter.ConvertToMathDistance(parameters.CreationDate, feed.Date);
                bool isMonitoringDate = parameters.IsMonitoringDate(feed.Date);
                
                if (isMonitoringDate || t == 0)
                    monitoringPast.Add(spots);
                
                double deltaTime = previousDate.HasValue 
                    ? parameters.DateConverter.ConvertToMathDistance(previousDate.Value, feed.Date) 
                    : 0;
                
                bool shouldRebalance = (t % parameters.RebalancingPeriod) == 0;
                
                if (t == 0)
                {
                    portfolio = await InitializePortfolioAsync(parameters, monitoringPast, feed, mathTime, isMonitoringDate);
                }
                else if (shouldRebalance && !optionExpired)
                {
                    optionExpired = await RebalancePortfolioAsync(parameters, portfolio!, monitoringPast, feed, mathTime, deltaTime, isMonitoringDate);
                }
                else
                {
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
            var pricingOutput = await _grpcClient!.GetPriceAndDeltasAsync(past, mathTime, isMonitoringDate);
            var deltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            
            var portfolio = new Portfolio.Portfolio(deltas, feed, pricingOutput.Price);
            var lastState = portfolio.History.Last();
            portfolio.History[portfolio.History.Count - 1] = new Portfolio.PortfolioState(
                lastState.Date, lastState.Compositions, lastState.Cash, lastState.PortfolioValue,
                pricingOutput.Price, pricingOutput.PriceStdDev, pricingOutput.DeltasStdDev.ToList()
            );
            return portfolio;
        }

        private async Task<bool> RebalancePortfolioAsync(
            HedgingParams parameters, Portfolio.Portfolio portfolio, List<List<double>> past,
            DataFeed feed, double mathTime, double deltaTime, bool isMonitoringDate)
        {
            double portfolioValue = portfolio.GetPortfolioValue(feed, deltaTime, parameters.InterestRate);
            var pricingOutput = await _grpcClient!.GetPriceAndDeltasAsync(past, mathTime, isMonitoringDate);
            
            if (double.IsNaN(pricingOutput.Price) || pricingOutput.Price <= 0)
                return true;
            
            var newDeltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            portfolio.UpdateCompo(newDeltas, feed, portfolioValue, 
                                   pricingOutput.Price, pricingOutput.PriceStdDev, pricingOutput.DeltasStdDev.ToList());
            return false;
        }
    }
}