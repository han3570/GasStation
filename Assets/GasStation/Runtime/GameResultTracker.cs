using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GasStation
{
    public sealed class FuelingCompletion
    {
        public int transactionId;
        public int pressCount;
        public double elapsed, stoppedWait, closingElapsed;
        public bool timedOut, overfilled, operatingEnd;
    }

    public sealed class FuelingRecord
    {
        public string vehicle;
        public FuelingOutcome outcome;
        public double target, fuel, elapsed, error, tolerance, timeLimit;
        public int capacity, initialFuel, seed;
        public string VolumeGroup => target <= 35 ? "small" : "large";
        public float rise, decay, coefficient;
        public int score;
        public int baseScore, oneTouchBonus, perfectBonus, pressCount;
        public bool perfect;
        public bool OneTouch => outcome == FuelingOutcome.Success && pressCount == 1;
        public int transactionId;
        public double stoppedWait, closingElapsed;
        public bool timedOut, overfilled, operatingEnd;
        public int salePricePerLiter, fuelCostPerLiter;
        public FuelSaleSettlement sale;
    }

    public sealed class GameResultTracker
    {
        public readonly List<FuelingRecord> Records = new List<FuelingRecord>();
        public int TotalScore { get; private set; }
        public int TotalPenalty { get; private set; }
        public int LastPenalty { get; private set; }
        public int SuccessCount { get; private set; }
        public int OverfillCount { get; private set; }
        public int TimeoutCount { get; private set; }
        public int ClosingCount { get; private set; }
        public int PerfectCount { get; private set; }
        public int OneTouchCount { get; private set; }
        public int TotalBonus { get; private set; }
        public int Streak { get; private set; }
        public int MaximumStreak { get; private set; }
        public long TotalRevenueMilliWon { get; private set; }
        public long TotalCostMilliWon { get; private set; }
        public long TotalProfitMilliWon => TotalRevenueMilliWon - TotalCostMilliWon;
        public decimal TotalRevenueWon => TotalRevenueMilliWon / 1000m;
        public decimal TotalCostWon => TotalCostMilliWon / 1000m;
        public decimal TotalProfitWon => TotalProfitMilliWon / 1000m;
        double totalError, totalTime;
        readonly Dictionary<int, FuelingRecord> completed = new Dictionary<int, FuelingRecord>();
        public double AverageError => Records.Count == 0 ? 0 : totalError / Records.Count;
        public double AverageTime => Records.Count == 0 ? 0 : totalTime / Records.Count;

        public FuelingRecord Add(VehicleFuelRequest request, FuelingOutcome outcome, double fuel, double remaining, PrototypeSettings settings, int seed = 0,
            FuelingCompletion completion = null)
        {
            if (completion != null && completion.transactionId > 0 && completed.TryGetValue(completion.transactionId, out var previous)) return previous;
            var record = new FuelingRecord { vehicle = request.Type.id, outcome = outcome, target = request.Target, fuel = fuel,
                capacity = request.TankCapacity, initialFuel = request.InitialFuel, tolerance = request.Tolerance, timeLimit = request.TimeLimit, seed = seed,
                elapsed = completion?.elapsed ?? request.TimeLimit - remaining, error = Math.Abs(fuel - request.Target),
                transactionId = completion?.transactionId ?? 0, stoppedWait = completion?.stoppedWait ?? 0, closingElapsed = completion?.closingElapsed ?? 0,
                pressCount = completion?.pressCount ?? 0,
                timedOut = (completion?.timedOut ?? false) || outcome == FuelingOutcome.Timeout,
                overfilled = (completion?.overfilled ?? false) || outcome == FuelingOutcome.Overfill,
                operatingEnd = (completion?.operatingEnd ?? false) || outcome == FuelingOutcome.OperatingEnd,
                rise = settings.pressureRise, decay = settings.pressureDecay, coefficient = settings.flowCoefficient,
                salePricePerLiter = request.SalePricePerLiter, fuelCostPerLiter = request.FuelCostPerLiter,
                sale = new FuelSaleSettlement(request, fuel) };
            record.baseScore = FuelingResultEvaluator.Score(outcome, record.error, request.Tolerance, remaining, request.TimeLimit, request.Target);
            record.perfect = outcome == FuelingOutcome.Success && record.error <= settings.perfectTolerance + 1e-8;
            // Each bonus uses the original success score; neither compounds on the other.
            if (record.OneTouch) record.oneTouchBonus = (int)Math.Round(record.baseScore * settings.oneTouchBonusRatio, MidpointRounding.AwayFromZero);
            if (record.perfect) record.perfectBonus = (int)Math.Round(record.baseScore * settings.perfectBonusRatio, MidpointRounding.AwayFromZero);
            record.score = record.baseScore + record.oneTouchBonus + record.perfectBonus;
            TotalBonus += record.oneTouchBonus + record.perfectBonus;
            TotalScore += record.score;
            TotalRevenueMilliWon += record.sale.RevenueMilliWon;
            TotalCostMilliWon += record.sale.CostMilliWon;
            if (record.score < 0) { LastPenalty = -record.score; TotalPenalty += LastPenalty; }
            totalError += record.error;
            totalTime += record.elapsed;
            if (outcome == FuelingOutcome.Success)
            {
                SuccessCount++;
                if (record.perfect) PerfectCount++;
                if (record.OneTouch) OneTouchCount++;
                Streak++;
                MaximumStreak = Math.Max(MaximumStreak, Streak);
            }
            else
            {
                Streak = 0;
            }
            if (record.overfilled) OverfillCount++;
            if (record.timedOut) TimeoutCount++;
            if (record.operatingEnd) ClosingCount++;
            Records.Add(record);
            if (record.transactionId > 0) completed.Add(record.transactionId, record);
            return record;
        }

        public int CountVolumeGroup(bool small)
        {
            int count = 0;
            foreach (var record in Records) if ((record.target <= 35) == small) count++;
            return count;
        }
        public double AverageVolumeTime(bool small)
        {
            double total = 0; int count = 0;
            foreach (var record in Records) if ((record.target <= 35) == small) { total += record.elapsed; count++; }
            return count == 0 ? 0 : total / count;
        }

        public string ToCsv(PrototypeSettings settings)
        {
            var c = CultureInfo.InvariantCulture;
            var csv = new StringBuilder("vehicle,outcome,target_liters,final_liters,error_liters,elapsed_seconds,score,rise_at_resolution,decay_at_resolution,coefficient_at_resolution,tank_capacity_liters,initial_fuel_liters,total_fuel_liters,tolerance_liters,time_limit_seconds,volume_group,seed,transaction_id,timed_out,overfilled,operating_end,flow_stopped_wait_seconds,closing_elapsed_seconds,press_count,base_score,one_touch_bonus,perfect_bonus,sale_price_won_per_liter,cost_won_per_liter,delivered_ml,retail_ml,at_cost_ml,free_ml,revenue_milliwon,cost_milliwon,profit_milliwon,revenue_won,cost_won,profit_won\n");
            foreach (var r in Records)
            {
                csv.AppendFormat(c, "{0},{1},{2:F2},{3:F4},{4:F4},{5:F4},{6},{7},{8},{9},{10},{11},{12:F4},{13:F2},{14:F1},{15},{16},{17},{18},{19},{20},{21:F4},{22:F4},{23},{24},{25},{26},",
                    r.vehicle, r.outcome, r.target, r.fuel, r.error, r.elapsed, r.score, r.rise, r.decay, r.coefficient,
                    r.capacity, r.initialFuel, r.initialFuel + r.fuel, r.tolerance, r.timeLimit, r.VolumeGroup, r.seed,
                    r.transactionId, r.timedOut, r.overfilled, r.operatingEnd, r.stoppedWait, r.closingElapsed,
                    r.pressCount, r.baseScore, r.oneTouchBonus, r.perfectBonus);
                csv.AppendFormat(c, "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9:0.###},{10:0.###},{11:0.###}\n",
                    r.salePricePerLiter, r.fuelCostPerLiter, r.sale.DeliveredMilliliters, r.sale.RetailMilliliters,
                    r.sale.AtCostMilliliters, r.sale.FreeMilliliters, r.sale.RevenueMilliWon, r.sale.CostMilliWon,
                    r.sale.ProfitMilliWon, r.sale.RevenueWon, r.sale.CostWon, r.sale.ProfitWon);
            }
            return csv.ToString();
        }
    }
}
