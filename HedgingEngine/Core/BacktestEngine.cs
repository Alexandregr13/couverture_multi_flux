// using HedgingEngine.Utils;
using HedgingEngine.Helpers;
using PricingLibrary.Computations;
using PricingLibrary.DataClasses;
using PricingLibrary.MarketDataFeed;

namespace HedgingEngine.Core
{
    public class BacktestEngine
    {
        private Pricer pricer = null!;
        private RebalancingOracle rebalancingOracle = null!;
        private PortfolioManager portfolioManager = null!;

        public void Run(string testParamsPath, string marketDataPath, string outputPath)
        {
            var testParams = FileHelper.LoadTestParams(testParamsPath);
            var marketData = FileHelper.LoadMarketData(marketDataPath);
            var dataFeeds = MarketDataHelper.ConvertToDataFeeds(marketData);
            var res = RunBacktest(testParams, dataFeeds);
            FileHelper.OutputRes(res, outputPath);
        }

        private List<OutputData> RunBacktest(BasketTestParameters testParams, List<DataFeed> dataFeeds)
        {
            var res = new List<OutputData>();
            var underlyingIds = testParams.BasketOption.UnderlyingShareIds;

            InitializeBacktest(testParams, dataFeeds.First(), underlyingIds);
            res.Add(CreateOutputData(dataFeeds.First(), portfolioManager.CalculateValue(dataFeeds.First()), underlyingIds));

            for (var dayIdx = 1; dayIdx < dataFeeds.Count; dayIdx++)
            {
                var currentFeed = dataFeeds[dayIdx];

                if (rebalancingOracle.ShouldRebalance())
                {
                    var portfolioValue = portfolioManager.CalculateValue(currentFeed);
                    portfolioManager.UpdateComposition(currentFeed, underlyingIds);
                    res.Add(CreateOutputData(currentFeed, portfolioValue, underlyingIds));
                }
            }
            return res;
        }

        private void InitializeBacktest(BasketTestParameters testParams, DataFeed firstDataFeed, string[] underlyingIds)
        {
            pricer = new Pricer(testParams);
            rebalancingOracle = new RebalancingOracle(testParams.RebalancingOracleDescription);
            portfolioManager = new PortfolioManager(pricer);

            var firstSpots = MarketDataHelper.GetOrderedSpots(firstDataFeed, underlyingIds);
            var firstRes = pricer.Price(firstDataFeed.Date, firstSpots);
            var deltaDict = VectorMath.ArrayToDict(firstRes.Deltas, underlyingIds);

            portfolioManager.Initialize(firstRes.Price, deltaDict, firstDataFeed);
        }

        private OutputData CreateOutputData(DataFeed dataFeed, double portfolioValue, string[] underlyingIds)
        {
            var spots = MarketDataHelper.GetOrderedSpots(dataFeed, underlyingIds);
            var pricingResults = pricer.Price(dataFeed.Date, spots);

            return new OutputData
            {
                Date = dataFeed.Date,
                Value = portfolioValue,
                Deltas = pricingResults.Deltas,
                DeltasStdDev = pricingResults.DeltaStdDev,
                Price = pricingResults.Price,
                PriceStdDev = pricingResults.PriceStdDev,
                TransactionCosts = 0.0
            };
        }
    }
}