using ParameterInfo;
using HedgingEngine.Pricing;
using MarketData;
using HedgingEngine.Rebalancing;
using GrpcPricing.Protos;
using TimeHandler;

namespace HedgingEngine.Hedging
{

    public class Hedging
    {
        TestParameters FinancialParam { get; init; }
        Pricer Pricer { get; init; }
        Rebalancing.Rebalancing OracleRebalancing { get; init; }

        public Hedging(TestParameters parameters)
        {
            FinancialParam = parameters;
            Pricer = new PricerGrpc(parameters);
            OracleRebalancing = new FixedRebalancing(parameters.RebalancingOracleDescription.Period);
        }

        public List<OutputData> Hedge(List<DataFeed> dataFeeds)
        {
            // Convertisseur de dates financieres -> temps mathematique
            MathDateConverter converter = new(FinancialParam.NumberOfDaysInOneYear);

            // Taux sans risque domestique
            double r = FinancialParam.AssetDescription.CurrencyRates[FinancialParam.AssetDescription.DomesticCurrencyId];

            // t=0
            List<DataFeed> past = [dataFeeds[0]];
            bool monitoringDate = false;
            double timeMath = converter.ConvertToMathDistance(FinancialParam.PayoffDescription.CreationDate, dataFeeds[0].Date);

            // Premier appel au pricer: obtenir V_0 et Δ_0
            PricingParams pricerParams = new(past, timeMath, monitoringDate);
            PricingResults results = Pricer.PriceAndDeltas(pricerParams);

            // Constituer le portefeuille initial: V_0 = Δ_0·S_0 + C_0
            Portfolio.Portfolio portfolio = new(results.Deltas, dataFeeds[0], results.Price);

            // Premier OutputData
            OutputData output0 = new()
            {
                Value = results.Price,
                Date = dataFeeds[0].Date,
                Price = results.Price,
                PriceStdDev = results.PriceStdDev,
                Deltas = [.. results.Deltas.Values],
                DeltasStdDev = [.. results.DeltasStdDev.Values]
            };
            List<OutputData> listOutput = [output0];

            // Historique des observations (pour le pricer)
            // hedgingPast contient uniquement les dates de monitoring passees
            List<DataFeed> hedgingPast = [dataFeeds[0]];

            // boucle date de trading
            foreach (DataFeed feed in dataFeeds.Skip(1))
            {
                // Temps mathematique depuis la creation
                timeMath = converter.ConvertToMathDistance(FinancialParam.PayoffDescription.CreationDate, feed.Date);

                // Est-ce une date de monitoring (date de paiement potentiel)?
                monitoringDate = FinancialParam.PayoffDescription.PaymentDates.Contains(feed.Date);

                // Construction du "past" pour le pricer:
                // - Si monitoring: on ajoute definitivement cette date a l'historique
                // - Sinon: on ajoute temporairement pour le pricing
                if (monitoringDate)
                {
                    hedgingPast.Add(feed);
                    past = new List<DataFeed>(hedgingPast);
                }
                else
                {
                    past = new List<DataFeed>(hedgingPast);
                    past.Add(feed);
                }

                // Appel au pricer pour obtenir le nouveau prix et les nouveaux deltas
                pricerParams.SetParams(past, timeMath, monitoringDate);
                results = Pricer.PriceAndDeltas(pricerParams);

                // rebalancing si oracle ok
                if (OracleRebalancing.IsRebalancing(feed.Date))
                {
                    // Temps ecoule depuis le dernier rebalancing (pour capitaliser le cash)
                    double deltaTime = converter.ConvertToMathDistance(portfolio.Date, feed.Date);

                    // Valeur du portefeuille avec cash capitalise: V_t = Δ·S_t + C·e^(r·Δt)
                    double value = portfolio.GetPortfolioValue(feed, deltaTime, r);

                    // Rebalancing autofinancant: passer de Δ_{k-1} a Δ_k
                    // C_k^+ = V_k - Δ_k·S_k
                    portfolio.UpdateCompo(results.Deltas, feed, value);

                    OutputData output = new()
                    {
                        Value = value,
                        Date = feed.Date,
                        Price = results.Price,
                        PriceStdDev = results.PriceStdDev,
                        Deltas = [.. results.Deltas.Values],
                        DeltasStdDev = [.. results.DeltasStdDev.Values]
                    };
                    listOutput.Add(output);
                }
            }

            return listOutput;
        }
    }
}
