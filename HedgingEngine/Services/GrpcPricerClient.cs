using Grpc.Net.Client;
using GrpcPricing.Protos;
using Grpc.Core;

namespace HedgingEngine.Services
{
    public class GrpcPricerClient : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly GrpcPricer.GrpcPricerClient _client;
        private readonly string _serverAddress;

        public GrpcPricerClient(string serverAddress)
        {
            _serverAddress = serverAddress;
            
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            
            _channel = GrpcChannel.ForAddress(serverAddress);
            _client = new GrpcPricer.GrpcPricerClient(_channel);
            
            Console.WriteLine($"[GrpcPricerClient] Client initialisé pour {serverAddress}");
        }

        public async Task<ReqInfo> TestConnectionAsync()
        {
            try
            {
                Console.WriteLine("[GrpcPricerClient] Envoi du Heartbeat...");
                
                var request = new Empty();
                var response = await _client.HeartbeatAsync(request);
                
                Console.WriteLine("[GrpcPricerClient] Heartbeat reçu avec succès");
                Console.WriteLine($"  - Taux domestique: {response.DomesticInterestRate}");
                Console.WriteLine($"  - Step différence finie: {response.RelativeFiniteDifferenceStep}");
                Console.WriteLine($"  - Nombre d'échantillons: {response.SampleNb}");
                
                return response;
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"[GrpcPricerClient] ERREUR lors du Heartbeat: {ex.Status.Detail}");
                throw new Exception($"Impossible de contacter le serveur de pricing à {_serverAddress}", ex);
            }
        }

        public ReqInfo TestConnection()
        {
            return TestConnectionAsync().GetAwaiter().GetResult();
        }

        public async Task<PricingOutput> GetPriceAndDeltasAsync(List<List<double>> past, double time, bool isMonitoringDate)
        {
            try
            {
                Console.WriteLine($"[GrpcPricerClient] Appel PriceAndDeltas (time={time}, monitoring={isMonitoringDate}, past size={past.Count}x{past[0].Count})");
                
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
                
                var response = await _client.PriceAndDeltasAsync(request);
                
                Console.WriteLine($"[GrpcPricerClient] Prix reçu: {response.Price} ± {response.PriceStdDev}");
                Console.WriteLine($"[GrpcPricerClient] Deltas reçus: [{string.Join(", ", response.Deltas)}]");
                
                return response;
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"[GrpcPricerClient] ERREUR lors de PriceAndDeltas: {ex.Status.Detail}");
                Console.WriteLine($"[GrpcPricerClient] Détails: StatusCode={ex.StatusCode}, Message={ex.Message}");
                throw new Exception("Échec du calcul de pricing", ex);
            }
        }

        public PricingOutput GetPriceAndDeltas(List<List<double>> past, double time, bool isMonitoringDate)
        {
            return GetPriceAndDeltasAsync(past, time, isMonitoringDate).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Console.WriteLine("[GrpcPricerClient] Fermeture de la connexion");
            _channel?.ShutdownAsync().Wait();
        }
    }
}