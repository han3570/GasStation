using System;
using UnityEngine;

namespace GasStation
{
    [Serializable]
    public class VehicleSettings
    {
        public string id;
        public string displayName;
        public int tankCapacityLiters;
        public int minRemainingFuel;
        public int maxRemainingFuel;
        public float baseSeconds;
        public float secondsPerLiter;
        public float minimumSeconds;
        public float maximumSeconds;
        public float resistance;
        public int weight;
        public Color color;
    }

    [CreateAssetMenu(menuName = "Gas Station/Prototype Settings")]
    public class PrototypeSettings : ScriptableObject
    {
        [Header("Session")]
        [Min(1)] public float operatingSeconds = 180;
        [Min(0)] public float arrivalSeconds = 0.5f;
        [Min(0)] public float feedbackSeconds = 0.4f;
        [Min(0)] public float overfillFeedbackSeconds = 1.2f;
        [Header("Pressure & fuel")]
        [Min(0.01f)] public float pressureRise = 60;
        [Min(0.01f)] public float pressureDecay = 28;
        [Min(0.001f)] public float flowCoefficient = 0.2f;
        [Min(0.01f)] public float tolerance = 5;
        [Range(0.01f, 0.5f)] public float relativeTolerance = 0.2f;
        [Min(0)] public float perfectTolerance = 1;
        [Header("Success bonuses")]
        [Range(0, 1)] public float oneTouchBonusRatio = .10f;
        [Range(0, 1)] public float perfectBonusRatio = .15f;
        [Header("Fuel sales (won per liter)")]
        [Min(0)] public int salePricePerLiter = 2000;
        [Min(0)] public int fuelCostPerLiter = 1500;
        [Header("Vehicles")]
        public VehicleSettings[] vehicles =
        {
            new VehicleSettings { id = "compact", displayName = "경차", tankCapacityLiters = 40, minRemainingFuel = 5, maxRemainingFuel = 25, baseSeconds = 8, secondsPerLiter = .17f, minimumSeconds = 11, maximumSeconds = 15, resistance = 20, weight = 40, color = new Color(1f, .73f, .25f) },
            new VehicleSettings { id = "sedan", displayName = "승용차", tankCapacityLiters = 60, minRemainingFuel = 5, maxRemainingFuel = 45, baseSeconds = 10, secondsPerLiter = .15f, minimumSeconds = 12, maximumSeconds = 19, resistance = 35, weight = 40, color = new Color(.37f, .69f, .91f) },
            new VehicleSettings { id = "suv", displayName = "SUV", tankCapacityLiters = 85, minRemainingFuel = 5, maxRemainingFuel = 65, baseSeconds = 11, secondsPerLiter = .13f, minimumSeconds = 13, maximumSeconds = 22, resistance = 48, weight = 20, color = new Color(.93f, .43f, .34f) }
        };

        public void Validate()
        {
            if (operatingSeconds <= 0 || pressureRise <= 0 || pressureDecay <= 0 || flowCoefficient <= 0 || tolerance <= 0 || relativeTolerance <= 0 || relativeTolerance >= 1)
                throw new ArgumentException("Session duration, pressure rates, flow and tolerance must be positive.");
            if (arrivalSeconds < 0 || feedbackSeconds < 0 || overfillFeedbackSeconds < 0 || perfectTolerance < 0 || vehicles == null || vehicles.Length == 0)
                throw new ArgumentException("Invalid transition time or vehicle list.");
            if (oneTouchBonusRatio < 0 || oneTouchBonusRatio > 1 || perfectBonusRatio < 0 || perfectBonusRatio > 1)
                throw new ArgumentException("Bonus ratios must be between zero and one.");
            if (salePricePerLiter < 0 || fuelCostPerLiter < 0)
                throw new ArgumentException("Fuel prices cannot be negative.");
            int totalWeight = 0;
            foreach (var vehicle in vehicles)
            {
                if (vehicle == null || vehicle.tankCapacityLiters <= 0 || vehicle.minRemainingFuel < 0 || vehicle.maxRemainingFuel < vehicle.minRemainingFuel || vehicle.maxRemainingFuel >= vehicle.tankCapacityLiters || vehicle.resistance < 0 || vehicle.resistance >= 100 || vehicle.baseSeconds < 0 || vehicle.secondsPerLiter < 0 || vehicle.minimumSeconds <= 0 || vehicle.maximumSeconds < vehicle.minimumSeconds || vehicle.weight < 0)
                    throw new ArgumentException("Invalid vehicle settings.");
                totalWeight += vehicle.weight;
            }
            if (totalWeight <= 0) throw new ArgumentException("At least one vehicle needs a positive spawn weight.");
        }
    }
}
