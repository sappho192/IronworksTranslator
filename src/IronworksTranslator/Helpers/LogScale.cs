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
            // Adjust this method based on how you've modified Scale()
            // This might involve applying the inverse of the transformations applied in Scale()
            var inverseLogScaledValue = Math.Exp(value / (_max / _min));
            return inverseLogScaledValue * _min;
        }
    }
}
