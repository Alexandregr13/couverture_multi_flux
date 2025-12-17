using System.IO;
using ParameterInfo;
using ParameterInfo.JsonUtils;
using MarketData;

namespace HedgingEngine.IO
{
    public static class InputReader
    {
        public static TestParameters LoadTestParameters(string financialParamFile)
        {
            return JsonIO.FromJson(File.ReadAllText(financialParamFile));
        }

        public static List<DataFeed> LoadMarketData(string marketDataFile)
        {
            return MarketDataReader.ReadDataFeeds(marketDataFile).ToList();
        }
    }
}