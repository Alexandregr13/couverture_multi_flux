using ParameterInfo;
using ParameterInfo.PayoffDescriptions;
using ParameterInfo.RebalancingOracleDescriptions;
using TimeHandler;

namespace HedgingEngine.Models
{
    public class HedgingParams
    {
        public string[] UnderlyingIds { get; }
        public DateTime[] PaymentDates { get; }
        public double InterestRate { get; }
        public IMathDateConverter DateConverter { get; }
        public int RebalancingPeriod { get; }
        public DateTime CreationDate { get; }

        public HedgingParams(TestParameters testParams)
        {
            UnderlyingIds = testParams.AssetDescription.UnderlyingCurrencyCorrespondence.Keys.OrderBy(k => k).ToArray();
            PaymentDates = testParams.PayoffDescription.PaymentDates;
            InterestRate = testParams.AssetDescription.CurrencyRates[testParams.AssetDescription.DomesticCurrencyId];
            CreationDate = testParams.PayoffDescription.CreationDate;
            DateConverter = new MathDateConverter(testParams.NumberOfDaysInOneYear);
            RebalancingPeriod = ((FixedTimesOracleDescription)testParams.RebalancingOracleDescription).Period;
        }

        public bool IsMonitoringDate(DateTime date)
        {
            return PaymentDates.Any(pd => pd.Date == date.Date);
        }
    }
}