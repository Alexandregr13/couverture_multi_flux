namespace HedgingEngine.Pricing
{
    public class PricingResults
    {
        public Dictionary<string, double> Deltas = new();
        public Dictionary<string, double> DeltasStdDev = new();
        public double Price;
        public double PriceStdDev;

        public PricingResults(double[] deltas, double price, double[] deltasStdDev, double priceStdDev, string[] ids)
        {
            Price = price;
            PriceStdDev = priceStdDev;

            for (int i = 0; i < deltas.Length; i++)
            {
                Deltas[ids[i]] = deltas[i];
                DeltasStdDev[ids[i]] = deltasStdDev[i];
            }
        }
    }
}
