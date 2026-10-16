using System;

namespace GasStation
{
    public enum SessionState { Ready, Fueling, UnderfillWait, ForcedDecay, Feedback, Arrival, Result }

    public sealed class FuelingSimulation
    {
        const double Epsilon = 1e-8;
        public readonly PrototypeSettings Settings;
        public readonly PressureController Pressure = new PressureController();
        public GameResultTracker Results { get; private set; } = new GameResultTracker();
        public SessionState State { get; private set; } = SessionState.Ready;
        public VehicleFuelRequest Request { get; private set; }
        public VehicleSettings Vehicle => Request?.Type;
        public int Seed { get; private set; }
        public int TransactionId { get; private set; }
        public double OperatingRemaining { get; private set; }
        public double VehicleRemaining { get; private set; }
        public double VehicleElapsed { get; private set; }
        public double FlowStoppedWait { get; private set; }
        public double ClosingElapsed { get; private set; }
        public double Elapsed { get; private set; }
        public double Fuel { get; private set; }
        public bool Held { get; private set; }
        public int PressCount { get; private set; }
        public bool Paused { get; private set; }
        public bool TimedOut { get; private set; }
        public bool Overfilled { get; private set; }
        public bool OperatingEnded { get; private set; }
        public bool IsActiveCustomer => State == SessionState.Fueling || State == SessionState.UnderfillWait || State == SessionState.ForcedDecay;
        public bool CanAcceptInput => !Paused && (State == SessionState.Fueling || State == SessionState.UnderfillWait);
        public double DecayRemaining => IsActiveCustomer ? Math.Max(0, Pressure.Value - Vehicle.resistance) / Settings.pressureDecay : 0;
        public FuelingRecord LastRecord { get; private set; }
        public double TransitionRemaining { get; private set; }
        public double FeedbackDuration => LastRecord?.outcome == FuelingOutcome.Overfill ? Settings.overfillFeedbackSeconds : Settings.feedbackSeconds;
        public double Flow => !IsActiveCustomer || Vehicle == null || Pressure.Value <= Vehicle.resistance + Epsilon ? 0 : (Pressure.Value - Vehicle.resistance) * Settings.flowCoefficient;
        public double PredictedFinal => !IsActiveCustomer || Vehicle == null ? Fuel : Fuel + Pressure.RemainingFuel(Vehicle.resistance, Settings.flowCoefficient, Settings.pressureDecay);
        public event Action<FuelingRecord> Resolved;
        public event Action<VehicleFuelRequest> Spawned;
        VehicleSpawner spawner;

        public FuelingSimulation(PrototypeSettings settings) { settings.Validate(); Settings = settings; OperatingRemaining = settings.operatingSeconds; }
        public void Start(int seed)
        {
            Settings.Validate();
            Results = new GameResultTracker();
            LastRecord = null;
            OperatingRemaining = Settings.operatingSeconds;
            OperatingEnded = false;
            ClosingElapsed = Elapsed = 0;
            TransactionId = 0;
            spawner = new VehicleSpawner(Settings.vehicles, seed);
            Seed = seed;
            Paused = false;
            Spawn();
        }
        void Spawn(VehicleSettings forced = null, int? initialFuel = null)
        {
            Request = spawner.NextRequest(Settings, forced, initialFuel);
            TransactionId++;
            Fuel = VehicleElapsed = FlowStoppedWait = 0;
            VehicleRemaining = Request.TimeLimit;
            TimedOut = Overfilled = false;
            Pressure.Reset();
            Held = false;
            PressCount = 0;
            State = SessionState.Fueling;
            Spawned?.Invoke(Request);
        }
        public void SetHeld(bool held)
        {
            if (Paused) { Held = false; return; }
            // Resolve a stopped-flow customer before a simultaneous new press.
            if (IsActiveCustomer && Pressure.Value <= Vehicle.resistance + Epsilon && !Held) EvaluateBoundaries();
            bool accepted = held && CanAcceptInput;
            if (accepted && !Held) PressCount++;
            Held = accepted;
            if (Held) State = SessionState.Fueling;
        }
        public void SetPaused(bool paused) { Paused = paused; Held = false; }
        public void ForceVehicle(VehicleSettings vehicle, int? initialFuel = null)
        {
            if (State == SessionState.Ready || State == SessionState.Result || OperatingEnded) return;
            Spawn(vehicle, initialFuel);
        }
        public void End()
        {
            if (State == SessionState.Result || OperatingEnded) return;
            OperatingRemaining = 0;
            OperatingEnded = true;
            // Closing stops new arrivals, not the current customer's controls or clock.
            if (IsActiveCustomer) EvaluateBoundaries();
            else if (State != SessionState.Feedback) FinishSession();
        }
        void FinishSession() { Held = false; Paused = false; State = SessionState.Result; }

