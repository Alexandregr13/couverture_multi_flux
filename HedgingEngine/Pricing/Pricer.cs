using ParameterInfo;

namespace HedgingEngine.Pricing
{
    public abstract class Pricer
    {
        public TestParameters Params { get; init; }

        public Pricer(TestParameters testParameters)
        {
            Params = testParameters;
        }

        public abstract PricingResults PriceAndDeltas(PricingParams pricingParams);
    }
}
