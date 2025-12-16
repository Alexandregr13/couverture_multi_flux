namespace HedgingEngine.Models
{
    public class PastData
    {
        private List<List<double>> _observations;

        public PastData()
        {
            _observations = new List<List<double>>();
        }

        public void AddObservation(params double[] values)
        {
            _observations.Add(new List<double>(values));
        }

        public void AddObservation(List<double> values)
        {
            _observations.Add(new List<double>(values));
        }

        public List<List<double>> GetMatrix()
        {
            return _observations;
        }

        public int DateCount => _observations.Count;

        public int AssetCount => _observations.Count > 0 ? _observations[0].Count : 0;

        public void Print()
        {
            Console.WriteLine($"[PastData] Matrice {DateCount}x{AssetCount}:");
            for (int i = 0; i < _observations.Count; i++)
            {
                Console.WriteLine($"  Date {i}: [{string.Join(", ", _observations[i].Select(v => v.ToString("F2")))}]");
            }
        }
    }
}