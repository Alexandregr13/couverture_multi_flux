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
        public List<PortfolioState> History { get; }

        public Portfolio(
            Dictionary<string, double> initialDeltas,
            DataFeed data,
            double initialPrice,
            double priceStdDev,
            List<double> deltaStdDev
        )
        {
            Compositions = new Dictionary<string, double>(initialDeltas);
            Cash = initialPrice - VectorMath.Dot(initialDeltas, data.SpotList);
            Date = data.Date;
            History = new List<PortfolioState>();

            History.Add(new PortfolioState(
                Date,
                new Dictionary<string, double>(Compositions),
                Cash,
                initialPrice, // a t=0 la valeur du pf c'est le prix de l'option
                initialPrice,
                priceStdDev,
                new List<double>(deltaStdDev)
            ));
        }

        public void UpdateCompo(
            Dictionary<string, double> newDeltas,
            DataFeed data,
            double portfolioValue,
            double price,
            double priceStdDev,
            List<double> deltaStdDev
        )
        {
            Cash = portfolioValue - VectorMath.Dot(newDeltas, data.SpotList);
            Compositions = new Dictionary<string, double>(newDeltas);
            Date = data.Date;

            History.Add(new PortfolioState(
                Date,
                new Dictionary<string, double>(Compositions),
                Cash,
                portfolioValue,
                price,
                priceStdDev,
                new List<double>(deltaStdDev)
            ));
        }

        public double GetPortfolioValue(DataFeed data, double deltaTime, double interestRate)
        {
            double spotValue = VectorMath.Dot(Compositions, data.SpotList);
            double cashValue = Cash * Math.Exp(interestRate * deltaTime);
            return spotValue + cashValue;
        }
    }
}