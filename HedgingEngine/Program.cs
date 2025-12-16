using HedgingEngine.Services;
using HedgingEngine.Models;

namespace HedgingEngine
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== HedgingEngine - Test du Pricing gRPC ===\n");

            string serverAddress = "http://localhost:50051";

            try
            {
                using (var pricerClient = new GrpcPricerClient(serverAddress))
                {
                    // Test 1 : Connexion avec Heartbeat
                    Console.WriteLine("=== TEST 1 : Heartbeat ===");
                    var serverInfo = pricerClient.TestConnection();
                    Console.WriteLine($"✓ Serveur prêt avec {serverInfo.SampleNb} échantillons Monte Carlo\n");

                    // Test 2 : Pricing avec données minimales
                    Console.WriteLine("=== TEST 2 : PriceAndDeltas avec données de test ===");
                    TestPricingWithMinimalData(pricerClient);

                    // Test 3 : Pricing avec données réelles du CSV (première date)
                    Console.WriteLine("\n=== TEST 3 : PriceAndDeltas avec données réelles ===");
                    TestPricingWithRealData(pricerClient);

                    Console.WriteLine("\n=== Tous les tests réussis ===");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n=== ERREUR ===");
                Console.WriteLine($"Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Détail: {ex.InnerException.Message}");
                }
                Console.WriteLine($"\nAssurez-vous que le serveur C++ est lancé avec :");
                Console.WriteLine($"  ./pricing_server <chemin_vers_math_param.json>");
                return;
            }
        }

        static void TestPricingWithMinimalData(GrpcPricerClient client)
        {
            var past = new PastData();
            
            past.AddObservation(13.0, 15.0, 17.0, 17.0, 14.0);
            past.AddObservation(13.3, 15.0, 17.13, 16.89, 14.16);

            past.Print();

            double time = 0.0;
            bool isMonitoringDate = false;

            Console.WriteLine($"\nAppel PriceAndDeltas(time={time}, monitoring={isMonitoringDate})...");
            var result = client.GetPriceAndDeltas(past.GetMatrix(), time, isMonitoringDate);

            Console.WriteLine("\n--- Résultats ---");
            Console.WriteLine($"Prix: {result.Price:F6} ± {result.PriceStdDev:F6}");
            Console.WriteLine($"Deltas: [{string.Join(", ", result.Deltas.Select(d => d.ToString("F6")))}]");
            Console.WriteLine($"Deltas StdDev: [{string.Join(", ", result.DeltasStdDev.Select(d => d.ToString("F6")))}]");
        }
        
        static void TestPricingWithRealData(GrpcPricerClient client)
        {
            var past = new PastData();
            past.AddObservation(13.0, 15.0, 17.0, 17.0, 14.0);

            past.Print();

            double time = 0.0;
            bool isMonitoringDate = true;

            Console.WriteLine($"\nAppel PriceAndDeltas(time={time}, monitoring={isMonitoringDate})...");
            var result = client.GetPriceAndDeltas(past.GetMatrix(), time, isMonitoringDate);

            Console.WriteLine("\n--- Résultats ---");
            Console.WriteLine($"Prix: {result.Price:F6} ± {result.PriceStdDev:F6}");
            Console.WriteLine($"Deltas: [{string.Join(", ", result.Deltas.Select(d => d.ToString("F6")))}]");

            var expectedDeltas = new[] { 0.1325889542909825, 0.1314798468295629, 0.13334867765152209, 0.130475298472051, 0.13853978075531614 };
            var expectedPrice = 0.5017426782428844;

            Console.WriteLine("\n--- Comparaison avec le portfolio attendu ---");
            Console.WriteLine($"Prix attendu: {expectedPrice:F6}");
            Console.WriteLine($"Deltas attendus: [{string.Join(", ", expectedDeltas.Select(d => d.ToString("F6")))}]");

            double priceError = Math.Abs(result.Price - expectedPrice);
            Console.WriteLine($"\nÉcart prix: {priceError:E2}");

            for (int i = 0; i < Math.Min(result.Deltas.Count, expectedDeltas.Length); i++)
            {
                double deltaError = Math.Abs(result.Deltas[i] - expectedDeltas[i]);
                Console.WriteLine($"Écart delta[{i}]: {deltaError:E2}");
            }
        }
    }
}