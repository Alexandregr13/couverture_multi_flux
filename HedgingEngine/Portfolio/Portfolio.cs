using MarketData;
using TimeHandler;

namespace HedgingEngine.Portfolio
{
    public class PortfolioState
    {
        public DateTime Date { get; init; }
        public Dictionary<string, double> Compositions { get; init; }
        public double Cash { get; init; }
        public double PortfolioValue { get; init; }

        public PortfolioState(DateTime date, Dictionary<string, double> compositions, double cash, double portfolioValue)
        {
            Date = date;
            Compositions = new Dictionary<string, double>(compositions);
            Cash = cash;
            PortfolioValue = portfolioValue;
        }
    }


    public class Portfolio
    {
        public Dictionary<string, double> Compositions { get; private set; }
        public double Cash { get; private set; } = 0;
        public DateTime Date { get; private set; }

        public List<PortfolioState> History { get; private set; } = new();

        /// Initialise le portefeuille a t=0
        /// C_0 = V_0 - delta_0 · S_0
        public Portfolio(Dictionary<string, double> dictInit, DataFeed data, double value)
        {
            Compositions = dictInit;
            Cash = value - Utilities.VectorMath.Dot(dictInit, data.SpotList);
            Date = data.Date;

            RecordState(value);
        }

        public void UpdateCompo(Dictionary<string, double> newDeltas, DataFeed feed, double value)
        {
            Compositions = newDeltas;
            // C_{t_k}^+ = V_{t_k} - delta_k · S_{t_k}
            Cash = value - Utilities.VectorMath.Dot(newDeltas, feed.SpotList);
            Date = feed.Date;

            RecordState(value);
        }

        public double GetPortfolioValue(DataFeed feed, double time, double r)
        {
            // Capitalisation du cash: C^- = C^+ * e^(r*delta_t)
            double capitalizedCash = Cash * Math.Exp(r * time);
            double value = Utilities.VectorMath.Dot(Compositions, feed.SpotList) + capitalizedCash;
            return value;
        }

        private void RecordState(double portfolioValue)
        {
            History.Add(new PortfolioState(Date, Compositions, Cash, portfolioValue));
        }
    }
}