        void EvaluateBoundaries()
        {
            if (!IsActiveCustomer) return;
            Overfilled |= Fuel > Request.MaximumFuel + Epsilon;
            TimedOut |= VehicleRemaining <= Epsilon;
            if (Overfilled || TimedOut) { Held = false; State = SessionState.ForcedDecay; }
            // Overfill cuts delivery immediately, including fuel delivered after release.
            if (!Overfilled && (Pressure.Value > Vehicle.resistance + Epsilon || Held)) return;
            var outcome = FuelingResultEvaluator.Evaluate(Fuel, Request.Target, Request.Tolerance,
                VehicleRemaining, Pressure.Value <= Vehicle.resistance + Epsilon && !Held, TimedOut, Overfilled);
            if (outcome == FuelingOutcome.Continue) { State = Fuel > 0 ? SessionState.UnderfillWait : SessionState.Fueling; return; }
            LastRecord = Results.Add(Request, outcome, Fuel, VehicleRemaining, Settings, Seed,
                new FuelingCompletion { transactionId = TransactionId, pressCount = PressCount, elapsed = VehicleElapsed, stoppedWait = FlowStoppedWait,
                    timedOut = TimedOut, overfilled = Overfilled, operatingEnd = OperatingEnded, closingElapsed = ClosingElapsed });
            Held = false;
            // Change state before publishing the event to prevent duplicate resolution.
            State = SessionState.Feedback;
            TransitionRemaining = FeedbackDuration;
            Resolved?.Invoke(LastRecord);
        }

        double SplitAtOverfill(double dt)
        {
            if (Overfilled || dt <= 0) return dt;
            double required = Request.MaximumFuel + 1e-7 - Fuel;
            if (required <= 0) return dt;
            if (Pressure.PredictFuel(dt, Held, Settings.pressureRise, Settings.pressureDecay, Vehicle.resistance, Settings.flowCoefficient) < required) return dt;
            // Locate the first crossing using the same exact integral as fuel delivery.
            double low = 0, high = dt;
            for (int i = 0; i < 40; i++)
            {
                double middle = (low + high) * .5;
                if (Pressure.PredictFuel(middle, Held, Settings.pressureRise, Settings.pressureDecay, Vehicle.resistance, Settings.flowCoefficient) < required) low = middle;
                else high = middle;
            }
            return high;
        }

        public void Advance(double seconds)
        {
            if (Paused || State == SessionState.Ready || State == SessionState.Result || seconds <= 0) return;
            while (seconds > 0 && State != SessionState.Result)
            {
                if (IsActiveCustomer) EvaluateBoundaries();
                if (State == SessionState.Result) break;
                if (!IsActiveCustomer && TransitionRemaining <= Epsilon)
                {
                    if (OperatingEnded) { FinishSession(); break; }
                    if (State == SessionState.Feedback) { State = SessionState.Arrival; TransitionRemaining = Settings.arrivalSeconds; }
                    else Spawn();
                    continue;
                }
                double dt = Math.Min(seconds, 1.0 / 240);
                if (!OperatingEnded) dt = Math.Min(dt, OperatingRemaining);
                bool active = IsActiveCustomer;
                if (active)
                {
                    if (VehicleRemaining > Epsilon) dt = Math.Min(dt, VehicleRemaining);
                    if (!Held && Pressure.Value > 0)
                    {
                        dt = Math.Min(dt, Pressure.Value / Settings.pressureDecay);
                        if (Pressure.Value > Vehicle.resistance + Epsilon)
                            dt = Math.Min(dt, (Pressure.Value - Vehicle.resistance) / Settings.pressureDecay);
                    }
                    dt = SplitAtOverfill(dt);
                    if (!Held && Pressure.Value > 0 && Pressure.Value <= Vehicle.resistance + Epsilon) FlowStoppedWait += dt;
                    Fuel += Pressure.Advance(dt, Held, Settings.pressureRise, Settings.pressureDecay, Vehicle.resistance, Settings.flowCoefficient);
                    VehicleElapsed += dt;
                    VehicleRemaining = Math.Max(0, VehicleRemaining - dt);
                }
                else TransitionRemaining = Math.Max(0, TransitionRemaining - dt);
                Elapsed += dt;
                if (OperatingEnded) ClosingElapsed += dt;
                else OperatingRemaining = Math.Max(0, OperatingRemaining - dt);
                seconds -= dt;

                // Delivered fuel precedes simultaneous expirations. The customer deadline
                // remains active after closing, including during forced decay.
                if (active && VehicleRemaining <= Epsilon) TimedOut = true;
                if (!OperatingEnded && OperatingRemaining <= Epsilon) End();
                if (IsActiveCustomer) EvaluateBoundaries();
            }
        }
    }
}
