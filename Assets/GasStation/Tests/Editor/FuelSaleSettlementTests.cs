using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class FuelSaleSettlementTests
    {
        PrototypeSettings settings;
        [SetUp] public void SetUp() => settings=ScriptableObject.CreateInstance<PrototypeSettings>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(settings);
        VehicleFuelRequest Request() => new VehicleFuelRequest(settings.vehicles[0],5,settings);

        [TestCase(0,0,0,0,0,0,0)]
        [TestCase(10,10000,0,0,20000,15000,5000)]
        [TestCase(35,35000,0,0,70000,52500,17500)]
        [TestCase(37,35000,2000,0,73000,55500,17500)]
        [TestCase(40,35000,5000,0,77500,60000,17500)]
        [TestCase(41,35000,5000,1000,77500,61500,16000)]
        public void SettlementSplitsRetailAtCostAndFreeFuel(double liters,long retail,long atCost,long free,long revenue,long cost,long profit)
        {
            var sale=new FuelSaleSettlement(Request(),liters);
            Assert.That(sale.RetailMilliliters,Is.EqualTo(retail));
            Assert.That(sale.AtCostMilliliters,Is.EqualTo(atCost));
            Assert.That(sale.FreeMilliliters,Is.EqualTo(free));
            Assert.That(sale.DeliveredMilliliters,Is.EqualTo(retail+atCost+free));
            Assert.That(sale.RevenueWon,Is.EqualTo(revenue));
            Assert.That(sale.CostWon,Is.EqualTo(cost));
            Assert.That(sale.ProfitWon,Is.EqualTo(profit));
        }
        [TestCase(.00049,0)] [TestCase(.0005,1)] [TestCase(.001,1)] [TestCase(35.0005,35001)]
        public void FinalVolumeRoundsOnceToTheNearestMilliliter(double liters,long ml)
        {
            var sale=new FuelSaleSettlement(Request(),liters);
            Assert.That(sale.DeliveredMilliliters,Is.EqualTo(ml));
            Assert.That(sale.CostMilliWon,Is.EqualTo(ml*1500));
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)]
        public void InvalidDeliveredVolumesAreRejected(double liters)
            => Assert.Throws<ArgumentOutOfRangeException>(()=>new FuelSaleSettlement(Request(),liters));

        [Test] public void PricesAreLockedOnArrivalAndNewCustomersUseTheNewPrice()
        {
            var request=Request(); settings.salePricePerLiter=3000; settings.fuelCostPerLiter=1800;
            var oldSale=new FuelSaleSettlement(request,35);
            var newSale=new FuelSaleSettlement(Request(),35);
            Assert.That(oldSale.RevenueWon,Is.EqualTo(70000)); Assert.That(oldSale.CostWon,Is.EqualTo(52500));
            Assert.That(newSale.RevenueWon,Is.EqualTo(105000)); Assert.That(newSale.CostWon,Is.EqualTo(63000));
        }
        [TestCase(FuelingOutcome.Success)] [TestCase(FuelingOutcome.Timeout)] [TestCase(FuelingOutcome.Overfill)]
        public void MoneyUsesDeliveredFuelRegardlessOfTheScoreOutcome(FuelingOutcome outcome)
        {
            var tracker=new GameResultTracker();
            var record=tracker.Add(Request(),outcome,32,1,settings);
            Assert.That(record.sale.RevenueWon,Is.EqualTo(64000));
            Assert.That(tracker.TotalCostWon,Is.EqualTo(48000));
            Assert.That(tracker.TotalProfitWon,Is.EqualTo(16000));
            if(outcome!=FuelingOutcome.Success) Assert.That(record.score,Is.LessThan(0));
        }
        [Test] public void DuplicateTransactionNeverChargesOrCountsTheCustomerTwice()
        {
            var tracker=new GameResultTracker(); var request=Request();
            var completion=new FuelingCompletion {transactionId=5,pressCount=1};
            var first=tracker.Add(request,FuelingOutcome.Success,35,4,settings,42,completion);
            var duplicate=tracker.Add(request,FuelingOutcome.Timeout,10,0,settings,42,completion);
            Assert.That(duplicate,Is.SameAs(first)); Assert.That(tracker.Records.Count,Is.EqualTo(1));
            Assert.That(tracker.TotalRevenueWon,Is.EqualTo(70000));
            Assert.That(tracker.TotalCostWon,Is.EqualTo(52500));
            Assert.That(tracker.TotalProfitWon,Is.EqualTo(17500));
            Assert.That(tracker.TotalScore,Is.EqualTo(first.score));
        }
        [Test] public void FractionalWonDoNotAccumulateFloatingPointError()
        {
            var tracker=new GameResultTracker(); var request=Request();
            for(int i=0;i<10000;i++) tracker.Add(request,FuelingOutcome.Timeout,.001,0,settings);
            Assert.That(tracker.TotalRevenueMilliWon,Is.EqualTo(20000000));
            Assert.That(tracker.TotalCostMilliWon,Is.EqualTo(15000000));
            Assert.That(tracker.TotalProfitMilliWon,Is.EqualTo(5000000));
        }
        [Test] public void CsvStoresExactMoneyAndArrivalPricesUsingInvariantNumbers()
        {
            var tracker=new GameResultTracker(); tracker.Add(Request(),FuelingOutcome.Success,35.001,4,settings);
            var previous=CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
                string[] rows=tracker.ToCsv(settings).Trim().Split('\n');
                string[] columns=rows[0].Split(','), values=rows[1].Split(',');
                Assert.That(values.Length,Is.EqualTo(columns.Length));
                Assert.That(values[Array.IndexOf(columns,"revenue_won")],Is.EqualTo("70001.5"));
                Assert.That(values[Array.IndexOf(columns,"cost_milliwon")],Is.EqualTo("52501500"));
                Assert.That(values[Array.IndexOf(columns,"sale_price_won_per_liter")],Is.EqualTo("2000"));
            }
            finally { CultureInfo.CurrentCulture=previous; }
        }
        [Test] public void RetryingUnderfillDefersMoneyUntilTheSingleFinalSettlement()
        {
            var game=new FuelingSimulation(settings); game.Start(42); game.ForceVehicle(settings.vehicles[0],5);
            game.SetHeld(true); game.Advance(.6); game.SetHeld(false); game.Advance(1);
            Assert.That(game.Fuel,Is.GreaterThan(0)); Assert.That(game.Results.TotalRevenueMilliWon,Is.Zero);
            game.SetHeld(true);
            while(game.CanAcceptInput && game.PredictedFinal<35) game.Advance(1d/240);
            game.SetHeld(false); game.Advance((game.Pressure.Value-20)/settings.pressureDecay);
            Assert.That(game.Results.Records.Count,Is.EqualTo(1)); Assert.That(game.LastRecord.pressCount,Is.EqualTo(2));
            Assert.That(game.Results.TotalProfitMilliWon,Is.EqualTo(game.LastRecord.sale.ProfitMilliWon));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void MoneyIsIndependentOfFrameRateAndIncludesClosingCustomer(int fps)
        {
            settings.operatingSeconds=1;
            var game=new FuelingSimulation(settings); game.Start(42); game.ForceVehicle(settings.vehicles[0],5);
            game.SetHeld(true);
            for(double held=0;held<1.75-1e-9;) { double step=Math.Min(1d/fps,1.75-held); game.Advance(step); held+=step; }
            game.SetHeld(false);
            while(game.State!=SessionState.Result) game.Advance(1d/fps);
            Assert.That(game.Results.Records.Count,Is.EqualTo(1)); Assert.That(game.LastRecord.operatingEnd,Is.True);
            Assert.That(game.LastRecord.sale.DeliveredMilliliters,Is.EqualTo(34857));
            Assert.That(game.Results.TotalRevenueWon,Is.EqualTo(69714));
            Assert.That(game.Results.TotalCostWon,Is.EqualTo(52285.5m));
            Assert.That(game.Results.TotalProfitWon,Is.EqualTo(17428.5m));
            game.Start(42);
            Assert.That(game.Results.TotalRevenueMilliWon,Is.Zero); Assert.That(game.Results.TotalCostMilliWon,Is.Zero);
            Assert.That(game.Results.TotalProfitMilliWon,Is.Zero);
        }
        [Test] public void FullShiftTotalsEqualTheExactSumOfEveryCompletedSale()
        {
            var game=new FuelingSimulation(settings); game.Start(12345);
            while(game.State!=SessionState.Result)
            {
                if(game.CanAcceptInput) game.SetHeld(game.PredictedFinal<game.Request.Target);
                game.Advance(1d/120);
            }
            Assert.That(game.Results.TotalScore,Is.EqualTo(8939),"Adding money must preserve the existing score and bonuses.");
            Assert.That(game.Results.TotalRevenueMilliWon,Is.EqualTo(game.Results.Records.Sum(r=>r.sale.RevenueMilliWon)));
            Assert.That(game.Results.TotalCostMilliWon,Is.EqualTo(game.Results.Records.Sum(r=>r.sale.CostMilliWon)));
            Assert.That(game.Results.TotalProfitMilliWon,Is.EqualTo(game.Results.Records.Sum(r=>r.sale.ProfitMilliWon)));
        }
        [Test] public void NegativePriceSettingsAreRejected()
        {
            settings.salePricePerLiter=-1; Assert.Throws<ArgumentException>(()=>settings.Validate());
            settings.salePricePerLiter=2000; settings.fuelCostPerLiter=-1;
            Assert.Throws<ArgumentException>(()=>settings.Validate());
        }
    }
}
