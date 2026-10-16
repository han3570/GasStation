using System;

namespace GasStation
{
    // Quantize once at completion. mL * won/L is exactly milli-won.
    public sealed class FuelSaleSettlement
    {
        public const long ExcessAtCostLimitMilliliters = 5000;
        public long DeliveredMilliliters { get; }
        public long RetailMilliliters { get; }
        public long AtCostMilliliters { get; }
        public long FreeMilliliters { get; }
        public long RevenueMilliWon { get; }
        public long CostMilliWon { get; }
        public long ProfitMilliWon => RevenueMilliWon - CostMilliWon;
        public decimal RevenueWon => RevenueMilliWon / 1000m;
        public decimal CostWon => CostMilliWon / 1000m;
        public decimal ProfitWon => ProfitMilliWon / 1000m;

        public FuelSaleSettlement(VehicleFuelRequest request, double deliveredLiters)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (double.IsNaN(deliveredLiters) || double.IsInfinity(deliveredLiters) || deliveredLiters < 0)
                throw new ArgumentOutOfRangeException(nameof(deliveredLiters));
            checked
            {
                DeliveredMilliliters = (long)Math.Round((decimal)deliveredLiters * 1000m, 0, MidpointRounding.AwayFromZero);
                RetailMilliliters = Math.Min(DeliveredMilliliters, (long)request.Target * 1000);
                AtCostMilliliters = Math.Min(DeliveredMilliliters - RetailMilliliters, ExcessAtCostLimitMilliliters);
                FreeMilliliters = DeliveredMilliliters - RetailMilliliters - AtCostMilliliters;
                RevenueMilliWon = RetailMilliliters * request.SalePricePerLiter + AtCostMilliliters * request.FuelCostPerLiter;
                CostMilliWon = DeliveredMilliliters * request.FuelCostPerLiter;
            }
        }
    }
}
