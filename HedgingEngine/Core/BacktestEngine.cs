using System;
using System.Net.Http;
using Grpc.Net.Client;
using GrpcPricing.Protos;
using System.Text.Json;
using System.IO;

namespace HedgingEngine.Core
{
    public class BacktestEngine
    {
        private GrpcPricer.GrpcPricerClient? _grpcClient;
        private GrpcChannel? _channel;

        public void Run(string financialParamFile, string marketDataFile, string outputFile)
        {
            try
            {
                InitializeGrpcClient();
                var financialParams = JsonSerializer.Deserialize<JsonDocument>(File.ReadAllText(financialParamFile));
                var marketData = ReadMarketData(marketDataFile);
                
                // TODO: Implémenter la logique de couverture complète
                TestPricing();
                
                // TODO: Générer output avec MultiCashFlow.Common
                File.WriteAllText(outputFile, "{}"); // Placeholder
            }
            finally
            {
                _channel?.Dispose();
            }
        }

        private void InitializeGrpcClient()
        {
            var httpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _channel = GrpcChannel.ForAddress("http://localhost:50051",
                new GrpcChannelOptions { HttpHandler = httpHandler });

            _grpcClient = new GrpcPricer.GrpcPricerClient(_channel);
        }

        private List<Dictionary<string, string>> ReadMarketData(string filePath)
        {
            var marketData = new List<Dictionary<string, string>>();
            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) return marketData;

            var headers = lines[0].Split(',');
            
            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                var row = new Dictionary<string, string>();
                
                for (int j = 0; j < headers.Length && j < values.Length; j++)
                {
                    row[headers[j].Trim()] = values[j].Trim();
                }
                
                marketData.Add(row);
            }

            return marketData;
        }

        private void TestPricing()
        {
            if (_grpcClient == null) throw new InvalidOperationException("gRPC client not initialized");

            var request = new PricingInput
            {
                MonitoringDateReached = true,
                Time = 0.0
            };

            var response = _grpcClient.PriceAndDeltas(request);
        }
    }
}
