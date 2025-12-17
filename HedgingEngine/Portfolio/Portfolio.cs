using MarketData;
using HedgingEngine.Utilities;

namespace HedgingEngine.Portfolio
{
    public record PortfolioState(
        DateTime Date,
        Dictionary<string, double> Compositions,
        double Cash,
        double PortfolioValue,
        double Price,
        double PriceStdDev,
        List<double>? DeltaStdDev
    );

    public class Portfolio
    {
        public Dictionary<string, double> Compositions { get; private set; }
        public double Cash { get; private set; }
        public DateTime Date { get; private set; }
        public List<PortfolioState> History { get; } = new();

        public Portfolio(Dictionary<string, double> initialDeltas, DataFeed data, double initialPrice)
        {
            Compositions = initialDeltas;
            Cash = initialPrice - VectorMath.Dot(initialDeltas, data.SpotList);
            Date = data.Date;
            RecordState(initialPrice);
        }

        public void UpdateCompo(Dictionary<string, double> newDeltas, DataFeed feed, double value, 
                                 double price = 0, double priceStdDev = 0, List<double>? deltaStdDev = null)
        {
            Compositions = newDeltas;
            Cash = value - VectorMath.Dot(newDeltas, feed.SpotList);
            Date = feed.Date;
            RecordState(value, price, priceStdDev, deltaStdDev);
        }

        public double GetPortfolioValue(DataFeed data, double deltaTime, double interestRate)
        {
            return VectorMath.Dot(Compositions, data.SpotList) + Cash * Math.Exp(interestRate * deltaTime);
        }

        private void RecordState(double value, double price = 0, double priceStdDev = 0, List<double>? deltaStdDev = null)
        {
            History.Add(new PortfolioState(
                Date,
                new Dictionary<string, double>(Compositions),
                Cash,
                value,
                price,
                priceStdDev,
                deltaStdDev ?? new List<double>()
            ));
        }
    }
}
