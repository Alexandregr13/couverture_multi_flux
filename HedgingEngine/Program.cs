using Grpc.Net.Client;
using GrpcPricing.Protos;
using MarketData;
using ParameterInfo.JsonUtils;
using ParameterInfo;

namespace HedgingEngine
{
    class Program
    {
        static void Main(string[] args)
        {
            // Verification des arguments
            if (args.Length != 3)
            {
                Console.WriteLine($"Erreur : Taille attendue du args est 3, alors que la taille est {args.Length}");
                Environment.Exit(1);
            }

            if (!File.Exists(args[0]) || !File.Exists(args[1]))
            {
                Console.WriteLine("Chemin non valide passe en args");
                Environment.Exit(1);
            }

            if (!args[0].EndsWith(".json") || !args[1].EndsWith(".csv") || !args[2].EndsWith(".json"))
            {
                Console.WriteLine("Les extensions de fichiers attendues sont : .json, .csv et .json");
                Environment.Exit(1);
            }

            // Lecture des donnees de marche
            List<DataFeed> data = MarketDataReader.ReadDataFeeds(args[1]);

            // Lecture des parametres financiers
            string jsonString = File.ReadAllText(args[0]);
            TestParameters financialParam = JsonIO.FromJson(jsonString);

            // Execution du hedging
            Hedging.Hedging hedger = new(financialParam);
            List<OutputData> listOutput = hedger.Hedge(data);

            // Ecriture des resultats
            File.WriteAllText(args[2], JsonIO.ToJson(listOutput));
        }
    }
}