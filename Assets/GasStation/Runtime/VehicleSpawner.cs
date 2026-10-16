using System;

namespace GasStation
{
    // Arrival values are independent of the shared vehicle type and locked until resolution.
    public sealed class VehicleFuelRequest
    {
        public readonly VehicleSettings Type;
        public readonly int TankCapacity, InitialFuel, Target;
        public readonly int SalePricePerLiter, FuelCostPerLiter;
        public readonly double TimeLimit, Tolerance;
        public double MinimumFuel => Math.Max(0, Target - Tolerance);
        public double MaximumFuel => Target + Tolerance;

        public VehicleFuelRequest(VehicleSettings type, int initialFuel, PrototypeSettings settings)
        {
            if (initialFuel < type.minRemainingFuel || initialFuel > type.maxRemainingFuel)
                throw new ArgumentOutOfRangeException(nameof(initialFuel));
            Type = type;
            TankCapacity = type.tankCapacityLiters;
            InitialFuel = initialFuel;
            Target = TankCapacity - InitialFuel;
            SalePricePerLiter = settings.salePricePerLiter;
            FuelCostPerLiter = settings.fuelCostPerLiter;
            Tolerance = Math.Min(settings.tolerance, Math.Round(Target * (double)settings.relativeTolerance, 2));
            TimeLimit = Math.Round(Math.Max(type.minimumSeconds, Math.Min(type.maximumSeconds,
                type.baseSeconds + Target * (double)type.secondsPerLiter)), 1, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class VehicleSpawner
    {
        readonly Random random;
        readonly VehicleSettings[] vehicles;
        public VehicleSpawner(VehicleSettings[] vehicles, int seed) { this.vehicles = vehicles; random = new Random(seed); }
        public VehicleSettings Next()
        {
            int total = 0;
            foreach (var vehicle in vehicles) total += vehicle.weight;
            int roll = random.Next(total);
            foreach (var vehicle in vehicles)
            {
                roll -= vehicle.weight;
                if (roll < 0) return vehicle;
            }
            return vehicles[vehicles.Length - 1];
        }
        public VehicleFuelRequest NextRequest(PrototypeSettings settings, VehicleSettings forced = null, int? initialFuel = null)
        {
            var type = forced ?? Next();
            int fuel = initialFuel ?? random.Next(type.minRemainingFuel, type.maxRemainingFuel + 1);
            return new VehicleFuelRequest(type, fuel, settings);
        }
    }
}
