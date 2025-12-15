using PricingLibrary.MarketDataFeed;

namespace HedgingEngine.Helpers
{
    public static class MarketDataHelper
    {
        public static List<DataFeed> ConvertToDataFeeds(List<ShareValue> shareValues)
        {
            return shareValues
                .GroupBy(d => d.DateOfPrice,
                    t => new { Symbol = t.Id.Trim(), Value = t.Value },
                    (key, g) => new DataFeed(key, g.ToDictionary(e => e.Symbol, e => e.Value)))
                .OrderBy(feed => feed.Date)
                .ToList();
        }

        public static double[] GetOrderedSpots(DataFeed dataFeed, string[] underlyingIds)
        {
            return underlyingIds.Select(id => dataFeed.PriceList[id]).ToArray();
        }
    }
}