using MarketData;

namespace HedgingEngine.Pricing
{
    public class PricingParams
    {
        public List<DataFeed> DataFeeds;
        public double Time;
        public bool MonitoringDate;

        public PricingParams(List<DataFeed> data, double time, bool monitoringDate)
        {
            DataFeeds = data;
            Time = time;
            MonitoringDate = monitoringDate;
        }

        public void SetParams(List<DataFeed> data, double time, bool monitoringDate)
        {
            DataFeeds = data;
            Time = time;
            MonitoringDate = monitoringDate;
        }
    }
}
