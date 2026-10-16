using System;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class FuelingSimulationTests
    {
        PrototypeSettings settings;
        [SetUp] public void SetUp() => settings=ScriptableObject.CreateInstance<PrototypeSettings>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(settings);
        FuelingSimulation Create()
        {
            var game=new FuelingSimulation(settings); game.Start(42); game.ForceVehicle(settings.vehicles[0],5); return game;
        }
        [Test] public void PressureClampAndResistanceCrossingAreIntegratedExactly()
        {
            var p=new PressureController();
            double liters=p.Advance(3,true,60,28,20,.2);
            Assert.That(p.Value,Is.EqualTo(100));
            Assert.That(liters,Is.EqualTo(.2*80*80/(2*60)+16*(3-100d/60)).Within(1e-9));
        }
        [TestCase(18)] [TestCase(28)] [TestCase(40)]
        public void ReleasedFuelMatchesPrediction(double decay)
        {
            var p=new PressureController(); p.Advance(2,true,60,decay,35,.2);
            double prediction=p.RemainingFuel(35,.2,decay);
            double actual=p.Advance(20,false,60,decay,35,.2);
            Assert.That(actual,Is.EqualTo(prediction).Within(1e-9));
            Assert.That(p.Value,Is.Zero);
        }
        [Test] public void IntegrationHasNoFrameRateDrift()
        {
            var a=new PressureController(); var b=new PressureController(); double fuelA=0,fuelB=0;
            for(int i=0;i<90;i++) fuelA+=a.Advance(1d/30,true,60,28,48,.2);
            for(int i=0;i<360;i++) fuelB+=b.Advance(1d/120,true,60,28,48,.2);
            for(int i=0;i<120;i++) fuelA+=a.Advance(1d/30,false,60,28,48,.2);
            for(int i=0;i<480;i++) fuelB+=b.Advance(1d/120,false,60,28,48,.2);
            Assert.That(fuelA,Is.EqualTo(fuelB).Within(1e-8));
        }
        [Test] public void UnderfillCanResumeAndDoesNotResolveAtZeroPressure()
        {
            var game=Create(); game.SetHeld(true); game.Advance(.6); game.SetHeld(false); game.Advance(2);
            Assert.That(game.State,Is.EqualTo(SessionState.UnderfillWait)); Assert.That(game.Results.Records.Count,Is.Zero);
            double before=game.Fuel; game.SetHeld(true); game.Advance(1);
            Assert.That(game.Fuel,Is.GreaterThan(before));
        }
        [Test] public void TargetRangeDoesNotAutoCompleteWhileFlowing()
        {
            var game=Create(); game.SetHeld(true); game.Advance(2.95);
            Assert.That(game.Fuel,Is.InRange(30d,40d));
            Assert.That(game.State,Is.EqualTo(SessionState.Fueling));
        }
        [Test] public void SuccessfulReleaseWaitsForFlowToStop()
        {
            var game=Create(); game.SetHeld(true);
            while(game.PredictedFinal<35) game.Advance(1d/240);
            game.SetHeld(false); game.Advance(.1);
            Assert.That(game.State,Is.EqualTo(SessionState.Fueling));
            game.Advance(4);
            Assert.That(game.Results.SuccessCount,Is.EqualTo(1));
            Assert.That(game.Results.Records[0].fuel,Is.EqualTo(35).Within(.1));
        }
        [Test] public void OverfillImmediatelyStopsDeliveryAndAppliesThePenaltyOnce()
        {
            var game=Create(); game.SetHeld(true); game.Advance(3.6);
            Assert.That(game.State,Is.EqualTo(SessionState.Feedback)); Assert.That(game.Overfilled,Is.True);
            Assert.That(game.Results.Records.Count,Is.EqualTo(1)); Assert.That(game.Pressure.Value,Is.GreaterThan(game.Vehicle.resistance));
            double expected=game.Fuel;
            game.SetHeld(true); Assert.That(game.Held,Is.False);
            game.Advance(.3);
            Assert.That(game.Results.OverfillCount,Is.EqualTo(1)); Assert.That(game.Results.TotalScore,Is.EqualTo(-80));
            Assert.That(game.LastRecord.fuel,Is.EqualTo(expected).Within(1e-7));
            Assert.That(game.Fuel,Is.EqualTo(expected)); Assert.That(game.Flow,Is.Zero);
            Assert.That(game.Results.TotalPenalty,Is.EqualTo(80));
            Assert.That(game.LastRecord.elapsed,Is.EqualTo(3.5).Within(1e-6));
        }
        [Test] public void TimeoutResolvesAndTransitionConsumesOperatingTime()
        {
            var game=Create(); double elapsed=game.Request.TimeLimit+.5; game.Advance(elapsed);
            Assert.That(game.Results.TimeoutCount,Is.EqualTo(1)); Assert.That(game.Results.TotalScore,Is.EqualTo(-40));
            Assert.That(game.OperatingRemaining,Is.EqualTo(180-elapsed).Within(1e-7)); Assert.That(game.State,Is.EqualTo(SessionState.Arrival));
            game.Advance(.41); Assert.That(game.State,Is.EqualTo(SessionState.Fueling));
            Assert.That(game.Pressure.Value,Is.Zero); Assert.That(game.Held,Is.False);
        }
        [Test] public void PauseFreezesAllTimersAndReleasesInput()
        {
            var game=Create(); game.SetHeld(true); game.Advance(.5); double p=game.Pressure.Value,t=game.OperatingRemaining,f=game.Fuel;
            game.SetPaused(true); game.Advance(30);
            Assert.That(game.Held,Is.False); Assert.That(game.Pressure.Value,Is.EqualTo(p));
            Assert.That(game.OperatingRemaining,Is.EqualTo(t)); Assert.That(game.Fuel,Is.EqualTo(f));
        }
        [Test] public void ClosingAtZeroPressureKeepsTheCurrentCustomerAvailable()
        {
            settings.operatingSeconds=1; var game=Create(); game.Advance(2);
            Assert.That(game.OperatingEnded,Is.True); Assert.That(game.CanAcceptInput,Is.True);
            Assert.That(game.Results.Records,Is.Empty); Assert.That(game.VehicleRemaining,Is.EqualTo(12).Within(1e-7));
            game.SetHeld(true); game.Advance(.5);
            Assert.That(game.Held,Is.True); Assert.That(game.PressCount,Is.EqualTo(1));
        }
        [Test] public void OverfillIsImmediateAndOtherOutcomesWaitForFlowStop()
        {
            Assert.That(FuelingResultEvaluator.Evaluate(41,35,5,0,true),Is.EqualTo(FuelingOutcome.Overfill));
            Assert.That(FuelingResultEvaluator.Evaluate(35,35,5,0,true),Is.EqualTo(FuelingOutcome.Timeout));
            Assert.That(FuelingResultEvaluator.Evaluate(30,35,5,1,true),Is.EqualTo(FuelingOutcome.Success));
            Assert.That(FuelingResultEvaluator.Evaluate(40,35,5,1,true),Is.EqualTo(FuelingOutcome.Success));
            Assert.That(FuelingResultEvaluator.Evaluate(35,35,5,1,true,operatingEnd:true),Is.EqualTo(FuelingOutcome.OperatingEnd));
            Assert.That(FuelingResultEvaluator.Evaluate(41,35,5,0,false),Is.EqualTo(FuelingOutcome.Overfill));
            Assert.That(FuelingResultEvaluator.Evaluate(35,35,5,0,false),Is.EqualTo(FuelingOutcome.Continue));
        }
        [Test] public void ScoringAndMetricsTrackSuccessfulAndFailedVehicles()
        {
            var results=new GameResultTracker(); var car=new VehicleFuelRequest(settings.vehicles[0],5,settings);
            results.Add(car,FuelingOutcome.Success,35,4,settings); results.Add(car,FuelingOutcome.Success,35.5,3,settings);
            results.Add(car,FuelingOutcome.Overfill,41,2,settings);
            Assert.That(results.PerfectCount,Is.EqualTo(2)); Assert.That(results.MaximumStreak,Is.EqualTo(2)); Assert.That(results.Streak,Is.Zero);
            Assert.That(results.TotalScore,Is.EqualTo(214+32+201+30-80));
            Assert.That(results.TotalPenalty,Is.EqualTo(80));
            Assert.That(results.AverageError,Is.EqualTo(6.5/3).Within(1e-8));
            StringAssert.Contains("compact,Success",results.ToCsv(settings));
        }
        [Test] public void SeededWeightedSpawnerRespectsConfiguredDistribution()
        {
            var spawner=new VehicleSpawner(settings.vehicles,123); int[] counts=new int[3];
            for(int i=0;i<10000;i++) counts[Array.IndexOf(settings.vehicles,spawner.Next())]++;
            Assert.That(counts[0],Is.InRange(3800,4200)); Assert.That(counts[1],Is.InRange(3800,4200)); Assert.That(counts[2],Is.InRange(1800,2200));
        }
        [TestCase(18)] [TestCase(28)] [TestCase(40)]
        public void EveryVehicleCanAchievePerfectWithAllDecayVariants(float decay)
        {
            settings.pressureDecay=decay;
            foreach(var car in settings.vehicles)
            {
                // Cover every integer remaining-fuel value, including both endpoints.
                for(int initial=car.minRemainingFuel;initial<=car.maxRemainingFuel;initial++)
                {
                    var game=Create(); game.ForceVehicle(car,initial); game.SetHeld(true);
                    while(game.State==SessionState.Fueling && game.PredictedFinal<game.Request.Target) game.Advance(1d/240);
                    game.SetHeld(false);
                    while(game.IsActiveCustomer) game.Advance(1d/240);
                    Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success),$"{car.id}, initial={initial}, decay={decay}");
                    Assert.That(game.LastRecord.error,Is.LessThan(1),$"{car.id}, initial={initial}");
                }
            }
        }
        [Test] public void WholeSessionCanFinishWithAllMetrics()
        {
            var game=Create();
            while(game.State!=SessionState.Result)
            {
                if(game.CanAcceptInput) game.SetHeld(game.PredictedFinal<game.Request.Target);
                game.Advance(1d/120);
            }
            Assert.That(game.Results.Records.Count,Is.GreaterThan(10));
            Assert.That(game.Results.OverfillCount,Is.Zero); Assert.That(game.Results.TimeoutCount,Is.Zero);
            Assert.That(game.Results.PerfectCount,Is.EqualTo(game.Results.SuccessCount));
        }

        [TestCase(1,10,50,45,55,17.5)]
        [TestCase(1,30,30,25,35,14.5)]
        [TestCase(0,25,15,12,18,11)]
        [TestCase(2,65,20,16,24,13.6)]
        public void ArrivalCalculatesTargetRangeAndRoundedDeadline(int type,int initial,int target,double minimum,double maximum,double time)
        {
            var request=new VehicleFuelRequest(settings.vehicles[type],initial,settings);
            Assert.That(request.Target,Is.EqualTo(target));
            Assert.That(request.MinimumFuel,Is.EqualTo(minimum));
            Assert.That(request.MaximumFuel,Is.EqualTo(maximum));
            Assert.That(request.TimeLimit,Is.EqualTo(time).Within(1e-8));
        }
        [Test] public void SeededArrivalSequenceRepeatsAndCoversInclusiveRemainingFuelRanges()
        {
            var a=new VehicleSpawner(settings.vehicles,12345); var b=new VehicleSpawner(settings.vehicles,12345);
            var seen=new System.Collections.Generic.HashSet<int>[3]; int[] counts=new int[3];
            for(int t=0;t<3;t++) seen[t]=new System.Collections.Generic.HashSet<int>();
            bool repeated=false; VehicleFuelRequest previous=null;
            for(int i=0;i<10000;i++)
            {
                var x=a.NextRequest(settings); var y=b.NextRequest(settings);
                Assert.That(y.Type.id,Is.EqualTo(x.Type.id)); Assert.That(y.InitialFuel,Is.EqualTo(x.InitialFuel));
                int t=Array.IndexOf(settings.vehicles,x.Type); counts[t]++; seen[t].Add(x.InitialFuel);
                Assert.That(x.InitialFuel,Is.InRange(x.Type.minRemainingFuel,x.Type.maxRemainingFuel));
                repeated |= previous!=null && previous.Type==x.Type && previous.InitialFuel==x.InitialFuel;
                previous=x;
            }
            for(int t=0;t<3;t++) Assert.That(seen[t].Count,Is.EqualTo(settings.vehicles[t].maxRemainingFuel-settings.vehicles[t].minRemainingFuel+1));
            Assert.That(counts[0],Is.InRange(3800,4200)); Assert.That(counts[1],Is.InRange(3800,4200)); Assert.That(counts[2],Is.InRange(1800,2200));
            Assert.That(repeated,Is.True,"Repeated values must be allowed.");
        }
        [Test] public void ArrivalLocksTargetAndDeadlineAndResetsFuelOnReplacement()
        {
            var game=Create(); var request=game.Request;
            game.SetHeld(true); game.Advance(1);
            settings.vehicles[0].tankCapacityLiters=50; settings.vehicles[0].baseSeconds=10; settings.tolerance=4;
            Assert.That(request.Target,Is.EqualTo(35)); Assert.That(request.TimeLimit,Is.EqualTo(14));
            Assert.That(request.Tolerance,Is.EqualTo(5)); Assert.That(game.Fuel,Is.GreaterThan(0));
            game.ForceVehicle(settings.vehicles[1],30);
            Assert.That(game.Request.Target,Is.EqualTo(30)); Assert.That(game.Fuel,Is.Zero);
            Assert.That(game.Pressure.Value,Is.Zero); Assert.That(game.Held,Is.False);
            Assert.That(game.Results.Records,Is.Empty);
        }
        [Test] public void SmallTargetKeepsItsToleranceButOverfillResolvesWhileFlowing()
        {
            var game=Create(); game.ForceVehicle(settings.vehicles[0],25);
            var request=game.Request;
            Assert.That(FuelingResultEvaluator.Evaluate(11.99,request.Target,request.Tolerance,1,true),Is.EqualTo(FuelingOutcome.Continue));
            Assert.That(FuelingResultEvaluator.Evaluate(12,request.Target,request.Tolerance,1,true),Is.EqualTo(FuelingOutcome.Success));
            Assert.That(FuelingResultEvaluator.Evaluate(18,request.Target,request.Tolerance,1,true),Is.EqualTo(FuelingOutcome.Success));
            Assert.That(FuelingResultEvaluator.Evaluate(18.01,request.Target,request.Tolerance,1,false),Is.EqualTo(FuelingOutcome.Overfill));
            Assert.That(FuelingResultEvaluator.Evaluate(18.01,request.Target,request.Tolerance,1,true),Is.EqualTo(FuelingOutcome.Overfill));
            game.SetHeld(true); game.Advance(2.3);
            Assert.That(game.State,Is.EqualTo(SessionState.Feedback)); Assert.That(game.Results.Records.Count,Is.EqualTo(1));
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Overfill));
        }
        [Test] public void SpeedScoreUsesDeadlineRatioAndRewardsVolumeWithoutExceedingBonusCap()
        {
            int small=FuelingResultEvaluator.Score(FuelingOutcome.Success,0,3,5.5,11,15);
            int large=FuelingResultEvaluator.Score(FuelingOutcome.Success,0,5,11,22,70);
            Assert.That(small,Is.EqualTo(96)); Assert.That(large,Is.EqualTo(450));
            Assert.That(FuelingResultEvaluator.Score(FuelingOutcome.Success,0,5,7,14,35),Is.EqualTo(225));
            Assert.That(FuelingResultEvaluator.Score(FuelingOutcome.Success,0,5,14,28,35),Is.EqualTo(225));
            Assert.That(FuelingResultEvaluator.Score(FuelingOutcome.Success,0,5,100,14,35),Is.EqualTo(250));
        }
        [Test] public void CsvAndVolumeMetricsPreserveArrivalDataAndElapsedTime()
        {
            var r=new GameResultTracker();
            r.Add(new VehicleFuelRequest(settings.vehicles[0],25,settings),FuelingOutcome.Success,15,7,settings,42);
            r.Add(new VehicleFuelRequest(settings.vehicles[1],10,settings),FuelingOutcome.Success,50,9.5,settings,42);
            Assert.That(r.AverageVolumeTime(true),Is.EqualTo(4)); Assert.That(r.AverageVolumeTime(false),Is.EqualTo(8));
            Assert.That(r.CountVolumeGroup(true),Is.EqualTo(1)); Assert.That(r.CountVolumeGroup(false),Is.EqualTo(1));
            string csv=r.ToCsv(settings); StringAssert.Contains("initial_fuel_liters",csv); StringAssert.Contains("time_limit_seconds",csv);
            StringAssert.Contains("40,25,40.0000,3.00,11.0,small,42",csv);
            StringAssert.Contains("60,10,60.0000,5.00,17.5,large,42",csv);
        }
        [Test] public void InvalidRemainingFuelAndTimeSettingsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new VehicleFuelRequest(settings.vehicles[0],26,settings));
            settings.vehicles[0].maxRemainingFuel=40;
            Assert.Throws<ArgumentException>(()=>settings.Validate());
            settings.vehicles[0].maxRemainingFuel=25; settings.vehicles[0].maximumSeconds=10;
            Assert.Throws<ArgumentException>(()=>settings.Validate());
        }
        [Test] public void DynamicDeadlineClampsAtBothEnds()
        {
            var car=settings.vehicles[0]; car.baseSeconds=0; car.secondsPerLiter=.01f;
            Assert.That(new VehicleFuelRequest(car,25,settings).TimeLimit,Is.EqualTo(11));
            car.secondsPerLiter=10;
            Assert.That(new VehicleFuelRequest(car,5,settings).TimeLimit,Is.EqualTo(15));
        }

        void HoldUntilPredictedTarget(FuelingSimulation game)
        {
            game.SetHeld(true);
            while(game.CanAcceptInput && game.PredictedFinal<game.Request.Target) game.Advance(1d/240);
            Assert.That(game.CanAcceptInput,Is.True,"Must reach the release point before timeout or overfill.");
            game.SetHeld(false);
        }
        [Test] public void FlowStopResolvesImmediatelyAtResistanceWithoutAnIdleTail()
        {
            var game=Create(); game.ForceVehicle(settings.vehicles[1],10);
            HoldUntilPredictedTarget(game);
            game.Advance((game.Pressure.Value-game.Vehicle.resistance)/settings.pressureDecay);
            Assert.That(game.Flow,Is.Zero.Within(1e-8)); Assert.That(game.Results.Records.Count,Is.EqualTo(1));
            Assert.That(game.Pressure.Value,Is.EqualTo(35).Within(1e-7));
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success));
            Assert.That(game.LastRecord.stoppedWait,Is.Zero); Assert.That(game.DecayRemaining,Is.Zero);
            game.SetHeld(true); Assert.That(game.Held,Is.False);
            game.Advance(.1); Assert.That(game.Results.Records.Count,Is.EqualTo(1));
        }
        [Test] public void NormalDecayCanBeInterruptedUntilAnEndingIsLatched()
        {
            var game=Create(); game.SetHeld(true); game.Advance(.8); game.SetHeld(false); game.Advance(.2);
            double pressure=game.Pressure.Value; game.SetHeld(true); game.Advance(.1);
            Assert.That(game.Pressure.Value,Is.GreaterThan(pressure)); Assert.That(game.Results.Records,Is.Empty);
        }
        [Test] public void TimeoutStaysLatchedWhenResidualFuelReachesTheTarget()
        {
            settings.vehicles[1].minimumSeconds=settings.vehicles[1].maximumSeconds=4;
            var game=Create(); game.ForceVehicle(settings.vehicles[1],10); HoldUntilPredictedTarget(game);
            game.Advance(game.VehicleRemaining);
            Assert.That(game.State,Is.EqualTo(SessionState.ForcedDecay)); Assert.That(game.TimedOut,Is.True);
            Assert.That(game.Fuel,Is.LessThan(game.Request.MinimumFuel)); Assert.That(game.Results.Records,Is.Empty);
            double prediction=game.PredictedFinal; game.Advance(4);
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Timeout));
            Assert.That(game.LastRecord.fuel,Is.EqualTo(prediction).Within(1e-7));
            Assert.That(game.LastRecord.error,Is.LessThan(.1)); Assert.That(game.LastRecord.elapsed,Is.GreaterThan(4));
        }
        [Test] public void FlowStopCompletesBeforeTheOldZeroPressureTailCouldCauseTimeout()
        {
            settings.vehicles[0].minimumSeconds=settings.vehicles[0].maximumSeconds=3.6f;
            var game=Create(); game.ForceVehicle(settings.vehicles[0],25); HoldUntilPredictedTarget(game);
            game.Advance((game.Pressure.Value-game.Vehicle.resistance)/settings.pressureDecay);
            Assert.That(game.Flow,Is.Zero.Within(1e-8)); Assert.That(game.VehicleRemaining,Is.GreaterThan(0));
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success));
            Assert.That(game.LastRecord.elapsed,Is.LessThan(3.6)); Assert.That(game.LastRecord.timedOut,Is.False);
            Assert.That(game.LastRecord.fuel,Is.InRange(game.LastRecord.target-game.LastRecord.tolerance,game.LastRecord.target+game.LastRecord.tolerance));
        }
        [Test] public void SimultaneousFlowStopAndCustomerExpiryIsTimeout()
        {
            settings.vehicles[0].minimumSeconds=settings.vehicles[0].maximumSeconds=3.5f;
            var game=Create(); double hold=(3.5+20d/28)/(1+60d/28);
            game.SetHeld(true); game.Advance(hold); game.SetHeld(false); game.Advance(3.5-hold);
            Assert.That(game.Pressure.Value,Is.EqualTo(20).Within(1e-7)); Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Timeout));
            Assert.That(game.LastRecord.elapsed,Is.EqualTo(3.5).Within(1e-7));
        }
        [Test] public void ClosingPreservesHeldInputAndLetsTheLastCustomerSucceedExactlyOnce()
        {
            settings.operatingSeconds=2;
            var game=Create(); game.ForceVehicle(settings.vehicles[1],10);
            int id=game.TransactionId, resolved=0; game.Resolved+=_=>resolved++;
            game.SetHeld(true); game.Advance(2);
            Assert.That(game.State,Is.EqualTo(SessionState.Fueling)); Assert.That(game.OperatingRemaining,Is.Zero);
            Assert.That(game.Held && game.CanAcceptInput,Is.True); Assert.That(game.Results.Records,Is.Empty);
            double remaining=game.VehicleRemaining; game.End(); Assert.That(game.Held,Is.True);
            HoldUntilPredictedTarget(game);
            game.Advance((game.Pressure.Value-35)/settings.pressureDecay);
            Assert.That(game.State,Is.EqualTo(SessionState.Feedback)); Assert.That(game.CanAcceptInput,Is.False);
            Assert.That(game.VehicleRemaining,Is.LessThan(remaining));
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success));
            Assert.That(game.LastRecord.operatingEnd && game.LastRecord.OneTouch && game.LastRecord.perfect,Is.True);
            Assert.That(game.LastRecord.oneTouchBonus+game.LastRecord.perfectBonus,Is.GreaterThan(0));
            Assert.That(game.LastRecord.elapsed-2,Is.EqualTo(game.LastRecord.closingElapsed).Within(1e-7));
            game.Advance(20); game.End(); game.SetHeld(true);
            Assert.That(game.State,Is.EqualTo(SessionState.Result)); Assert.That(game.TransactionId,Is.EqualTo(id));
            Assert.That(game.Results.Records.Count,Is.EqualTo(1)); Assert.That(resolved,Is.EqualTo(1));
        }
        [Test] public void SimultaneousOperatingAndCustomerExpiryRecordsBothCauses()
        {
            settings.operatingSeconds=2;
            settings.vehicles[0].minimumSeconds=settings.vehicles[0].maximumSeconds=2;
            var game=Create(); game.SetHeld(true); game.Advance(2); game.Advance(4);
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Timeout));
            Assert.That(game.LastRecord.timedOut && game.LastRecord.operatingEnd,Is.True);
            Assert.That(game.Results.ClosingCount,Is.EqualTo(1));
        }
        [Test] public void PauseFreezesForcedDecayAndResumeCannotReenableTheButton()
        {
            settings.vehicles[0].minimumSeconds=settings.vehicles[0].maximumSeconds=1;
            var game=Create(); game.SetHeld(true); game.Advance(1);
            Assert.That(game.State,Is.EqualTo(SessionState.ForcedDecay));
            double pressure=game.Pressure.Value,fuel=game.Fuel,time=game.Elapsed;
            game.SetPaused(true); game.SetHeld(true); game.Advance(30);
            Assert.That(game.Pressure.Value,Is.EqualTo(pressure)); Assert.That(game.Fuel,Is.EqualTo(fuel));
            Assert.That(game.Elapsed,Is.EqualTo(time)); Assert.That(game.Held,Is.False);
            game.SetPaused(false); game.SetHeld(true); Assert.That(game.Held,Is.False); game.Advance(2);
            Assert.That(game.Results.Records.Count,Is.EqualTo(1));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void OverfillCrossingAndFinalFuelAreFrameRateIndependent(int fps)
        {
            var game=Create(); game.SetHeld(true);
            while(game.LastRecord==null && game.Elapsed<12) game.Advance(1d/fps);
            Assert.That(game.LastRecord,Is.Not.Null);
            Assert.That(game.LastRecord.fuel,Is.EqualTo(40+1e-7).Within(1e-6));
            Assert.That(game.LastRecord.elapsed,Is.EqualTo(3.5).Within(1e-6));
        }
        [Test] public void TransactionIdentifierPreventsDuplicateScoreAndMetricChanges()
        {
            var request=new VehicleFuelRequest(settings.vehicles[0],5,settings); var tracker=new GameResultTracker();
            var completion=new FuelingCompletion { transactionId=7,elapsed=5,stoppedWait=20d/28 };
            var first=tracker.Add(request,FuelingOutcome.Success,35,9,settings,42,completion);
            int score=tracker.TotalScore;
            var duplicate=tracker.Add(request,FuelingOutcome.Success,35,9,settings,42,completion);
            Assert.That(duplicate,Is.SameAs(first)); Assert.That(tracker.Records.Count,Is.EqualTo(1));
            Assert.That(tracker.TotalScore,Is.EqualTo(score)); Assert.That(tracker.SuccessCount,Is.EqualTo(1));
            StringAssert.Contains("flow_stopped_wait_seconds",tracker.ToCsv(settings));
        }
        [Test] public void ClosingDuringTransitionDoesNotCreateAnExtraCustomer()
        {
            settings.operatingSeconds=.7f;
            settings.vehicles[0].minimumSeconds=settings.vehicles[0].maximumSeconds=.2f;
            var game=Create(); int id=game.TransactionId; game.Advance(2);
            Assert.That(game.State,Is.EqualTo(SessionState.Result)); Assert.That(game.TransactionId,Is.EqualTo(id));
            Assert.That(game.Results.Records.Count,Is.EqualTo(1)); Assert.That(game.Results.ClosingCount,Is.Zero);
        }
        [Test] public void UnderfillCanResumeFromResistanceWithoutWaitingForZeroPressure()
        {
            var game=Create(); game.SetHeld(true); game.Advance(.6); game.SetHeld(false);
            game.Advance((game.Pressure.Value-20)/settings.pressureDecay);
            Assert.That(game.State,Is.EqualTo(SessionState.UnderfillWait));
            Assert.That(game.Pressure.Value,Is.EqualTo(20).Within(1e-7)); Assert.That(game.Results.Records,Is.Empty);
            int id=game.TransactionId; double fuel=game.Fuel;
            game.SetHeld(true); game.Advance(.1);
            Assert.That(game.Pressure.Value,Is.EqualTo(26).Within(1e-7)); Assert.That(game.Fuel,Is.GreaterThan(fuel));
            Assert.That(game.TransactionId,Is.EqualTo(id));
        }
        [Test] public void ClosingBelowResistanceAllowsTheUnfinishedCustomerToResume()
        {
            var game=Create(); game.SetHeld(true); game.Advance(.2); game.SetHeld(false);
            double pressure=game.Pressure.Value; game.End();
            Assert.That(game.State,Is.EqualTo(SessionState.Fueling)); Assert.That(game.Pressure.Value,Is.EqualTo(pressure));
            Assert.That(game.CanAcceptInput,Is.True); Assert.That(game.Results.Records,Is.Empty);
            HoldUntilPredictedTarget(game); game.Advance((game.Pressure.Value-20)/settings.pressureDecay+.5);
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success)); Assert.That(game.State,Is.EqualTo(SessionState.Result));
            Assert.That(game.Results.Records.Count,Is.EqualTo(1));
        }
        [Test] public void HoldingBelowResistanceDoesNotEndAnUnfinishedCustomer()
        {
            var game=Create(); game.SetHeld(true); game.Advance(.2);
            Assert.That(game.Pressure.Value,Is.LessThan(20)); Assert.That(game.Held,Is.True);
            Assert.That(game.State,Is.EqualTo(SessionState.Fueling)); Assert.That(game.Results.Records,Is.Empty);
            game.Advance(.2); Assert.That(game.Fuel,Is.GreaterThan(0));
        }
        [Test] public void ReleasedResidualFuelStopsAtTheOverfillBoundary()
        {
            var game=Create(); game.ForceVehicle(settings.vehicles[0],25);
            game.SetHeld(true); game.Advance(2); game.SetHeld(false);
            Assert.That(game.Fuel,Is.LessThan(18)); Assert.That(game.PredictedFinal,Is.GreaterThan(18));
            game.Advance(.4);
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Overfill));
            Assert.That(game.Fuel,Is.EqualTo(18+1e-7).Within(1e-6));
            Assert.That(game.Pressure.Value,Is.GreaterThan(20)); Assert.That(game.Flow,Is.Zero);
            double fuel=game.Fuel; game.Advance(.2); Assert.That(game.Fuel,Is.EqualTo(fuel));
        }
        [Test] public void OverfillDuringTimeoutDecayEndsImmediatelyAndPreservesBothCauses()
        {
            settings.vehicles[0].minimumSeconds=settings.vehicles[0].maximumSeconds=3;
            var game=Create(); game.SetHeld(true); game.Advance(3);
            Assert.That(game.TimedOut,Is.True); Assert.That(game.Results.Records,Is.Empty);
            game.Advance(1);
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Overfill));
            Assert.That(game.LastRecord.timedOut && game.LastRecord.overfilled,Is.True);
            Assert.That(game.Results.TotalScore,Is.EqualTo(-80)); Assert.That(game.Results.TotalPenalty,Is.EqualTo(80));
            Assert.That(game.Results.TimeoutCount,Is.EqualTo(1)); Assert.That(game.Results.OverfillCount,Is.EqualTo(1));
            Assert.That(game.LastRecord.fuel,Is.EqualTo(40+1e-7).Within(1e-6));
        }
        [Test] public void ClosingOverfillStopsImmediatelyAndKeepsTheLossInTheFinalResult()
        {
            settings.operatingSeconds=3;
            var game=Create(); int resolved=0; game.Resolved+=_=>resolved++;
            game.SetHeld(true); game.Advance(5);
            Assert.That(game.State,Is.EqualTo(SessionState.Result));
            Assert.That(game.LastRecord.overfilled && game.LastRecord.operatingEnd,Is.True);
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Overfill));
            Assert.That(game.Pressure.Value,Is.GreaterThan(20)); Assert.That(game.Results.TotalPenalty,Is.EqualTo(80));
            game.End(); game.Advance(5); Assert.That(resolved,Is.EqualTo(1));
        }
        [Test] public void AccumulatedLossCountsActualPenaltiesOnceAndSurvivesSuccessfulCustomers()
        {
            var request=new VehicleFuelRequest(settings.vehicles[0],5,settings); var tracker=new GameResultTracker();
            var completion=new FuelingCompletion {transactionId=7,elapsed=3.5,overfilled=true};
            tracker.Add(request,FuelingOutcome.Overfill,40+1e-7,10.5,settings,42,completion);
            tracker.Add(request,FuelingOutcome.Overfill,40+1e-7,10.5,settings,42,completion);
            tracker.Add(request,FuelingOutcome.Timeout,0,0,settings);
            tracker.Add(request,FuelingOutcome.Success,35,9,settings);
            Assert.That(tracker.TotalPenalty,Is.EqualTo(120)); Assert.That(tracker.LastPenalty,Is.EqualTo(40));
            Assert.That(tracker.Records.Count,Is.EqualTo(3));
        }
        [Test] public void FailureFeedbackCannotCarryHeldInputIntoTheNextCustomerAndRestartClearsLoss()
        {
            var game=Create(); game.SetHeld(true); game.Advance(3.6); game.SetHeld(true);
            Assert.That(game.State,Is.EqualTo(SessionState.Feedback)); Assert.That(game.Held,Is.False);
            Assert.That(game.FeedbackDuration,Is.EqualTo(settings.overfillFeedbackSeconds));
            game.Advance(2);
            Assert.That(game.State,Is.EqualTo(SessionState.Fueling)); Assert.That(game.Fuel,Is.Zero); Assert.That(game.Held,Is.False);
            Assert.That(game.Results.TotalPenalty,Is.EqualTo(80));
            game.Start(42); Assert.That(game.Results.TotalPenalty,Is.Zero); Assert.That(game.Results.LastPenalty,Is.Zero);
        }
        [Test] public void HeldUpdatesCountOnlyActualPressEdgesAndResetOnReplacement()
        {
            var game=Create(); game.SetHeld(true);
            for(int i=0;i<100;i++) game.SetHeld(true);
            Assert.That(game.PressCount,Is.EqualTo(1));
            game.SetHeld(false); game.SetHeld(true); Assert.That(game.PressCount,Is.EqualTo(2));
            game.ForceVehicle(settings.vehicles[0],25); Assert.That(game.PressCount,Is.Zero);
            game.SetPaused(true); game.SetHeld(true); Assert.That(game.PressCount,Is.Zero);
            game.SetPaused(false); game.SetHeld(true); Assert.That(game.PressCount,Is.EqualTo(1));
        }
        [TestCase(1,35,25,38)] [TestCase(2,35,0,38)]
        [TestCase(1,37,21,0)] [TestCase(2,37,0,0)] [TestCase(0,35,0,38)]
        public void SuccessBonusesStackOnlyForTheirOwnConditions(int presses,double fuel,int oneTouch,int perfect)
        {
            var request=new VehicleFuelRequest(settings.vehicles[0],5,settings); var results=new GameResultTracker();
            var record=results.Add(request,FuelingOutcome.Success,fuel,14,settings,42,new FuelingCompletion {transactionId=1,pressCount=presses});
            Assert.That(record.oneTouchBonus,Is.EqualTo(oneTouch)); Assert.That(record.perfectBonus,Is.EqualTo(perfect));
            Assert.That(record.score,Is.EqualTo(record.baseScore+oneTouch+perfect));
            Assert.That(results.TotalBonus,Is.EqualTo(oneTouch+perfect));
            Assert.That(results.OneTouchCount,Is.EqualTo(presses==1?1:0));
            Assert.That(results.PerfectCount,Is.EqualTo(perfect>0?1:0));
        }
        [TestCase(FuelingOutcome.Overfill)] [TestCase(FuelingOutcome.Timeout)] [TestCase(FuelingOutcome.OperatingEnd)]
        public void FailedAndClosingCustomersCannotEarnBonusesEvenWithPerfectQuantities(FuelingOutcome outcome)
        {
            var request=new VehicleFuelRequest(settings.vehicles[0],5,settings); var results=new GameResultTracker();
            var record=results.Add(request,outcome,35,4,settings,42,new FuelingCompletion {transactionId=1,pressCount=1});
            Assert.That(record.oneTouchBonus+record.perfectBonus,Is.Zero); Assert.That(record.perfect,Is.False);
            Assert.That(results.OneTouchCount+results.PerfectCount+results.TotalBonus,Is.Zero);
            Assert.That(record.score,Is.EqualTo(record.baseScore));
        }
        [TestCase(34,true)] [TestCase(36,true)] [TestCase(33.999,false)] [TestCase(36.001,false)]
        public void PerfectBonusIncludesBothOneLiterBoundaries(double fuel,bool expected)
        {
            var request=new VehicleFuelRequest(settings.vehicles[0],5,settings);
            var record=new GameResultTracker().Add(request,FuelingOutcome.Success,fuel,4,settings);
            Assert.That(record.perfect,Is.EqualTo(expected)); Assert.That(record.perfectBonus>0,Is.EqualTo(expected));
        }
        [Test] public void RealOneTouchSuccessRecordsBothBonusesOnceAndExportsTheirBreakdown()
        {
            var game=Create(); HoldUntilPredictedTarget(game);
            game.Advance((game.Pressure.Value-20)/settings.pressureDecay);
            var record=game.LastRecord; Assert.That(record.OneTouch && record.perfect,Is.True);
            Assert.That(record.pressCount,Is.EqualTo(1)); Assert.That(record.oneTouchBonus,Is.GreaterThan(0));
            Assert.That(record.perfectBonus,Is.GreaterThan(0)); int bonus=game.Results.TotalBonus, score=game.Results.TotalScore;
            game.SetHeld(true); game.Advance(.1);
            Assert.That(game.PressCount,Is.EqualTo(1)); Assert.That(game.Results.TotalBonus,Is.EqualTo(bonus));
            game.Results.Add(game.Request,FuelingOutcome.Success,record.fuel,game.VehicleRemaining,settings,42,
                new FuelingCompletion {transactionId=game.TransactionId,pressCount=1});
            Assert.That(game.Results.TotalBonus,Is.EqualTo(bonus)); Assert.That(game.Results.TotalScore,Is.EqualTo(score));
            StringAssert.Contains("press_count,base_score,one_touch_bonus,perfect_bonus",game.Results.ToCsv(settings));
        }
        [Test] public void RetryingAnUnderfillKeepsPerfectBonusButLosesOneTouchBonus()
        {
            var game=Create(); game.SetHeld(true); game.Advance(.6); game.SetHeld(false); game.Advance(1);
            Assert.That(game.State,Is.EqualTo(SessionState.UnderfillWait));
            HoldUntilPredictedTarget(game); game.Advance((game.Pressure.Value-20)/settings.pressureDecay);
            Assert.That(game.LastRecord.pressCount,Is.EqualTo(2)); Assert.That(game.LastRecord.perfect,Is.True);
            Assert.That(game.LastRecord.oneTouchBonus,Is.Zero); Assert.That(game.LastRecord.perfectBonus,Is.GreaterThan(0));
        }
        [Test] public void LastCustomerCanResumeAnUnderfillAfterClosingWithoutBeingReplaced()
        {
            settings.operatingSeconds=.7f;
            var game=Create(); game.SetHeld(true); game.Advance(.6); game.SetHeld(false); game.Advance(1);
            Assert.That(game.OperatingEnded,Is.True); Assert.That(game.State,Is.EqualTo(SessionState.UnderfillWait));
            Assert.That(game.CanAcceptInput,Is.True); int id=game.TransactionId;
            game.ForceVehicle(settings.vehicles[2],65); Assert.That(game.TransactionId,Is.EqualTo(id));
            HoldUntilPredictedTarget(game); game.Advance((game.Pressure.Value-20)/settings.pressureDecay+.5);
            Assert.That(game.State,Is.EqualTo(SessionState.Result)); Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success));
            Assert.That(game.LastRecord.pressCount,Is.EqualTo(2)); Assert.That(game.LastRecord.oneTouchBonus,Is.Zero);
            Assert.That(game.LastRecord.perfectBonus,Is.GreaterThan(0)); Assert.That(game.Results.ClosingCount,Is.EqualTo(1));
        }
        [Test] public void CustomerDeadlineStillExpiresAfterClosingAndCannotStallTheSession()
        {
            settings.operatingSeconds=1;
            var game=Create(); game.Advance(2); Assert.That(game.CanAcceptInput,Is.True);
            game.Advance(20);
            Assert.That(game.State,Is.EqualTo(SessionState.Result)); Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Timeout));
            Assert.That(game.LastRecord.operatingEnd && game.LastRecord.timedOut,Is.True);
            Assert.That(game.Results.TotalScore,Is.EqualTo(-40)); Assert.That(game.Results.TotalBonus,Is.Zero);
            Assert.That(game.Results.Records.Count,Is.EqualTo(1));
        }
        [Test] public void ClosingDoesNotClearAnEarlierTimeoutOrReenableItsButton()
        {
            settings.operatingSeconds=1.1f;
            settings.vehicles[0].minimumSeconds=settings.vehicles[0].maximumSeconds=1;
            var game=Create(); game.SetHeld(true); game.Advance(1.2);
            Assert.That(game.OperatingEnded && game.TimedOut,Is.True); Assert.That(game.CanAcceptInput,Is.False);
            game.SetHeld(true); Assert.That(game.Held,Is.False); game.Advance(10);
            Assert.That(game.State,Is.EqualTo(SessionState.Result)); Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Timeout));
        }
        [Test] public void PauseAfterClosingFreezesTheLastCustomerAndItsDeadline()
        {
            settings.operatingSeconds=.5f;
            var game=Create(); game.SetHeld(true); game.Advance(.6); game.SetPaused(true);
            double time=game.Elapsed, remaining=game.VehicleRemaining, pressure=game.Pressure.Value;
            game.Advance(20);
            Assert.That(game.Elapsed,Is.EqualTo(time)); Assert.That(game.VehicleRemaining,Is.EqualTo(remaining));
            Assert.That(game.Pressure.Value,Is.EqualTo(pressure)); Assert.That(game.CanAcceptInput,Is.False);
            game.SetPaused(false); Assert.That(game.CanAcceptInput,Is.True);
            HoldUntilPredictedTarget(game); game.Advance((game.Pressure.Value-20)/settings.pressureDecay+.5);
            Assert.That(game.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success)); Assert.That(game.State,Is.EqualTo(SessionState.Result));
        }
        [Test] public void ClosingDuringFinalFeedbackPreservesItAndStartsNoNewArrival()
        {
            var game=Create(); HoldUntilPredictedTarget(game); game.Advance((game.Pressure.Value-20)/settings.pressureDecay);
            Assert.That(game.State,Is.EqualTo(SessionState.Feedback)); int id=game.TransactionId;
            game.End(); Assert.That(game.State,Is.EqualTo(SessionState.Feedback));
            game.Advance(.1); Assert.That(game.State,Is.EqualTo(SessionState.Feedback));
            game.End(); game.Advance(2);
            Assert.That(game.State,Is.EqualTo(SessionState.Result)); Assert.That(game.TransactionId,Is.EqualTo(id));
            Assert.That(game.Results.Records.Count,Is.EqualTo(1));
        }
    }
}
