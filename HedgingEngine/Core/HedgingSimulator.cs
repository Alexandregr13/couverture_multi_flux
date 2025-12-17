using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketData;
using HedgingEngine.Models;
using HedgingEngine.Services;
using HedgingEngine.Utilities;

namespace HedgingEngine.Core
{
    public class HedgingSimulator
    {
        private readonly GrpcPricerClient _grpcClient;

        public HedgingSimulator(GrpcPricerClient grpcClient)
        {
            _grpcClient = grpcClient;
        }

        public async Task<Portfolio.Portfolio> SimulateAsync(HedgingParams parameters, List<DataFeed> dataFeeds)
        {
            var monitoringPast = new List<List<double>>();
            Portfolio.Portfolio? portfolio = null;
            DateTime previousDate = DateTime.MinValue;

            for (int stepIndex = 0; stepIndex < dataFeeds.Count; stepIndex++)
            {
                var feed = dataFeeds[stepIndex];
                var spots = GetCurrentSpots(parameters, feed);

                double mathTime = parameters.DateConverter.ConvertToMathDistance(parameters.CreationDate, feed.Date);
                bool isMonitoringDate = parameters.IsMonitoringDate(feed.Date);

                if (isMonitoringDate || stepIndex == 0)
                {
                    monitoringPast.Add(spots);
                }

                double deltaTime = 0;
                if (stepIndex > 0)
                {
                    deltaTime = parameters.DateConverter.ConvertToMathDistance(previousDate, feed.Date);
                }

                bool shouldRebalance = (stepIndex % parameters.RebalancingPeriod) == 0;

                if (stepIndex == 0)
                {
                    portfolio = await InitializePortfolioAsync(parameters, monitoringPast, feed, mathTime, isMonitoringDate);
                    previousDate = feed.Date;
                }
                else if (shouldRebalance)
                {
                    await RebalancePortfolioAsync(parameters, portfolio!, monitoringPast, feed, mathTime, deltaTime, isMonitoringDate);
                    previousDate = feed.Date;
                }
            }

            return portfolio!;
        }

        private async Task<Portfolio.Portfolio> InitializePortfolioAsync(
            HedgingParams parameters, List<List<double>> past, DataFeed feed, double mathTime, bool isMonitoringDate)
        {
            var pricingOutput = await _grpcClient.GetPriceAndDeltasAsync(past, mathTime, isMonitoringDate);
            var deltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);

            return new Portfolio.Portfolio(
                deltas,
                feed,
                pricingOutput.Price,
                pricingOutput.PriceStdDev,
                pricingOutput.DeltasStdDev.ToList()
            );
        }

        private async Task RebalancePortfolioAsync(
            HedgingParams parameters, Portfolio.Portfolio portfolio, List<List<double>> past,
            DataFeed feed, double mathTime, double deltaTime, bool isMonitoringDate)
        {
            double portfolioValue = portfolio.GetPortfolioValue(feed, deltaTime, parameters.InterestRate);

            var pastToSend = new List<List<double>>(past);
            if (!isMonitoringDate)
            {
                pastToSend.Add(GetCurrentSpots(parameters, feed));
            }

            var pricingOutput = await _grpcClient.GetPriceAndDeltasAsync(pastToSend, mathTime, isMonitoringDate);

            var newDeltas = VectorMath.ArrayToDict(pricingOutput.Deltas.ToArray(), parameters.UnderlyingIds);
            portfolio.UpdateCompo(newDeltas, feed, portfolioValue,
                                   pricingOutput.Price, pricingOutput.PriceStdDev, pricingOutput.DeltasStdDev.ToList());
        }

        private List<double> GetCurrentSpots(HedgingParams parameters, DataFeed feed)
        {
            return parameters.UnderlyingIds.Select(id => feed.SpotList[id]).ToList();
        }
    }
}