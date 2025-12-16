using MarketData;

namespace HedgingEngine.Portfolio
{
    public class PortfolioState
    {
        public DateTime Date { get; init; }
        public Dictionary<string, double> Compositions { get; init; }
        public double Cash { get; init; }
        public double PortfolioValue { get; init; }
        public double Price { get; init; }
        public double PriceStdDev { get; init; }
        public List<double> DeltaStdDev { get; init; }

        public PortfolioState(DateTime date, Dictionary<string, double> compositions, double cash, double portfolioValue, 
                              double price = 0.0, double priceStdDev = 0.0, List<double>? deltaStdDev = null)
        {
            Date = date;
            Compositions = new Dictionary<string, double>(compositions);
            Cash = cash;
            PortfolioValue = portfolioValue;
            Price = price;
            PriceStdDev = priceStdDev;
            DeltaStdDev = deltaStdDev ?? new List<double>();
        }
    }

    public class Portfolio
    {
        public Dictionary<string, double> Compositions { get; private set; }
        public double Cash { get; private set; } = 0;
        public DateTime Date { get; private set; }
        public List<PortfolioState> History { get; private set; } = new();

        public Portfolio(Dictionary<string, double> dictInit, DataFeed data, double value)
        {
            Compositions = dictInit;
            Cash = value - Utilities.VectorMath.Dot(dictInit, data.SpotList);
            Date = data.Date;
            RecordState(value, 0.0, 0.0, null);
        }

        public void UpdateCompo(Dictionary<string, double> newDeltas, DataFeed feed, double value, 
                                 double price = 0.0, double priceStdDev = 0.0, List<double>? deltaStdDev = null)
        {
            Compositions = newDeltas;
            Cash = value - Utilities.VectorMath.Dot(newDeltas, feed.SpotList);
            Date = feed.Date;
            RecordState(value, price, priceStdDev, deltaStdDev);
        }

        public double GetPortfolioValue(DataFeed data, double deltaTime, double interestRate)
        {
            double capitalizedCash = Cash * Math.Exp(interestRate * deltaTime);
            return Utilities.VectorMath.Dot(Compositions, data.SpotList) + capitalizedCash;
        }

        private void RecordState(double portfolioValue, double price = 0.0, double priceStdDev = 0.0, List<double>? deltaStdDev = null)
        {
            History.Add(new PortfolioState(Date, Compositions, Cash, portfolioValue, price, priceStdDev, deltaStdDev));
        }
    }
}
