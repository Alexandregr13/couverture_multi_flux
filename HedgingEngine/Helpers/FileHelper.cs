using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using PricingLibrary.MarketDataFeed;
using PricingLibrary.DataClasses;
using PricingLibrary.RebalancingOracleDescriptions;

namespace HedgingEngine.Helpers
{
    public static class FileHelper
    {
        public static BasketTestParameters LoadTestParams(string file)
        {
            var jsonContent = File.ReadAllText(file);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(), new RebalancingOracleDescriptionConverter() }
            };
            var res = JsonSerializer.Deserialize<BasketTestParameters>(jsonContent, options);
            if (res is null) 
                throw new InvalidOperationException("The test parameters file is invalid.");
            return res;
        }

        public static List<ShareValue> LoadMarketData(string file)
        {
            using var reader = new StringReader(File.ReadAllText(file));
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            return csv.GetRecords<ShareValue>().ToList();
        }

        public static void OutputRes(List<OutputData> results, string outputPath)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            var jsonContent = JsonSerializer.Serialize(results, options);
            File.WriteAllText(outputPath, jsonContent);
        }
    }
}