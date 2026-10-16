using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GasStation.Tests
{
    public class PrototypeInteractionTests
    {
        GameSessionManager session;
        [UnitySetUp] public IEnumerator Setup()
        {
            SceneManager.LoadScene("GasStationPrototype"); yield return null; yield return null;
            session=Object.FindAnyObjectByType<GameSessionManager>();
            Assert.That(session,Is.Not.Null);
        }
        [UnityTest] public IEnumerator StartAndHoldButtonOperateThroughTheUi()
        {
            var buttons=Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            Button start=null;
            foreach(var b in buttons) if(b.GetComponentInChildren<Text>().text=="영업 시작  →") start=b;
            Assert.That(start,Is.Not.Null);
            start.onClick.Invoke(); yield return null;
            Assert.That(session.Simulation.State,Is.EqualTo(SessionState.Fueling));
            session.Simulation.ForceVehicle(session.settings.vehicles[0]);
            var hold=Object.FindAnyObjectByType<HoldPressureButton>();
            var pointer=new PointerEventData(EventSystem.current) { pointerId=1, button=PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            yield return new WaitForSeconds(1);
            Assert.That(session.Simulation.Pressure.Value,Is.GreaterThan(40));
            Assert.That(session.Simulation.Fuel,Is.GreaterThan(0));
            double before=session.Simulation.Fuel;
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            yield return new WaitForSeconds(.3f);
            Assert.That(session.Simulation.Held,Is.False);
            Assert.That(session.Simulation.Fuel,Is.GreaterThan(before),"Fuel must continue after release.");
        }
        [UnityTest] public IEnumerator PauseAndPointerExitReleaseHeldInput()
        {
            session.StartSession(); var hold=Object.FindAnyObjectByType<HoldPressureButton>();
            var pointer=new PointerEventData(EventSystem.current) {pointerId=2,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerDownHandler); yield return null;
            Assert.That(session.Simulation.Held,Is.True);
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerExitHandler); yield return null;
            Assert.That(session.Simulation.Held,Is.False);
            session.TogglePause(); double timer=session.Simulation.OperatingRemaining;
            yield return new WaitForSeconds(.2f);
            Assert.That(session.Simulation.OperatingRemaining,Is.EqualTo(timer));
            session.TogglePause(); yield return null;
            Assert.That(session.Simulation.Held,Is.False);
        }
        [UnityTest] public IEnumerator RandomFuelUiAndDebugOverridesUseTheSameLockedRequest()
        {
            session.StartSession(); session.ToggleDebug();
            session.ForceVehicle(0); session.Simulation.ForceVehicle(session.settings.vehicles[0],25);
            Object.FindAnyObjectByType<GameUIController>().Refresh(); yield return null;
            Assert.That(session.Simulation.Paused,Is.True);
            session.AdjustInitialFuel(-5);
            Assert.That(session.Simulation.Request.InitialFuel,Is.EqualTo(20));
            Assert.That(session.Simulation.Request.Target,Is.EqualTo(20));
            Assert.That(session.Simulation.Request.Tolerance,Is.EqualTo(4));
            Assert.That(session.Simulation.Fuel,Is.Zero); Assert.That(session.PracticeModified,Is.True);
            session.ToggleDebug(); Object.FindAnyObjectByType<GameUIController>().Refresh(); yield return null;
            bool hasInitial=false,hasTarget=false;
            foreach(var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
            {
                hasInitial |= text.text=="탱크 용량 40 L   ·   기존 연료 20 L";
                hasTarget |= text.text=="목표 20 L\n허용 16–24 L";
            }
            Assert.That(hasInitial && hasTarget,Is.True,"Fuel request must be shown consistently in the actual UI.");
        }
        [UnityTest] public IEnumerator FixedSeedRestartsReproduceArrivalsAndRemainPracticeSessions()
        {
            session.ToggleFixedSeed(); session.StartSession();
            string type=session.Simulation.Vehicle.id; int initial=session.Simulation.Request.InitialFuel;
            session.StartSession(); yield return null;
            Assert.That(session.Simulation.Seed,Is.EqualTo(session.DebugSeed));
            Assert.That(session.Simulation.Vehicle.id,Is.EqualTo(type));
            Assert.That(session.Simulation.Request.InitialFuel,Is.EqualTo(initial));
            Assert.That(session.PracticeModified,Is.True);
        }
        [UnityTest] public IEnumerator UpdatedScreensRenderThroughAnEntireShift()
        {
            session.enabled=false;
            var ui=Object.FindAnyObjectByType<GameUIController>();
            Directory.CreateDirectory("Verification/v0.2.5");
            ui.Refresh(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/ready.png")); yield return null; yield return null;
            session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],25);
            session.Simulation.SetHeld(true); session.Simulation.Advance(.8); session.Simulation.SetHeld(false);
            ui.Refresh(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/small-fueling.png")); yield return null; yield return null;
            session.ToggleDebug(); ui.Refresh(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/debug.png")); yield return null; yield return null;
            session.ToggleDebug();
            session.Simulation.SetHeld(true);
            while(session.Simulation.CanAcceptInput && session.Simulation.PredictedFinal<session.Simulation.Request.Target) session.Simulation.Advance(1d/240);
            session.Simulation.SetHeld(false);
            session.Simulation.Advance((session.Simulation.Pressure.Value-session.Simulation.Vehicle.resistance)/session.settings.pressureDecay);
            ui.Refresh(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/flow-stopped.png")); yield return null; yield return null;
            session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],25);
            session.Simulation.SetHeld(true); session.Simulation.Advance(2.3);
            ui.Refresh(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/overfill-failure.png")); yield return null; yield return null;
            session.Simulation.End(); session.Simulation.Advance(2); ui.Refresh(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/result-loss.png")); yield return null; yield return null;
            session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[1],10);
            session.Simulation.SetHeld(true); session.Simulation.Advance(2); session.Simulation.End();
            ui.Refresh(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/closing.png")); yield return null; yield return null;
            session.ToggleFixedSeed(); session.StartSession();
            while(session.Simulation.State!=SessionState.Result)
            {
                var game=session.Simulation;
                if(game.CanAcceptInput) game.SetHeld(game.PredictedFinal<game.Request.Target);
                game.Advance(1d/120);
            }
            Assert.That(session.Simulation.Results.SuccessCount,Is.GreaterThan(10));
            Assert.That(session.Simulation.Results.OverfillCount+session.Simulation.Results.TimeoutCount,Is.Zero);
            ui.Refresh(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/result.png")); yield return null; yield return null;
            File.WriteAllText("Verification/v0.2.5/session.csv",session.Simulation.Results.ToCsv(session.settings));
        }
        [UnityTest] public IEnumerator OverfillStopsTheActualPumpAndShowsTheFailureAndLoss()
        {
            session.enabled=false; session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],25);
            session.Simulation.SetHeld(true); session.Simulation.Advance(2.3);
            var ui=Object.FindAnyObjectByType<GameUIController>(); ui.Refresh(); yield return null;
            var hold=Object.FindAnyObjectByType<HoldPressureButton>();
            Assert.That(hold.GetComponent<Button>().interactable,Is.False);
            Assert.That(session.Simulation.Results.Records.Count,Is.EqualTo(1));
            bool failure=false,penalty=false,quantity=false;
            foreach(var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
            {
                failure |= text.text=="주유 실패 · 초과 주유";
                penalty |= text.text=="−80점";
                quantity |= text.text==">18.00";
            }
            Assert.That(failure && penalty && quantity,Is.True,"Visible failure must show the actual loss and exceeded limit.");
            Assert.That(GameObject.Find("Success Celebration"),Is.Null,"Failure must never receive a success effect.");
            double pressure=session.Simulation.Pressure.Value,fuel=session.Simulation.Fuel;
            var pointer=new PointerEventData(EventSystem.current) {pointerId=5,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            session.Simulation.SetHeld(true); session.Simulation.Advance(.2);
            Assert.That(session.Simulation.Held,Is.False); Assert.That(session.Simulation.Pressure.Value,Is.EqualTo(pressure));
            Assert.That(session.Simulation.Fuel,Is.EqualTo(fuel));
            session.Simulation.Advance(4); Assert.That(session.Simulation.Results.Records.Count,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator FlowStopShowsFinalResultAndDisablesThePumpImmediately()
        {
            session.enabled=false; session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],25);
            session.Simulation.SetHeld(true);
            while(session.Simulation.PredictedFinal<15 && session.Simulation.CanAcceptInput) session.Simulation.Advance(1d/240);
            session.Simulation.SetHeld(false);
            session.Simulation.Advance((session.Simulation.Pressure.Value-20)/session.settings.pressureDecay);
            var ui=Object.FindAnyObjectByType<GameUIController>(); ui.Refresh(); yield return null;
            bool finalized=false;
            foreach(var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
                finalized |= text.text=="유량 0 · 최종 주유량 확정";
            Assert.That(finalized,Is.True); Assert.That(session.Simulation.Results.Records.Count,Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<HoldPressureButton>().GetComponent<Button>().interactable,Is.False);
            Assert.That(session.Simulation.Pressure.Value,Is.EqualTo(20).Within(1e-7));
            Assert.That(session.Simulation.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success));
        }
        [UnityTest] public IEnumerator UnderfillWaitAllowsAnotherPointerPressOnTheSameCustomer()
        {
            session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],5);
            session.Simulation.SetHeld(true); session.Simulation.Advance(.6); session.Simulation.SetHeld(false); session.Simulation.Advance(2);
            Assert.That(session.Simulation.State,Is.EqualTo(SessionState.UnderfillWait));
            int id=session.Simulation.TransactionId;
            var hold=Object.FindAnyObjectByType<HoldPressureButton>();
            var pointer=new PointerEventData(EventSystem.current) {pointerId=6,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerDownHandler); yield return null;
            Assert.That(session.Simulation.Held,Is.True); Assert.That(session.Simulation.TransactionId,Is.EqualTo(id));
            Assert.That(session.Simulation.Results.Records,Is.Empty);
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerUpHandler); yield return null;
        }
        [UnityTest] public IEnumerator LossRemainsVisibleAfterTheNextArrivalAndOnTheFinalScreen()
        {
            session.enabled=false; session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],25);
            session.Simulation.SetHeld(true); session.Simulation.Advance(2.3); session.Simulation.Advance(2);
            var ui=Object.FindAnyObjectByType<GameUIController>(); ui.Refresh(); yield return null;
            Assert.That(session.Simulation.State,Is.EqualTo(SessionState.Fueling));
            bool history=false;
            foreach(var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
                history |= text.text=="최근 감점 −80점  ·  누적 감점 −80점";
            Assert.That(history,Is.True,"Loss must remain visible when the failure banner is gone.");
            session.Simulation.End(); ReleaseAtPredictedAmount(session.Simulation.Request.Target);
            session.Simulation.Advance((session.Simulation.Pressure.Value-session.Simulation.Vehicle.resistance)/session.settings.pressureDecay+.5);
            ui.Refresh(); yield return null;
            bool summary=false;
            foreach(var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
                summary |= text.text=="이번 영업의 누적 감점 −80점";
            Assert.That(summary,Is.True); Assert.That(session.Simulation.Results.TotalPenalty,Is.EqualTo(80));
        }
        void ReleaseAtPredictedAmount(double liters)
        {
            session.Simulation.SetHeld(true);
            while(session.Simulation.CanAcceptInput && session.Simulation.PredictedFinal<liters) session.Simulation.Advance(1d/240);
            Assert.That(session.Simulation.CanAcceptInput,Is.True);
            session.Simulation.SetHeld(false);
        }
        [UnityTest] public IEnumerator OneTouchPerfectEffectWaitsForFinalSuccessAndNeverBlocksTheNextCar()
        {
            session.enabled=false; session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],5);
            ReleaseAtPredictedAmount(35);
            var ui=Object.FindAnyObjectByType<GameUIController>(); ui.Refresh(); yield return null;
            Assert.That(GameObject.Find("Success Celebration"),Is.Null,"Target prediction is not a confirmed success.");
            session.Simulation.Advance((session.Simulation.Pressure.Value-20)/session.settings.pressureDecay+.12);
            ui.Refresh(); yield return null;
            var effect=GameObject.Find("Success Celebration"); Assert.That(effect,Is.Not.Null);
            var record=session.Simulation.LastRecord;
            Assert.That(record.OneTouch && record.perfect,Is.True);
            bool title=false,breakdown=false;
            foreach(var label in effect.GetComponentsInChildren<Text>())
            {
                title |= label.text=="원터치 퍼펙트!";
                breakdown |= label.text==$"원터치 +{record.oneTouchBonus}점  ·  퍼펙트 +{record.perfectBonus}점";
            }
            Assert.That(title && breakdown,Is.True); Assert.That(effect.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            Directory.CreateDirectory("Verification/v0.2.5");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/one-touch-perfect.png")); yield return null; yield return null;
            var sparks=effect.GetComponentInChildren<PrototypeGraphic>(); float progress=sparks.effectProgress;
            session.Simulation.Advance(.1); ui.Refresh(); yield return null;
            Assert.That(sparks.effectProgress,Is.GreaterThan(progress));
            session.Simulation.Advance(1); ui.Refresh(); yield return null;
            Assert.That(effect.activeSelf,Is.False); Assert.That(session.Simulation.CanAcceptInput,Is.True);
            var hold=Object.FindAnyObjectByType<HoldPressureButton>();
            var pointer=new PointerEventData(EventSystem.current) {pointerId=9,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerDownHandler); session.enabled=true; yield return null;
            Assert.That(session.Simulation.Held,Is.True); Assert.That(session.Simulation.PressCount,Is.EqualTo(1));
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerUpHandler); yield return null;
        }
        [UnityTest] public IEnumerator CelebrationFreezesWithPauseAndClearsOnRestart()
        {
            session.enabled=false; session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],5);
            ReleaseAtPredictedAmount(35); session.Simulation.Advance((session.Simulation.Pressure.Value-20)/session.settings.pressureDecay+.15);
            var ui=Object.FindAnyObjectByType<GameUIController>(); ui.Refresh(); yield return null;
            var effect=GameObject.Find("Success Celebration"); Assert.That(effect,Is.Not.Null);
            float alpha=effect.GetComponent<CanvasGroup>().alpha;
            float progress=effect.GetComponentInChildren<PrototypeGraphic>().effectProgress;
            session.Simulation.SetPaused(true); session.Simulation.Advance(10); ui.Refresh(); yield return null;
            Assert.That(effect.GetComponent<CanvasGroup>().alpha,Is.EqualTo(alpha));
            Assert.That(effect.GetComponentInChildren<PrototypeGraphic>().effectProgress,Is.EqualTo(progress));
            session.StartSession(); ui.Refresh(); yield return null;
            Assert.That(effect.activeSelf,Is.False); Assert.That(session.Simulation.Results.TotalBonus,Is.Zero);
        }
        [UnityTest] public IEnumerator RetriedPerfectSuccessShowsOnlyItsAccuracyBonus()
        {
            session.enabled=false; session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[0],5);
            session.Simulation.SetHeld(true); session.Simulation.Advance(.6); session.Simulation.SetHeld(false); session.Simulation.Advance(1);
            ReleaseAtPredictedAmount(35); session.Simulation.Advance((session.Simulation.Pressure.Value-20)/session.settings.pressureDecay+.12);
            var ui=Object.FindAnyObjectByType<GameUIController>(); ui.Refresh(); yield return null;
            var effect=GameObject.Find("Success Celebration"); Assert.That(effect,Is.Not.Null);
            var record=session.Simulation.LastRecord;
            Assert.That(record.pressCount,Is.EqualTo(2)); Assert.That(record.oneTouchBonus,Is.Zero);
            bool title=false,bonus=false;
            foreach(var label in effect.GetComponentsInChildren<Text>())
            {
                title |= label.text=="퍼펙트!";
                bonus |= label.text==$"퍼펙트 +{record.perfectBonus}점";
            }
            Assert.That(title && bonus,Is.True);
            Directory.CreateDirectory("Verification/v0.2.5");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/perfect-retry.png")); yield return null; yield return null;
        }
        [UnityTest] public IEnumerator LastCustomerRemainsPlayableAfterClosingAndThenShowsTheFinalResult()
        {
            session.enabled=false; session.settings.operatingSeconds=1; session.StartSession();
            session.Simulation.ForceVehicle(session.settings.vehicles[0],5);
            session.Simulation.SetHeld(true); session.Simulation.Advance(1.2);
            var ui=Object.FindAnyObjectByType<GameUIController>(); ui.Refresh(); yield return null;
            var hold=Object.FindAnyObjectByType<HoldPressureButton>();
            Assert.That(hold.GetComponent<Button>().interactable,Is.True);
            Assert.That(session.Simulation.Held,Is.True); int id=session.Simulation.TransactionId;
            bool closing=false,clock=false;
            foreach(var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
            {
                closing |= text.text=="영업 마감 · 마지막 차량 주유를 마무리하세요";
                clock |= text.text=="00:00";
            }
            Assert.That(closing && clock,Is.True); Assert.That(session.Simulation.Results.Records,Is.Empty);
            Directory.CreateDirectory("Verification/v0.2.5");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/closing-playable.png")); yield return null; yield return null;
            session.Simulation.SetHeld(false); session.Simulation.Advance((session.Simulation.Pressure.Value-20)/session.settings.pressureDecay);
            Assert.That(session.Simulation.State,Is.EqualTo(SessionState.UnderfillWait));
            var pointer=new PointerEventData(EventSystem.current) {pointerId=10,button=PointerEventData.InputButton.Left};
            ui.Refresh(); ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            session.enabled=true; yield return null; Assert.That(session.Simulation.Held,Is.True);
            ExecuteEvents.Execute(hold.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            session.enabled=false; session.ReleaseInputs();
            ReleaseAtPredictedAmount(35); session.Simulation.Advance((session.Simulation.Pressure.Value-20)/session.settings.pressureDecay+.1);
            ui.Refresh(); yield return null;
            Assert.That(session.Simulation.State,Is.EqualTo(SessionState.Feedback));
            Assert.That(GameObject.Find("Success Celebration"),Is.Not.Null);
            Assert.That(session.Simulation.LastRecord.outcome,Is.EqualTo(FuelingOutcome.Success));
            Assert.That(session.Simulation.LastRecord.perfectBonus,Is.GreaterThan(0));
            session.Simulation.Advance(1); ui.Refresh(); yield return null;
            Assert.That(session.Simulation.State,Is.EqualTo(SessionState.Result));
            Assert.That(session.Simulation.TransactionId,Is.EqualTo(id)); Assert.That(session.Simulation.Results.Records.Count,Is.EqualTo(1));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/v0.2.5/closing-success-result.png")); yield return null; yield return null;
        }
    }
}
