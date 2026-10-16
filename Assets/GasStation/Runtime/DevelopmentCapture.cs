using System.Collections;
using System.IO;
using UnityEngine;

namespace GasStation
{
    // Opt-in build smoke check. Never runs during normal gameplay.
    public class DevelopmentCapture : MonoBehaviour
    {
        IEnumerator Start()
        {
            var session=GetComponent<GameSessionManager>(); var ui=GetComponent<GameUIController>();
            session.enabled=false;
            if(session.SoundEnabled) session.ToggleSound();
            Application.runInBackground=true;
            string folder=Application.isEditor?Path.Combine(Application.dataPath,"..","Verification"):Path.Combine(Application.dataPath,"..","..","..","Verification");
            folder=Path.GetFullPath(folder); Directory.CreateDirectory(folder);
            while(!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            ui.Refresh(); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder,"ready.png"));
            yield return new WaitForSecondsRealtime(.3f);
            session.StartSession(); session.Simulation.ForceVehicle(session.settings.vehicles[1], 10);
            session.Simulation.SetHeld(true); session.Simulation.Advance(3.2); session.Simulation.SetHeld(false);
            ui.Refresh(); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder,"fueling.png"));
            yield return new WaitForSecondsRealtime(.3f);
            // Autoplay a complete shift with the same public simulation interface.
            session.StartSession();
            while(session.Simulation.State!=SessionState.Result)
            {
                var simulation=session.Simulation;
                if(simulation.CanAcceptInput) simulation.SetHeld(simulation.PredictedFinal<simulation.Request.Target);
                simulation.Advance(1d/120);
            }
            ui.Refresh(); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder,"result.png"));
            yield return new WaitForSecondsRealtime(.3f);
            File.WriteAllText(Path.Combine(folder,"build-smoke.txt"),"Standalone reached Ready, Fueling and Result.\nScore: "+session.Simulation.Results.TotalScore+"\nSuccesses: "+session.Simulation.Results.SuccessCount+"\n");
            Application.Quit(0);
        }
    }
}
