using System;

namespace GasStation
{
    public enum FuelingOutcome { Continue, Success, Overfill, Timeout, OperatingEnd }
    public static class FuelingResultEvaluator
    {
        public static FuelingOutcome Evaluate(double fuel, double target, double tolerance, double remainingTime, bool flowStopped,
            bool timedOut = false, bool overfilled = false, bool operatingEnd = false)
        {
            if (overfilled || fuel > target + tolerance + 1e-8) return FuelingOutcome.Overfill;
            if (!flowStopped) return FuelingOutcome.Continue;
            if (timedOut || remainingTime <= 1e-8) return FuelingOutcome.Timeout;
            if (operatingEnd) return FuelingOutcome.OperatingEnd;
            if (fuel >= target - tolerance - 1e-8) return FuelingOutcome.Success;
            return FuelingOutcome.Continue;
        }

        public static int Score(FuelingOutcome outcome, double error, double tolerance, double remainingTime, double timeLimit, double target)
        {
            if (outcome == FuelingOutcome.Overfill) return -80;
            if (outcome == FuelingOutcome.Timeout) return -40;
            if (outcome != FuelingOutcome.Success) return 0;
            double accuracy = Math.Max(0, 100 * (1 - error / tolerance));
            double speed = 50 * Math.Max(0, Math.Min(1, remainingTime / timeLimit));
            return (int)Math.Round((100 + accuracy + speed) * target / 35, MidpointRounding.AwayFromZero);
        }
    }
}
