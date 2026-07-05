namespace IronworksTranslator.Helpers
{
    public interface ILogScale
    {
        double Scale(double value);
        double Invert(double value);
    }

    public class LogScale(double min, double max) : ILogScale
    {
        private readonly double _min = min;
        private readonly double _max = max;

        // Adjusted Scale method to slow down changes at larger numbers
        public double Scale(double value)
        {
            // Apply logarithmic scaling
            var logScaledValue = Math.Log(value / _min, _max / _min);

            // Apply inverse transformation to slow down changes at larger numbers
            return Math.Pow(logScaledValue, 2); // Example power function, adjust exponent as needed
        }

        public double Invert(double value)
        {
            var normalizedValue = Math.Sqrt(value);
            return _min * Math.Pow(_max / _min, normalizedValue);
        }
    }
}
