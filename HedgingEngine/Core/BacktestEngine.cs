using System;
#nullable disable
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
using System.Text.Json;

namespace HedgingEngine.Core
{
    public class BacktestEngine
    {
        private GrpcPricerClient _grpcClient;

        public async Task RunAsync(string financialParamFile, string marketDataFile, string outputFile)
        {
            var testParams = JsonIO.FromJson(File.ReadAllText(financialParamFile));
            var hedgingParams = new HedgingParams(testParams);
            var dataFeeds = MarketDataReader.ReadDataFeeds(marketDataFile).ToList();
            
            _grpcClient = new GrpcPricerClient("http://localhost:50051");
            await _grpcClient.TestConnectionAsync();
            
            var portfolio = await RunHedgingAsync(hedgingParams, dataFeeds);
            
            var outputData = new List<object>();
            foreach (var h in portfolio.History)
            {
                outputData.Add(new
                {
                    OutputDate = h.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                    PortfolioValue = h.PortfolioValue,
                    Delta = h.Compositions.Values.ToArray(),
                    DeltaStdDev = h.DeltaStdDev.ToArray(),
                    Price = h.Price,
                    PriceStdDev = h.PriceStdDev
                });
            }
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(outputFile, JsonSerializer.Serialize(outputData, options));
        }

        private async Task<Portfolio.Portfolio> RunHedgingAsync(HedgingParams parameters, List<DataFeed> dataFeeds)
        {
            var monitoringPast = new List<List<double>>();
            Portfolio.Portfolio portfolio = null;
            DateTime previousDate = DateTime.MinValue;
            
            for (int t = 0; t < dataFeeds.Count; t++)
            {
                var feed = dataFeeds[t];
                var spots = new List<double>();
                foreach(var id in parameters.UnderlyingIds)
                {
                    spots.Add(feed.SpotList[id]);
                }
                
                double mathTime = parameters.DateConverter.ConvertToMathDistance(parameters.CreationDate, feed.Date);
                bool isMonitoringDate = parameters.IsMonitoringDate(feed.Date);
                
                if (isMonitoringDate || t == 0)
                {
                    monitoringPast.Add(spots);
                }
                
                double deltaTime = 0;
                if (t > 0)
                {
                    deltaTime = parameters.DateConverter.ConvertToMathDistance(previousDate, feed.Date);
                }
                
                bool shouldRebalance = (t % parameters.RebalancingPeriod) == 0;
                
                if (t == 0)
                {
                    portfolio = await InitializePortfolioAsync(parameters, monitoringPast, feed, mathTime, isMonitoringDate);
                }
                else if (shouldRebalance)
                {
                    await RebalancePortfolioAsync(parameters, portfolio, monitoringPast, feed, mathTime, deltaTime, isMonitoringDate);
                }
                else
                {
                    double portfolioValue = portfolio.GetPortfolioValue(feed, deltaTime, parameters.InterestRate);
                    portfolio.UpdateCompo(portfolio.Compositions, feed, portfolioValue);
                }
                
                previousDate = feed.Date;
            }
            
            return portfolio;
        }

        private async Task<Portfolio.Portfolio> InitializePortfolioAsync(
            HedgingParams parameters, List<List<double>> past, DataFeed feed, double mathTime, bool isMonitoringDate)
        {
            var pricingOutput = await _grpcClient.GetPriceAndDeltasAsync(past, mathTime, isMonitoringDate);
            var deltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            
            var portfolio = new Portfolio.Portfolio(deltas, feed, pricingOutput.Price);
            
            var lastState = portfolio.History.Last();
           // injecter les stats du pricer dans la première ligne de l'historique sans avoir à surcharger le constructeur de la classe Portfolio avec trop de paramètres.
            portfolio.History[portfolio.History.Count - 1] = new Portfolio.PortfolioState(
                lastState.Date, lastState.Compositions, lastState.Cash, lastState.PortfolioValue,
                pricingOutput.Price, pricingOutput.PriceStdDev, pricingOutput.DeltasStdDev.ToList()
            );
            return portfolio;
        }

        private async Task RebalancePortfolioAsync(
            HedgingParams parameters, Portfolio.Portfolio portfolio, List<List<double>> past,
            DataFeed feed, double mathTime, double deltaTime, bool isMonitoringDate)
        {
            double portfolioValue = portfolio.GetPortfolioValue(feed, deltaTime, parameters.InterestRate);
            
            var pastToSend = new List<List<double>>(past);
            if (!isMonitoringDate)
            {
                var spots = new List<double>();
                foreach(var id in parameters.UnderlyingIds)
                {
                    spots.Add(feed.SpotList[id]);
                }
                pastToSend.Add(spots);
            }

            var pricingOutput = await _grpcClient.GetPriceAndDeltasAsync(pastToSend, mathTime, isMonitoringDate);
            
            var newDeltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            portfolio.UpdateCompo(newDeltas, feed, portfolioValue, 
                                   pricingOutput.Price, pricingOutput.PriceStdDev, pricingOutput.DeltasStdDev.ToList());
        }
    }
}