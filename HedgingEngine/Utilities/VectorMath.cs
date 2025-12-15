namespace HedgingEngine.Utilities
{
    public static class VectorMath
    {
        public static double Dot(Dictionary<string, double> dictA, Dictionary<string, double> dictB)
        {
            var res = 0.0;
            foreach (var key in dictA.Keys)
            {
                if (dictB.ContainsKey(key))
                    res += dictA[key] * dictB[key];
            }
            return res;
        }

        public static Dictionary<string, double> ArrayToDict(double[] array, string[] keys)
        {
            var dict = new Dictionary<string, double>();
            for (var i = 0; i < array.Length && i < keys.Length; i++)
            {
                dict[keys[i]] = array[i];
            }
            return dict;
        }
    }
}