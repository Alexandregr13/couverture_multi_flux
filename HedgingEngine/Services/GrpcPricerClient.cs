using Grpc.Net.Client;
using GrpcPricing.Protos;

namespace HedgingEngine.Services
{
    public class GrpcPricerClient
    {
        private readonly GrpcChannel _channel;
        private readonly GrpcPricer.GrpcPricerClient _client;

        public GrpcPricerClient(string serverAddress)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            _channel = GrpcChannel.ForAddress(serverAddress);
            _client = new GrpcPricer.GrpcPricerClient(_channel);
        }

        public async Task<ReqInfo> TestConnectionAsync()
        {
            return await _client.HeartbeatAsync(new Empty());
        }

        public async Task<PricingOutput> GetPriceAndDeltasAsync(List<List<double>> past, double time, bool isMonitoringDate)
        {
            var request = new PricingInput
            {
                Time = time,
                MonitoringDateReached = isMonitoringDate
            };
            
            foreach (var row in past)
            {
                var pastLine = new PastLines();
                pastLine.Value.AddRange(row);
                request.Past.Add(pastLine);
            }
            
            return await _client.PriceAndDeltasAsync(request);
        }
    }
}
