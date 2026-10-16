using System;

namespace GasStation
{
    // Exact integration across resistance and pressure clamp boundaries.
    // The trapezoid is integrated only over the time during which fuel flows.
    public sealed class PressureController
    {
        public double Value { get; private set; }
        public void Reset() => Value = 0;

        public double Advance(double seconds, bool held, double rise, double decay, double resistance, double coefficient)
        {
            double fuel = Integrate(Value, seconds, held, rise, decay, resistance, coefficient, out double end);
            Value = end;
            return fuel;
        }

        public double PredictFuel(double seconds, bool held, double rise, double decay, double resistance, double coefficient)
            => Integrate(Value, seconds, held, rise, decay, resistance, coefficient, out _);

        static double Integrate(double start, double seconds, bool held, double rise, double decay, double resistance, double coefficient, out double end)
        {
            end = start;
            if (seconds <= 0) return 0;
            double rate = held ? rise : -decay;
            double limit = held ? 100 : 0;
            double boundaryTime = Math.Max(0, (limit - start) / rate);
            double movingTime = Math.Min(seconds, boundaryTime);
            // Correct sub-picosecond roundoff at the natural clamp boundary.
            end = boundaryTime - movingTime <= 1e-12 ? limit : Math.Max(0, Math.Min(100, start + rate * movingTime));
            double activeStart = 0;
            double activeEnd = movingTime;
            if (held) activeStart = Math.Min(movingTime, Math.Max(0, (resistance - start) / rate));
            else activeEnd = Math.Min(movingTime, Math.Max(0, (resistance - start) / rate));
            double fuel = 0;
            if (activeEnd > activeStart)
            {
                double a = Math.Max(0, start + rate * activeStart - resistance);
                double b = Math.Max(0, start + rate * activeEnd - resistance);
                fuel = (a + b) * .5 * (activeEnd - activeStart) * coefficient;
            }
            fuel += Math.Max(0, end - resistance) * (seconds - movingTime) * coefficient;
            return fuel;
        }

        public double RemainingFuel(double resistance, double coefficient, double decay)
        {
            double effective = Math.Max(0, Value - resistance);
            return coefficient * effective * effective / (2 * decay);
        }
    }
}
