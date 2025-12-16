using ParameterInfo;
using ParameterInfo.PayoffDescriptions;
using ParameterInfo.RebalancingOracleDescriptions;
using TimeHandler;

namespace HedgingEngine.Models
{
    public class HedgingParams
    {
        public TestParameters TestParams { get; }
        public string[] UnderlyingIds { get; }
        public DateTime[] PaymentDates { get; }
        public double InterestRate { get; }
        public IMathDateConverter DateConverter { get; }
        public int RebalancingPeriod { get; }
        public DateTime CreationDate { get; }

        public HedgingParams(TestParameters testParams)
        {
            TestParams = testParams;
            
            // Extraire et trier les IDs des sous-jacents
            UnderlyingIds = testParams.AssetDescription.UnderlyingCurrencyCorrespondence.Keys
                .OrderBy(k => k)
                .ToArray();
            
            // Extraire les dates de paiement selon le type de payoff
            PaymentDates = testParams.PayoffDescription switch
            {
                ConditionalBasketPayoffDescription cb => cb.PaymentDates,
                ConditionalMaxPayoffDescription cm => cm.PaymentDates,
                _ => throw new NotSupportedException($"Unknown payoff type: {testParams.PayoffDescription.GetType().Name}")
            };

            // Extraire le taux d'intérêt domestique
            InterestRate = testParams.AssetDescription.CurrencyRates[
                testParams.AssetDescription.DomesticCurrencyId];

            // Stocker la date de création
            CreationDate = testParams.PayoffDescription.CreationDate;

            // Créer le convertisseur de dates mathématiques
            DateConverter = new MathDateConverter(testParams.NumberOfDaysInOneYear);

            // Extraire la période de rebalancement
            RebalancingPeriod = testParams.RebalancingOracleDescription switch
            {
                FixedTimesOracleDescription fixedOracle => fixedOracle.Period,
                _ => throw new NotSupportedException($"Unknown oracle type: {testParams.RebalancingOracleDescription.GetType().Name}")
            };
        }

        public bool IsMonitoringDate(DateTime date)
        {
            return PaymentDates.Any(pd => pd.Date == date.Date);
        }
    }
}