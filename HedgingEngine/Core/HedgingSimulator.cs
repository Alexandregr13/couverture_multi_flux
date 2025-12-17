using System;
using System.Collections.Generic;
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

            for (int t = 0; t < dataFeeds.Count; t++)
            {
                var feed = dataFeeds[t];
                var spots = new List<double>();
                foreach (var id in parameters.UnderlyingIds)
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

            var portfolio = new Portfolio.Portfolio(deltas, feed, pricingOutput.Price);

            var lastState = portfolio.History.Last();
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
                foreach (var id in parameters.UnderlyingIds)
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