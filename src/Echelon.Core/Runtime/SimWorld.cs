using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class SimWorld
    {
        private readonly List<SimSystem> _systems = new List<SimSystem>();
        private double _accum;

        public SimWorld(long seed = 0)
        {
            Rng = new SimRng(seed);
        }

        public long Tick { get; private set; }
        public bool Paused { get; private set; }
        public int SpeedIndex { get; private set; } = SimConst.DefaultSpeedIndex;
        public SimRng Rng { get; }
        public SimEvents Events { get; } = new SimEvents();

        public void AddSystem(SimSystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            _systems.Add(system);
        }

        public List<SimSystem> GetSystems()
        {
            return new List<SimSystem>(_systems);
        }

        public double GetSpeed()
        {
            return SimConst.SpeedSteps[SpeedIndex];
        }

        public void SetSpeedIndex(int index)
        {
            int clamped = SimMath.Clamp(index, 0, SimConst.SpeedSteps.Count - 1);
            if (clamped == SpeedIndex)
            {
                return;
            }

            SpeedIndex = clamped;
            Events.EmitEvent("speed_changed", Tick, new Dictionary<string, object> { ["speed"] = GetSpeed() });
        }

        public void SpeedUp()
        {
            SetSpeedIndex(SpeedIndex + 1);
        }

        public void SpeedDown()
        {
            SetSpeedIndex(SpeedIndex - 1);
        }

        public void SetPaused(bool value)
        {
            if (Paused == value)
            {
                return;
            }

            Paused = value;
            Events.EmitEvent("paused_changed", Tick, new Dictionary<string, object> { ["paused"] = Paused });
        }

        public void TogglePause()
        {
            SetPaused(!Paused);
        }

        public int Advance(double realDelta)
        {
            if (Paused)
            {
                return 0;
            }

            _accum += Math.Max(realDelta, 0.0) * GetSpeed();
            int ran = 0;
            while (_accum + SimConst.TickEpsilon >= SimConst.TickDt && ran < SimConst.MaxTicksPerAdvance)
            {
                StepTick();
                _accum = Math.Max(_accum - SimConst.TickDt, 0.0);
                ran++;
            }

            if (ran >= SimConst.MaxTicksPerAdvance)
            {
                _accum = 0.0;
            }

            return ran;
        }

        public void StepTick()
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Step(this);
            }

            Tick++;
        }

        public double GetInterpolationAlpha()
        {
            return SimMath.Clamp(_accum / SimConst.TickDt, 0.0, 1.0);
        }

        public double GetSimTimeSec()
        {
            return Tick * SimConst.TickDt;
        }

        public long StateHash()
        {
            long hash = SimHash.Append(SimHash.Initial, Tick);
            hash = SimHash.Append(hash, Rng.SeedValue);
            hash = SimHash.Append(hash, Rng.GetState());
            for (int i = 0; i < _systems.Count; i++)
            {
                SimSystem system = _systems[i];
                if (!system.OmitFromHash())
                {
                    hash = SimHash.Append(hash, system.StateHash());
                }
            }

            return hash;
        }
    }
}
