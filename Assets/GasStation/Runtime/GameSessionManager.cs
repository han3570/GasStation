using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation
{
    [RequireComponent(typeof(GameUIController))]
    public class GameSessionManager : MonoBehaviour
    {
        public PrototypeSettings settings;
        public FuelingSimulation Simulation { get; private set; }
        public bool SoundEnabled { get; private set; } = true;
        public int BestScore { get; private set; }
        public bool DebugVisible { get; private set; }
        public bool PracticeModified { get; private set; }
        public string ExportMessage { get; private set; } = "";
        public string BalanceName { get; private set; } = "STANDARD";
        public bool FixedSeedEnabled { get; private set; }
        [SerializeField] int debugSeed = 12345;
        [SerializeField] bool logVehicleData;
        public int DebugSeed => debugSeed;
        GameUIController ui;
        bool pointerHeld, keyHeld, savedResult;
        AudioSource pump, feedback;
        AudioClip successTone, perfectTone, failureTone;
        const string BestKey = "GasStation.Best.Standard.v2.ClosingCustomer";

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            var original = settings != null ? settings : Resources.Load<PrototypeSettings>("PrototypeSettings");
            settings = original != null ? Instantiate(original) : ScriptableObject.CreateInstance<PrototypeSettings>();
            Simulation = new FuelingSimulation(settings);
            BestScore = PlayerPrefs.GetInt(BestKey, 0);
            ui = GetComponent<GameUIController>();
            ui.Build(this);
            CreateAudio();
            Simulation.Resolved += OnResolved;
            Simulation.Spawned += OnSpawned;
            if (Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "-capture-prototype") >= 0)
                gameObject.AddComponent<DevelopmentCapture>();
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame && Simulation.CanAcceptInput) keyHeld = true;
                if (keyboard.spaceKey.wasReleasedThisFrame) keyHeld = false;
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
                if (keyboard.f1Key.wasPressedThisFrame) ToggleDebug();
            }
            Simulation.SetHeld(pointerHeld || keyHeld);
            Simulation.Advance(Time.deltaTime);
            if (!Simulation.CanAcceptInput) ReleaseInputs();
            if (Simulation.State == SessionState.Result && !savedResult)
            {
                savedResult = true;
                if (!PracticeModified && BalanceName == "STANDARD" && Simulation.Results.TotalScore > BestScore)
                {
                    BestScore = Simulation.Results.TotalScore;
                    PlayerPrefs.SetInt(BestKey, BestScore);
                    PlayerPrefs.Save();
                }
            }
            pump.volume = SoundEnabled && Simulation.IsActiveCustomer && !Simulation.Paused ? Mathf.Clamp01((float)Simulation.Flow / 16) * .065f : 0;
            pump.pitch = .6f + (float)Simulation.Pressure.Value / 100 * 1.1f;
            ui.Refresh();
        }
        public void StartSession()
        {
            ReleaseInputs();
            savedResult = false; PracticeModified = FixedSeedEnabled || Mathf.Abs(settings.pressureRise-60)>.001f || Mathf.Abs(settings.flowCoefficient-.2f)>.0001f; ExportMessage = "";
            Simulation.Start(FixedSeedEnabled ? debugSeed : Environment.TickCount);
            // Opening a new shift from the test panel must keep timers paused.
            if (DebugVisible) Simulation.SetPaused(true);
        }
        public void SetPointerHeld(bool held) { pointerHeld = held && Simulation.CanAcceptInput; }
        public void ReleaseInputs() { pointerHeld = false; keyHeld = false; Simulation.SetHeld(false); }
        public void TogglePause()
        {
            if (Simulation.State == SessionState.Ready || Simulation.State == SessionState.Result) return;
            ReleaseInputs(); Simulation.SetPaused(!Simulation.Paused);
        }
        public void ToggleDebug() { DebugVisible = !DebugVisible; ReleaseInputs(); if (Simulation.State != SessionState.Ready && Simulation.State != SessionState.Result) Simulation.SetPaused(DebugVisible); }
        public void ToggleSound() { SoundEnabled = !SoundEnabled; feedback.mute = !SoundEnabled; }
        public void SelectBalance(int index)
        {
            if (Simulation.State != SessionState.Ready && Simulation.State != SessionState.Result) PracticeModified = true;
            settings.pressureDecay = index == 1 ? 40 : index == 2 ? 18 : 28;
            BalanceName = index == 1 ? "FAST DECAY" : index == 2 ? "SLOW DECAY" : "STANDARD";
            ReleaseInputs();
        }
        public void AdjustRise(float delta) { settings.pressureRise = Mathf.Clamp(settings.pressureRise + delta, 10, 150); PracticeModified = true; }
        public void AdjustFlow(float delta) { settings.flowCoefficient = Mathf.Clamp(settings.flowCoefficient + delta, .05f, .5f); PracticeModified = true; }
        public void ForceVehicle(int index) { if(Simulation.OperatingEnded) return; ReleaseInputs(); Simulation.ForceVehicle(settings.vehicles[index]); PracticeModified = true; }
        public void ToggleFixedSeed() { FixedSeedEnabled = !FixedSeedEnabled; PracticeModified = true; }
        public void AdjustInitialFuel(int delta)
        {
            var request = Simulation.Request;
            if (request == null || Simulation.OperatingEnded || Simulation.State == SessionState.Ready || Simulation.State == SessionState.Result) return;
            ReleaseInputs();
            int fuel = Mathf.Clamp(request.InitialFuel + delta, request.Type.minRemainingFuel, request.Type.maxRemainingFuel);
            Simulation.ForceVehicle(request.Type, fuel);
            PracticeModified = true;
        }
        public void ExportResults()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "gas-station-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                File.WriteAllText(path, Simulation.Results.ToCsv(settings), new System.Text.UTF8Encoding(true));
                ExportMessage = "CSV 저장 완료 · " + Path.GetFileName(path);
                Debug.Log("Gas Station metrics saved: " + path);
            }
            catch (Exception e) { ExportMessage = "저장 실패: " + e.Message; Debug.LogException(e); }
        }
        void OnApplicationFocus(bool focus) { if (!focus) Suspend(); }
        void OnApplicationPause(bool pause) { if (pause) Suspend(); }
        void Suspend()
        {
            ReleaseInputs();
            if (Simulation != null && Simulation.State != SessionState.Ready && Simulation.State != SessionState.Result) Simulation.SetPaused(true);
        }
        void OnResolved(FuelingRecord record)
        {
            ReleaseInputs();
            if (logVehicleData) Debug.Log($"Fuel result: {record.vehicle}, initial={record.initialFuel}L, target={record.target}L, pumped={record.fuel:F3}L, elapsed={record.elapsed:F3}s, outcome={record.outcome}, score={record.score}");
            if (SoundEnabled && record.outcome != FuelingOutcome.OperatingEnd)
                feedback.PlayOneShot(record.outcome == FuelingOutcome.Success ? (record.perfect ? perfectTone : successTone) : failureTone,
                    record.outcome == FuelingOutcome.Success ? .18f : .28f);
        }
        void OnSpawned(VehicleFuelRequest request)
        {
            // A long frame can contain departure and a new arrival; never carry its input.
            ReleaseInputs();
            if (logVehicleData) Debug.Log($"Vehicle arrival: seed={Simulation.Seed}, {request.Type.id}, tank={request.TankCapacity}L, initial={request.InitialFuel}L, target={request.Target}L, tolerance={request.Tolerance:F1}L, time={request.TimeLimit:F1}s");
        }
        void CreateAudio()
        {
            pump = gameObject.AddComponent<AudioSource>(); feedback = gameObject.AddComponent<AudioSource>();
            pump.clip = Tone("Pump", 100, .5f); pump.loop = true; pump.volume = 0; pump.Play();
            successTone = Tone("Success", 660, .24f); perfectTone = PerfectTone(); failureTone = FailureTone();
        }
        static AudioClip PerfectTone()
        {
            const int rate=22050;
            var samples=new float[(int)(rate*.30f)];
            for(int i=0;i<samples.Length;i++)
            {
                float time=i/(float)rate;
                float pulse=time<.14f?time:time-.14f;
                float frequency=time<.14f?660:880;
                float duration=time<.14f?.14f:.16f;
                float envelope=Mathf.Min(1,pulse/.012f)*Mathf.Clamp01((duration-pulse)/.035f);
                samples[i]=Mathf.Sin(2*Mathf.PI*frequency*pulse)*envelope;
            }
            var clip=AudioClip.Create("Perfect",samples.Length,1,rate,false);
            clip.SetData(samples,0); return clip;
        }
        static AudioClip FailureTone()
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * .55f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                float pulse = time < .25f ? time : time - .30f;
                if (pulse < 0 || pulse > .25f) continue;
                float frequency = time < .25f ? 240 : 150;
                float envelope = Mathf.Min(1, pulse / .015f) * Mathf.Clamp01((.25f - pulse) / .05f);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * pulse) * envelope;
            }
            var clip = AudioClip.Create("Failure", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
        static AudioClip Tone(string name, float frequency, float duration)
        {
            int count = (int)(22050 * duration); var samples = new float[count];
            for (int i = 0; i < count; i++) samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / 22050) * Mathf.Min(1, i / 180f) * Mathf.Min(1, (count-i) / 500f);
            var clip = AudioClip.Create(name, count, 1, 22050, false); clip.SetData(samples, 0); return clip;
        }
        void OnDestroy()
        {
            if (Simulation != null) Simulation.Resolved -= OnResolved;
            if (Simulation != null) Simulation.Spawned -= OnSpawned;
            if (pump != null && pump.clip != null) Destroy(pump.clip);
            if (successTone != null) Destroy(successTone);
            if (perfectTone != null) Destroy(perfectTone);
            if (failureTone != null) Destroy(failureTone);
            if (settings != null) Destroy(settings);
        }
    }
}
