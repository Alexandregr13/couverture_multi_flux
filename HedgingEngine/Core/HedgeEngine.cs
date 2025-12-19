using System.Threading.Tasks;
using HedgingEngine.Models;
using HedgingEngine.Services;
using HedgingEngine.IO;

namespace HedgingEngine.Core
{
    public class HedgeEngine
    {
        public async Task RunAsync(string financialParamFile, string marketDataFile, string outputFile)
        {
            // data loading
            var testParams = InputReader.LoadTestParameters(financialParamFile);
            var hedgingParams = new HedgingParams(testParams);
            var dataFeeds = InputReader.LoadMarketData(marketDataFile);

            // initialize gRPC
            var grpcClient = new GrpcPricerClient("http://localhost:50051");
            await grpcClient.TestConnectionAsync();

            // launch simulation
            var simulator = new HedgingSimulator(grpcClient);
            var portfolio = await simulator.SimulateAsync(hedgingParams, dataFeeds);

            // write results
            OutputWriter.WritePortfolioHistory(portfolio, outputFile);
        }
    }
}
