using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Fx
{
    public sealed class FxEffectSystem
    {
        public const uint DefaultSeed = 0x5EEDu;
        public const int StaleTicks = 30;
        public const float TicksPerSecond = 10.0f;
        public const float WreckSeconds = 120.0f;
        public const float ScorchSeconds = 240.0f;

        private static readonly Vector3 Wind = new Vector3(2.2f, 0.0f, 1.2f);
        private static readonly float[] ShellScale = { 0.85f, 1.2f, 1.6f };

        private sealed class Projectile
        {
            internal Vector3 A;
            internal Vector3 B;
            internal Vector3 Head;
            internal Vector3 TailPosition;
            internal float Age;
            internal float Duration = 0.3f;
            internal string Weapon;
            internal bool Hit;
            internal Color Color;
            internal float TrailAccumulator;
            internal float Tail = 0.3f;
            internal bool ShowImpact = true;
        }

        private sealed class Wreck
        {
            internal Vector3 Position;
            internal float Age;
            internal float FireAccumulator;
            internal float SmokeAccumulator;
        }

        private sealed class ScreenEmitter
        {
            internal Vector3 Center;
            internal float Accumulator;
            internal bool Started;
        }

        private sealed class Flash
        {
            internal bool Active;
            internal Vector3 Position;
            internal float Age;
            internal float Life;
            internal float Energy;
            internal float Range;
        }

        private struct Scorch
        {
            internal Vector3 Position;
            internal float Radius;
            internal float Rotation;
            internal float Seed;
            internal float Age;
        }

        private readonly FxParticlePool[] _layers;
        private readonly List<FxEvent> _eventQueue = new List<FxEvent>();
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly List<Wreck> _wrecks = new List<Wreck>();
        private readonly List<Flash> _flashes = new List<Flash>();
        private readonly List<FxSmoke> _smokeCircles = new List<FxSmoke>();
        private readonly Dictionary<int, ScreenEmitter> _screens = new Dictionary<int, ScreenEmitter>();
        private readonly HashSet<int> _aliveSmokeIds = new HashSet<int>();
        private readonly List<int> _obsoleteSmokeIds = new List<int>();
        private readonly Scorch[] _scorches;
        private readonly FxRandom _random;
        private int _scorchNext;

        public FxCaps Caps { get; }
        public Func<Vector3, float> GroundHeight { get; set; }
        public int CurrentTick { get; private set; }
        public int QueuedEventCount { get { return _eventQueue.Count; } }
        public int ProjectileCount { get { return _projectiles.Count; } }
        public int BurningWreckCount { get { return _wrecks.Count; } }
        public int SmokeEmitterCount { get { return _screens.Count; } }
        public int FlashLightSlotCount { get { return _flashes.Count; } }
        public int ScorchSlotCount { get { return _scorches.Length; } }
        public long DroppedParticleCount { get; private set; }
        public uint PresentationRandomState { get { return _random.State; } }

        public int SmokeParticleCount { get { return _layers[(int)FxParticleLayer.Smoke].Count; } }
        public int FireParticleCount { get { return _layers[(int)FxParticleLayer.Fire].Count; } }

        public int ActiveScorchCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _scorches.Length; i++)
                {
                    if (_scorches[i].Age < ScorchSeconds)
                        count++;
                }
                return count;
            }
        }

        public int ActiveFlashLightCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _flashes.Count; i++)
                {
                    if (_flashes[i].Active)
                        count++;
                }
                return count;
            }
        }

        public FxEffectSystem() : this(FxQuality.Auto)
        {
        }

        public FxEffectSystem(FxQuality quality) : this(FxCaps.ForQuality(quality))
        {
        }

        public FxEffectSystem(FxCaps caps)
        {
            caps.Validate();
            Caps = caps;
            _layers = new[]
            {
                new FxParticlePool(caps.SmokeParticles),
                new FxParticlePool(caps.FireParticles)
            };
            _scorches = new Scorch[caps.ScorchMarks];
            _random = new FxRandom(DefaultSeed);
            Reset();
        }

        public void Reset(uint seed = DefaultSeed)
        {
            _random.Reset(seed);
            _eventQueue.Clear();
            _projectiles.Clear();
            _wrecks.Clear();
            _flashes.Clear();
            _smokeCircles.Clear();
            _screens.Clear();
            _layers[0].Clear();
            _layers[1].Clear();
            _scorchNext = 0;
            CurrentTick = 0;
            DroppedParticleCount = 0;
            for (int i = 0; i < _scorches.Length; i++)
            {
                _scorches[i] = new Scorch
                {
                    Position = new Vector3(0.0f, -1000.0f, 0.0f),
                    Radius = 0.001f,
                    Age = ScorchSeconds
                };
            }
        }

        public void Enqueue(FxEvent fxEvent)
        {
            if (fxEvent == null)
                throw new ArgumentNullException("fxEvent");
            _eventQueue.Add(fxEvent.Snapshot());
        }

        public void SetSmokeCircles(IEnumerable<FxSmoke> smokeCircles)
        {
            _smokeCircles.Clear();
            if (smokeCircles == null)
                return;
            foreach (FxSmoke smoke in smokeCircles)
                _smokeCircles.Add(smoke);
        }

        public void Update(float dt, int tick)
        {
            CurrentTick = tick;
            dt = Mathf.Max(0.0f, dt);
            for (int i = 0; i < _eventQueue.Count; i++)
            {
                FxEvent fxEvent = _eventQueue[i];
                if (fxEvent.Tick < 0)
                    fxEvent.Tick = tick;
                ProcessEvent(fxEvent, tick);
            }
            _eventQueue.Clear();

            UpdateProjectiles(dt);
            if (dt <= 0.0f)
                return;

            UpdateWrecks(dt);
            UpdateScreens(dt, tick);
            UpdateLights(dt);
            UpdateScorches(dt);
            _layers[0].Step(dt, Wind);
            _layers[1].Step(dt, Wind);
        }

        public FxParticleState GetParticle(FxParticleLayer layer, int index)
        {
            if (layer != FxParticleLayer.Smoke && layer != FxParticleLayer.Fire)
                throw new ArgumentOutOfRangeException("layer");
            return _layers[(int)layer].GetState(index);
        }

        public FxProjectileState GetProjectile(int index)
        {
            Projectile projectile = _projectiles[index];
            return new FxProjectileState(projectile.A, projectile.B, projectile.Head, projectile.TailPosition,
                projectile.Color, projectile.Weapon, projectile.Age, projectile.Duration, projectile.Tail,
                projectile.TrailAccumulator, projectile.Hit, projectile.ShowImpact);
        }

        public FxWreckState GetWreck(int index)
        {
            Wreck wreck = _wrecks[index];
            return new FxWreckState(wreck.Position, wreck.Age);
        }

        public FxScorchState GetScorchSlot(int index)
        {
            Scorch scorch = _scorches[index];
            return new FxScorchState(scorch.Position, scorch.Radius, scorch.Rotation,
                scorch.Seed, scorch.Age, ScorchAlpha(scorch.Age));
        }

        public FxFlashState GetFlashLight(int index)
        {
            Flash flash = _flashes[index];
            float intensity = flash.Active
                ? flash.Energy * Mathf.Clamp01(1.0f - flash.Age / flash.Life)
                : 0.0f;
            return new FxFlashState(flash.Active, flash.Position, new Color(1.0f, 0.65f, 0.3f, 1.0f),
                flash.Range, intensity, flash.Age, flash.Life);
        }

        private void ProcessEvent(FxEvent fxEvent, int tick)
        {
            if (tick - fxEvent.Tick > StaleTicks)
            {
                if (fxEvent.Type == FxEventType.ElementDestroyed)
                {
                    Wreck wreck = new Wreck
                    {
                        Position = Ground(fxEvent.Position, 2.0f),
                        Age = (tick - fxEvent.Tick) / TicksPerSecond
                    };
                    AddWreck(wreck);
                }
                return;
            }

            switch (fxEvent.Type)
            {
                case FxEventType.Shot:
                    OnShot(fxEvent);
                    break;
                case FxEventType.ElementDestroyed:
                    OnElementDestroyed(fxEvent);
                    break;
                case FxEventType.Shell:
                    if (fxEvent.Smoke)
                        SmokeShellBurst(fxEvent.Position);
                    else
                        ShellImpact(fxEvent.Position, fxEvent.Asset);
                    break;
                case FxEventType.IndirectFire:
                    if (fxEvent.SourceId >= 0 && fxEvent.SourceVisible)
                        ArtilleryMuzzle(fxEvent.Position, fxEvent.SourceIsMortar);
                    break;
                case FxEventType.SamLaunch:
                    OnSamLaunch(fxEvent);
                    break;
                case FxEventType.MineStrike:
                    MineStrike(fxEvent.Position);
                    break;
                case FxEventType.CasShotDown:
                    CasShotDown(fxEvent.Position);
                    break;
            }
        }

        private void OnShot(FxEvent fxEvent)
        {
            if (fxEvent.SourceId < 0)
                return;

            bool shooterSeen = fxEvent.SourceVisible;
            bool targetSeen = fxEvent.TargetId >= 0 && fxEvent.TargetVisible;
            if (!shooterSeen && !targetSeen)
                return;

            string weapon = string.IsNullOrEmpty(fxEvent.Weapon) ? FxWeapon.MG : fxEvent.Weapon.ToUpperInvariant();
            Vector3 from = fxEvent.SourcePosition;
            Vector3 target = fxEvent.TargetPosition;
            Vector3 delta = target - from;
            delta.y = 0.0f;
            float distance = Mathf.Max(delta.magnitude, 1.0f);
            Vector3 direction2 = delta / distance;
            bool infantry = fxEvent.SourceIsInfantry;
            float muzzleHeight = (infantry ? 1.3f : 2.9f) + fxEvent.SourceAltitude;
            float muzzleForward = infantry ? 0.5f : 5.0f;
            Vector3 a = Ground(from + direction2 * muzzleForward, muzzleHeight);
            float targetHeight = fxEvent.TargetId >= 0 && fxEvent.TargetIsInfantry ? 1.0f : 1.8f;
            if (fxEvent.TargetId >= 0)
                targetHeight += fxEvent.TargetAltitude;
            Vector3 b = Ground(target, targetHeight);

            if (!fxEvent.Hit)
            {
                float spread = weapon == FxWeapon.MG ? 6.0f : 12.0f;
                float along = _random.Range(8.0f, 45.0f);
                float lateral = _random.Range(-spread, spread);
                Vector3 perpendicular = new Vector3(direction2.z, 0.0f, -direction2.x);
                b = Ground(target + direction2 * along + perpendicular * lateral, 0.2f);
            }

            Vector3 direction3 = (b - a).normalized;
            if (shooterSeen)
                Muzzle(fxEvent, weapon, a, direction3, muzzleHeight);

            Projectile projectile = new Projectile
            {
                A = a,
                B = b,
                Weapon = weapon,
                Hit = fxEvent.Hit,
                Color = TracerColor(fxEvent),
                ShowImpact = targetSeen || !fxEvent.Hit
            };

            switch (weapon)
            {
                case FxWeapon.KE:
                    projectile.Duration = Mathf.Clamp(distance / 1650.0f, 0.08f, 2.0f);
                    projectile.Tail = 0.25f;
                    break;
                case FxWeapon.HEAT:
                case FxWeapon.HE:
                    projectile.Duration = Mathf.Clamp(distance / 900.0f, 0.1f, 2.5f);
                    projectile.Tail = 0.2f;
                    break;
                case FxWeapon.ATGM:
                    projectile.Duration = Mathf.Clamp(distance / 450.0f, 0.4f, 4.0f);
                    projectile.Tail = 0.05f;
                    projectile.Color = new Color(1.0f, 0.9f, 0.7f, 1.0f);
                    break;
                case FxWeapon.AC:
                    projectile.Duration = Mathf.Clamp(distance / 1100.0f, 0.08f, 2.0f);
                    projectile.Tail = 0.3f;
                    break;
                default:
                    projectile.Duration = Mathf.Clamp(distance / 850.0f, 0.06f, 1.6f);
                    projectile.Tail = 0.35f;
                    break;
            }

            if (!shooterSeen)
                projectile.Age = projectile.Duration;
            _projectiles.Add(projectile);
        }

        private void OnElementDestroyed(FxEvent fxEvent)
        {
            if (fxEvent.DestroyedIsHelicopter && fxEvent.DestroyedHasPlatoon)
            {
                Vector3 air = Ground(fxEvent.Position, fxEvent.DestroyedAltitude);
                EmitFlash(air, 14.0f, 0.2f);
                Fireball(air, 1.2f);
                Sparks(air, 14, 1.4f);
            }
            ExplodeVehicle(fxEvent.Position);
        }

        private void OnSamLaunch(FxEvent fxEvent)
        {
            if (fxEvent.SourceId < 0 || !fxEvent.SourceVisible)
                return;
            Vector3 a = Ground(fxEvent.Position, 3.0f);
            Vector3 b = Ground(fxEvent.TargetPosition, 450.0f);
            _projectiles.Add(new Projectile
            {
                A = a,
                B = b,
                Duration = 2.5f,
                Weapon = FxWeapon.SAM,
                Hit = false,
                Color = new Color(1.0f, 0.95f, 0.8f, 1.0f),
                ShowImpact = false
            });
            MuzzleSmoke(a, Vector3.up, 4, 1.0f);
        }

        private void Muzzle(FxEvent fxEvent, string weapon, Vector3 a, Vector3 direction, float muzzleHeight)
        {
            switch (weapon)
            {
                case FxWeapon.KE:
                case FxWeapon.HEAT:
                case FxWeapon.HE:
                    if (fxEvent.SourceIsInfantry)
                    {
                        EmitFlash(a, 2.0f, 0.08f);
                        MuzzleSmoke(a - direction * 1.5f, -direction, 4, 0.8f);
                    }
                    else
                    {
                        EmitFlash(a, 5.5f, 0.1f);
                        BurstDirectional(FxParticleLayer.Fire, a, direction, 3, 18.0f, 30.0f, 2.5f, 1.0f,
                            new Color(1.0f, 0.8f, 0.4f, 1.0f), new Color(1.0f, 0.3f, 0.05f, 0.0f), 0.1f, 0.16f);
                        MuzzleSmoke(a, direction, 6, 1.4f);
                        Vector3 ground = new Vector3(a.x, a.y - muzzleHeight, a.z);
                        GroundBlast(ground, 7, 1.0f);
                        Light(a, 3.0f, 35.0f, 0.12f);
                    }
                    break;
                case FxWeapon.ATGM:
                    EmitFlash(a, 3.0f, 0.12f);
                    MuzzleSmoke(a, -direction, 5, 1.1f);
                    break;
                case FxWeapon.AC:
                    EmitFlash(a, 2.2f, 0.06f);
                    MuzzleSmoke(a, direction, 1, 0.5f);
                    break;
                case FxWeapon.SAM:
                    EmitFlash(a, 3.0f, 0.12f);
                    break;
                default:
                    EmitFlash(a, fxEvent.SourceIsInfantry ? 1.0f : 1.4f, 0.05f);
                    break;
            }
        }

        private void Impact(Projectile projectile)
        {
            Vector3 p = projectile.B;
            if (projectile.Hit)
            {
                switch (projectile.Weapon)
                {
                    case FxWeapon.KE:
                        EmitFlash(p, 4.5f, 0.12f);
                        Sparks(p, 10, 1.0f);
                        Burst(FxParticleLayer.Smoke, p, 2, 1.5f, 3.0f, 2.0f, 7.0f,
                            new Color(0.25f, 0.24f, 0.22f, 0.6f), new Color(0.35f, 0.34f, 0.32f, 0.0f),
                            2.0f, 3.5f, 1.2f, -0.5f);
                        break;
                    case FxWeapon.HEAT:
                    case FxWeapon.ATGM:
                        EmitFlash(p, 6.0f, 0.14f);
                        Fireball(p, 0.6f);
                        Sparks(p, 6, 0.8f);
                        Burst(FxParticleLayer.Smoke, p, 4, 2.0f, 4.0f, 3.0f, 10.0f,
                            new Color(0.08f, 0.08f, 0.07f, 0.7f), new Color(0.28f, 0.27f, 0.26f, 0.0f),
                            3.0f, 5.0f, 0.8f, -0.8f);
                        Light(p, 4.0f, 45.0f, 0.2f);
                        break;
                    case FxWeapon.HE:
                        EmitFlash(p, 5.0f, 0.12f);
                        Fireball(p, 0.45f);
                        Burst(FxParticleLayer.Smoke, p, 3, 2.0f, 5.0f, 2.5f, 9.0f,
                            new Color(0.2f, 0.19f, 0.17f, 0.65f), new Color(0.4f, 0.38f, 0.35f, 0.0f),
                            2.5f, 4.0f, 0.8f, -0.5f);
                        break;
                    case FxWeapon.AC:
                        EmitFlash(p, 2.2f, 0.06f);
                        Sparks(p, 4, 0.6f);
                        Burst(FxParticleLayer.Smoke, p, 1, 1.0f, 2.0f, 1.2f, 4.0f,
                            new Color(0.3f, 0.29f, 0.27f, 0.5f), new Color(0.4f, 0.4f, 0.38f, 0.0f),
                            1.5f, 2.5f, 1.2f, 0.0f);
                        break;
                    default:
                        Sparks(p, 2, 0.4f);
                        break;
                }
            }
            else
            {
                switch (projectile.Weapon)
                {
                    case FxWeapon.KE:
                    case FxWeapon.HEAT:
                    case FxWeapon.ATGM:
                    case FxWeapon.HE:
                        float big = projectile.Weapon != FxWeapon.KE ? 1.3f : 1.0f;
                        if (projectile.Weapon != FxWeapon.KE)
                            EmitFlash(p, 4.0f, 0.1f);
                        Dirt(p, 8, big);
                        Burst(FxParticleLayer.Smoke, p, 4, 3.0f, 7.0f, 2.0f * big, 8.0f * big,
                            new Color(0.42f, 0.36f, 0.28f, 0.75f), new Color(0.52f, 0.48f, 0.42f, 0.0f),
                            2.0f, 3.5f, 0.9f, 1.0f);
                        break;
                    case FxWeapon.AC:
                        Dirt(p, 2, 0.5f);
                        Burst(FxParticleLayer.Smoke, p, 1, 2.0f, 4.0f, 1.0f, 3.5f,
                            new Color(0.45f, 0.4f, 0.32f, 0.6f), new Color(0.5f, 0.47f, 0.42f, 0.0f),
                            1.2f, 2.0f, 1.0f, 1.0f);
                        break;
                    case FxWeapon.SAM:
                        break;
                    default:
                        if (_random.NextFloat() < 0.5f)
                        {
                            Burst(FxParticleLayer.Smoke, p, 1, 1.5f, 3.0f, 0.5f, 1.8f,
                                new Color(0.45f, 0.4f, 0.32f, 0.55f), new Color(0.5f, 0.47f, 0.42f, 0.0f),
                                0.6f, 1.0f, 1.0f, 2.0f);
                        }
                        break;
                }
            }
        }

        private void ExplodeVehicle(Vector3 groundPosition)
        {
            Vector3 p = Ground(groundPosition, 2.0f);
            EmitFlash(p, 14.0f, 0.2f);
            Fireball(p, 1.3f);
            Sparks(p + Vector3.up, 16, 1.6f);
            Burst(FxParticleLayer.Smoke, p + Vector3.up * 2.0f, 8, 3.0f, 9.0f, 3.0f, 14.0f,
                new Color(0.06f, 0.06f, 0.05f, 0.8f), new Color(0.25f, 0.24f, 0.23f, 0.0f),
                5.0f, 8.0f, 0.5f, -1.0f);
            Light(p, 10.0f, 80.0f, 0.45f);
            AddWreck(new Wreck { Position = p });
        }

        private void AddWreck(Wreck wreck)
        {
            if (wreck.Age >= WreckSeconds)
                return;
            _wrecks.Add(wreck);
            if (_wrecks.Count > Caps.BurningWrecks)
                _wrecks.RemoveAt(0);
        }

        private void ShellImpact(Vector3 groundPosition, int asset)
        {
            float scale = ShellScale[Mathf.Clamp(asset, 0, 2)];
            Vector3 p = Ground(groundPosition, 0.5f);
            EmitFlash(p + Vector3.up * 3.0f, 20.0f * scale, 0.18f);
            Fireball(p + Vector3.up * 1.5f, 1.1f * scale);
            Dirt(p, Mathf.RoundToInt(26.0f * scale), 2.0f * scale);

            int pillars = Mathf.RoundToInt(12.0f * scale);
            for (int i = 0; i < pillars; i++)
            {
                float angle = _random.NextFloat() * Mathf.PI * 2.0f;
                float horizontal = _random.Range(0.5f, 4.0f) * scale;
                Vector3 velocity = new Vector3(Mathf.Cos(angle) * horizontal,
                    _random.Range(8.0f, 26.0f) * scale, Mathf.Sin(angle) * horizontal);
                Emit(FxParticleLayer.Smoke, p + Vector3.up, velocity, _random.Range(3.5f, 6.5f),
                    3.0f * scale, _random.Range(10.0f, 16.0f) * scale,
                    new Color(0.3f, 0.25f, 0.19f, 0.95f), new Color(0.52f, 0.49f, 0.44f, 0.0f),
                    0.9f, 0.0f);
            }

            int ringCount = Mathf.RoundToInt(16.0f * scale);
            for (int i = 0; i < ringCount; i++)
            {
                float angle = Mathf.PI * 2.0f * i / ringCount + _random.Range(-0.15f, 0.15f);
                Vector3 velocity = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle))
                    * (_random.Range(20.0f, 32.0f) * scale)
                    + Vector3.up * _random.Range(0.5f, 2.5f);
                Emit(FxParticleLayer.Smoke, p + Vector3.up * 1.5f, velocity, _random.Range(3.0f, 5.0f),
                    3.5f * scale, 13.0f * scale,
                    new Color(0.48f, 0.43f, 0.35f, 0.75f), new Color(0.58f, 0.55f, 0.5f, 0.0f),
                    1.5f, 0.0f);
            }

            Burst(FxParticleLayer.Smoke, p + Vector3.up * 6.0f, 4, 0.5f, 2.5f,
                8.0f * scale, 24.0f * scale, new Color(0.36f, 0.35f, 0.33f, 0.45f),
                new Color(0.45f, 0.45f, 0.44f, 0.0f), 12.0f, 18.0f, 0.4f, -0.4f);
            Light(p + Vector3.up * 3.0f, 10.0f * scale, 110.0f * scale, 0.35f);
            AddScorch(groundPosition, 9.0f * scale);
        }

        private void ArtilleryMuzzle(Vector3 groundPosition, bool mortar)
        {
            Vector3 p = Ground(groundPosition, 3.0f);
            float scale = mortar ? 0.6f : 1.0f;
            EmitFlash(p + Vector3.up * 2.0f, 7.0f * scale, 0.12f);
            MuzzleSmoke(p + Vector3.up * 2.0f, Vector3.up, Mathf.RoundToInt(6.0f * scale) + 1, 1.6f * scale);
            GroundBlast(groundPosition, Mathf.RoundToInt(8.0f * scale), 1.2f * scale);
            Light(p, 4.0f, 50.0f * scale, 0.15f);
        }

        private void SmokeShellBurst(Vector3 groundPosition)
        {
            Vector3 p = Ground(groundPosition, 2.0f);
            EmitFlash(p, 4.0f, 0.1f);
            Burst(FxParticleLayer.Smoke, p, 6, 3.0f, 8.0f, 4.0f, 14.0f,
                new Color(0.9f, 0.9f, 0.9f, 0.85f), new Color(0.86f, 0.87f, 0.88f, 0.0f),
                3.0f, 6.0f, 0.5f, -0.3f);
        }

        private void MineStrike(Vector3 groundPosition)
        {
            Vector3 ground = Ground(groundPosition, 0.5f);
            EmitFlash(ground + Vector3.up * 2.0f, 10.0f, 0.14f);
            Dirt(ground, 14, 1.2f);
            Burst(FxParticleLayer.Smoke, ground + Vector3.up * 2.0f, 6, 3.0f, 9.0f, 3.0f, 12.0f,
                new Color(0.32f, 0.27f, 0.21f, 0.85f), new Color(0.5f, 0.47f, 0.42f, 0.0f),
                4.0f, 6.0f, 0.6f, -0.3f);
            AddScorch(groundPosition, 6.0f);
            Light(ground + Vector3.up * 2.0f, 6.0f, 60.0f, 0.25f);
        }

        private void CasShotDown(Vector3 groundPosition)
        {
            Vector3 air = Ground(groundPosition, 450.0f);
            Fireball(air, 1.4f);
            Burst(FxParticleLayer.Smoke, air, 8, 4.0f, 18.0f, 3.0f, 10.0f,
                new Color(0.1f, 0.1f, 0.1f, 0.7f), new Color(0.3f, 0.3f, 0.3f, 0.0f),
                5.0f, 8.0f, 0.4f, 0.0f);
        }

        private void EmitFlash(Vector3 position, float size, float life)
        {
            Emit(FxParticleLayer.Fire, position, Vector3.zero, life, size, size * 1.3f,
                new Color(1.0f, 0.85f, 0.55f, 1.0f), new Color(1.0f, 0.45f, 0.1f, 0.0f), 0.0f, 0.0f);
        }

        private void Fireball(Vector3 position, float scale)
        {
            int count = Mathf.RoundToInt(6.0f * scale) + 2;
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = RandomDirection(0.3f) * (_random.Range(3.0f, 9.0f) * scale)
                    + Vector3.up * (3.0f * scale);
                Vector3 positionOffset = RandomDirection(0.0f) * (1.5f * scale);
                Emit(FxParticleLayer.Fire, position + positionOffset, velocity,
                    _random.Range(0.5f, 1.0f) * Mathf.Max(scale, 0.6f), 2.5f * scale, 7.0f * scale,
                    new Color(1.0f, 0.7f, 0.3f, 1.0f), new Color(0.55f, 0.12f, 0.02f, 0.0f),
                    1.5f, -2.0f);
            }
        }

        private void Sparks(Vector3 position, int count, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = RandomDirection(0.1f) * (_random.Range(12.0f, 30.0f) * scale);
                Emit(FxParticleLayer.Fire, position, velocity, _random.Range(0.3f, 0.8f),
                    0.7f, 0.25f, new Color(1.0f, 0.9f, 0.6f, 1.0f),
                    new Color(1.0f, 0.4f, 0.1f, 0.0f), 0.2f, 20.0f);
            }
        }

        private void Dirt(Vector3 position, int count, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = _random.NextFloat() * Mathf.PI * 2.0f;
                float horizontal = _random.Range(2.0f, 9.0f) * scale;
                Vector3 velocity = new Vector3(Mathf.Cos(angle) * horizontal,
                    _random.Range(16.0f, 32.0f) * Mathf.Sqrt(scale), Mathf.Sin(angle) * horizontal);
                Emit(FxParticleLayer.Smoke, position, velocity, _random.Range(1.4f, 2.4f),
                    1.4f * scale, 3.6f * scale, new Color(0.16f, 0.12f, 0.09f, 1.0f),
                    new Color(0.3f, 0.26f, 0.2f, 0.0f), 0.15f, 24.0f);
            }
        }

        private void MuzzleSmoke(Vector3 position, Vector3 direction, int count, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = direction * (_random.Range(4.0f, 12.0f) * scale)
                    + RandomDirection(0.0f) * 2.0f;
                Emit(FxParticleLayer.Smoke, position, velocity, _random.Range(2.0f, 3.5f) * scale,
                    1.5f * scale, 7.0f * scale, new Color(0.6f, 0.58f, 0.55f, 0.6f),
                    new Color(0.66f, 0.65f, 0.63f, 0.0f), 1.4f, -0.3f);
            }
        }

        private void GroundBlast(Vector3 groundPosition, int count, float scale)
        {
            Vector3 p = Ground(groundPosition, 0.8f);
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2.0f * i / count + _random.Range(-0.3f, 0.3f);
                Vector3 velocity = new Vector3(Mathf.Cos(angle), 0.15f, Mathf.Sin(angle))
                    * (_random.Range(7.0f, 12.0f) * scale);
                Emit(FxParticleLayer.Smoke, p, velocity, _random.Range(1.4f, 2.4f),
                    2.5f * scale, 8.0f * scale, new Color(0.55f, 0.5f, 0.41f, 0.5f),
                    new Color(0.6f, 0.57f, 0.52f, 0.0f), 1.8f, 0.0f);
            }
        }

        private void Burst(FxParticleLayer layer, Vector3 position, int count, float velocityMin,
            float velocityMax, float size0, float size1, Color color0, Color color1,
            float lifeMin, float lifeMax, float drag, float gravity)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = RandomDirection(0.0f) * _random.Range(velocityMin, velocityMax);
                Emit(layer, position, velocity, _random.Range(lifeMin, lifeMax), size0, size1,
                    color0, color1, drag, gravity);
            }
        }

        private void BurstDirectional(FxParticleLayer layer, Vector3 position, Vector3 direction,
            int count, float velocityMin, float velocityMax, float size0, float size1,
            Color color0, Color color1, float lifeMin, float lifeMax)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = (direction + RandomDirection(-1.0f) * 0.2f).normalized
                    * _random.Range(velocityMin, velocityMax);
                Emit(layer, position, velocity, _random.Range(lifeMin, lifeMax), size0, size1,
                    color0, color1, 0.0f, 0.0f);
            }
        }

        private Vector3 RandomDirection(float minUp)
        {
            float y = _random.Range(Mathf.Max(minUp, -1.0f), 1.0f);
            float angle = _random.NextFloat() * Mathf.PI * 2.0f;
            float radius = Mathf.Sqrt(Mathf.Max(1.0f - y * y, 0.0f));
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        private void Emit(FxParticleLayer layer, Vector3 position, Vector3 velocity, float life,
            float size0, float size1, Color color0, Color color1, float drag, float gravity)
        {
            FxParticlePool pool = _layers[(int)layer];
            if (pool.Count >= pool.Capacity)
            {
                DroppedParticleCount++;
                return;
            }
            pool.Add(position, velocity, life, size0, size1, color0, color1, drag, gravity,
                _random.NextFloat() * Mathf.PI * 2.0f, _random.Range(-0.6f, 0.6f), _random.NextFloat());
        }

        private void AddScorch(Vector3 groundPosition, float radius)
        {
            if (_scorches.Length == 0)
                return;
            int index = _scorchNext;
            _scorchNext = (_scorchNext + 1) % _scorches.Length;
            float height = SampleGround(groundPosition);
            Vector3 offset = new Vector3(radius, 0.0f, 0.0f);
            height = Mathf.Max(height, SampleGround(groundPosition + offset));
            height = Mathf.Max(height, SampleGround(groundPosition - offset));
            offset = new Vector3(0.0f, 0.0f, radius);
            height = Mathf.Max(height, SampleGround(groundPosition + offset));
            height = Mathf.Max(height, SampleGround(groundPosition - offset));
            _scorches[index] = new Scorch
            {
                Position = new Vector3(groundPosition.x, height + 0.25f, groundPosition.z),
                Radius = radius,
                Rotation = _random.NextFloat() * Mathf.PI * 2.0f,
                Seed = _random.NextFloat(),
                Age = 0.0f
            };
        }

        private void Light(Vector3 position, float energy, float range, float life)
        {
            if (Caps.FlashLights <= 0)
                return;
            Flash flash;
            if (_flashes.Count >= Caps.FlashLights)
            {
                flash = _flashes[0];
                _flashes.RemoveAt(0);
            }
            else
            {
                flash = new Flash();
            }

            flash.Position = position + Vector3.up * 2.0f;
            flash.Range = range;
            flash.Energy = energy;
            flash.Life = life;
            flash.Age = 0.0f;
            flash.Active = true;
            _flashes.Add(flash);
        }

        private void UpdateProjectiles(float dt)
        {
            int write = 0;
            int originalCount = _projectiles.Count;
            for (int i = 0; i < originalCount; i++)
            {
                Projectile projectile = _projectiles[i];
                projectile.Age += dt;
                float k = projectile.Age / projectile.Duration;
                if (k >= 1.0f)
                {
                    if (projectile.ShowImpact)
                        Impact(projectile);
                    continue;
                }

                if (write != i)
                    _projectiles[write] = projectile;
                write++;
                Vector3 head = Vector3.Lerp(projectile.A, projectile.B, k);
                if (projectile.Weapon == FxWeapon.SAM || projectile.Weapon == FxWeapon.ATGM)
                {
                    if (projectile.Weapon == FxWeapon.SAM)
                        head = Vector3.Lerp(projectile.A, projectile.B, k * k);
                    else
                        head.y += Mathf.Sin(k * Mathf.PI) * Mathf.Min(Vector3.Distance(projectile.A, projectile.B) * 0.01f, 12.0f);
                    projectile.Head = head;
                    projectile.TailPosition = head;
                    if (dt > 0.0f)
                    {
                        Emit(FxParticleLayer.Fire, head, Vector3.zero, 0.05f, 1.8f, 1.2f,
                            new Color(1.0f, 0.9f, 0.7f, 1.0f), new Color(1.0f, 0.6f, 0.2f, 0.0f),
                            0.0f, 0.0f);
                        projectile.TrailAccumulator += dt * 35.0f;
                        while (projectile.TrailAccumulator >= 1.0f)
                        {
                            projectile.TrailAccumulator -= 1.0f;
                            Emit(FxParticleLayer.Smoke, head, RandomDirection(-1.0f) * 0.6f,
                                _random.Range(1.8f, 3.0f), 0.8f, 3.5f,
                                new Color(0.85f, 0.85f, 0.83f, 0.55f), new Color(0.8f, 0.8f, 0.8f, 0.0f),
                                1.0f, -0.2f);
                        }
                    }
                    continue;
                }

                projectile.Head = head;
                projectile.TailPosition = Vector3.Lerp(projectile.A, projectile.B, Mathf.Max(0.0f, k - projectile.Tail));
                if (dt > 0.0f && projectile.Weapon != FxWeapon.MG)
                {
                    float glow = projectile.Weapon == FxWeapon.AC ? 1.6f : 2.6f;
                    Color color = projectile.Color;
                    Emit(FxParticleLayer.Fire, head, Vector3.zero, 0.04f, glow, glow,
                        new Color(color.r, color.g, color.b, 0.9f), new Color(color.r, color.g, color.b, 0.0f),
                        0.0f, 0.0f);
                }
            }
            if (write < originalCount)
                _projectiles.RemoveRange(write, originalCount - write);
        }

        private void UpdateWrecks(float dt)
        {
            int write = 0;
            int originalCount = _wrecks.Count;
            for (int i = 0; i < originalCount; i++)
            {
                Wreck wreck = _wrecks[i];
                wreck.Age += dt;
                if (wreck.Age >= WreckSeconds)
                    continue;
                if (write != i)
                    _wrecks[write] = wreck;
                write++;

                float fireRate = 10.0f * Mathf.Clamp01(1.0f - (wreck.Age - 20.0f) / 25.0f);
                float smokeRate = 3.2f * Caps.WreckSmokeScale
                    * Mathf.Clamp(1.0f - (wreck.Age - 40.0f) / 80.0f, 0.15f, 1.0f);
                wreck.FireAccumulator += fireRate * dt;
                while (wreck.FireAccumulator >= 1.0f)
                {
                    wreck.FireAccumulator -= 1.0f;
                    Vector3 offset = new Vector3(_random.Range(-1.5f, 1.5f), 0.0f, _random.Range(-1.5f, 1.5f));
                    Emit(FxParticleLayer.Fire, wreck.Position + offset,
                        new Vector3(0.0f, _random.Range(2.0f, 4.5f), 0.0f), _random.Range(0.35f, 0.7f),
                        _random.Range(1.2f, 2.2f), 0.4f, new Color(1.0f, 0.62f, 0.22f, 0.9f),
                        new Color(0.8f, 0.18f, 0.04f, 0.0f), 0.5f, -1.0f);
                }

                wreck.SmokeAccumulator += smokeRate * dt;
                while (wreck.SmokeAccumulator >= 1.0f)
                {
                    wreck.SmokeAccumulator -= 1.0f;
                    Vector3 offset = new Vector3(_random.Range(-1.0f, 1.0f), 1.5f, _random.Range(-1.0f, 1.0f));
                    Emit(FxParticleLayer.Smoke, wreck.Position + offset,
                        new Vector3(0.0f, _random.Range(3.0f, 5.0f), 0.0f), _random.Range(6.0f, 9.0f),
                        2.2f, _random.Range(11.0f, 16.0f), new Color(0.06f, 0.055f, 0.05f, 0.75f),
                        new Color(0.24f, 0.23f, 0.22f, 0.0f), 0.35f, -0.6f);
                }
            }
            if (write < originalCount)
                _wrecks.RemoveRange(write, originalCount - write);
        }

        private void UpdateScreens(float dt, int tick)
        {
            _aliveSmokeIds.Clear();
            for (int i = 0; i < _smokeCircles.Count; i++)
            {
                FxSmoke smoke = _smokeCircles[i];
                if (smoke.UntilTick <= tick)
                    continue;
                _aliveSmokeIds.Add(smoke.Id);
                ScreenEmitter emitter;
                if (!_screens.TryGetValue(smoke.Id, out emitter))
                {
                    emitter = new ScreenEmitter();
                    _screens.Add(smoke.Id, emitter);
                }
                emitter.Center = Ground(smoke.Position, 0.0f);

                float remainingSeconds = (smoke.UntilTick - tick) / TicksPerSecond;
                float target = Mathf.Clamp(smoke.Radius * 0.9f, 16.0f, 50.0f) * Caps.SmokeScreenDensity;
                float rate = target / 16.0f * Mathf.Clamp01(remainingSeconds / 15.0f);
                if (!emitter.Started)
                {
                    emitter.Started = true;
                    emitter.Accumulator = target * 0.7f;
                }
                emitter.Accumulator += rate * dt;
                while (emitter.Accumulator >= 1.0f)
                {
                    emitter.Accumulator -= 1.0f;
                    float angle = _random.NextFloat() * Mathf.PI * 2.0f;
                    float distance = Mathf.Sqrt(_random.NextFloat()) * smoke.Radius * 0.85f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0.0f, -Mathf.Sin(angle) * distance);
                    Vector3 groundPosition = smoke.Position + offset;
                    Vector3 p = Ground(groundPosition, _random.Range(3.0f, 10.0f));
                    float shade = _random.Range(0.8f, 0.92f);
                    float size1 = Mathf.Clamp(smoke.Radius * 0.55f, 14.0f, 28.0f) * _random.Range(0.8f, 1.2f);
                    Emit(FxParticleLayer.Smoke, p, new Vector3(0.0f, _random.Range(0.2f, 0.8f), 0.0f),
                        _random.Range(13.0f, 19.0f), size1 * 0.5f, size1,
                        new Color(shade, shade, shade + 0.01f, 0.8f), new Color(shade, shade, shade, 0.0f),
                        0.08f, -0.03f);
                }
            }

            _obsoleteSmokeIds.Clear();
            foreach (KeyValuePair<int, ScreenEmitter> pair in _screens)
            {
                if (!_aliveSmokeIds.Contains(pair.Key))
                    _obsoleteSmokeIds.Add(pair.Key);
            }
            for (int i = 0; i < _obsoleteSmokeIds.Count; i++)
                _screens.Remove(_obsoleteSmokeIds[i]);
        }

        private void UpdateLights(float dt)
        {
            for (int i = 0; i < _flashes.Count; i++)
            {
                Flash flash = _flashes[i];
                if (!flash.Active)
                    continue;
                flash.Age += dt;
                if (flash.Age >= flash.Life)
                    flash.Active = false;
            }
        }

        private void UpdateScorches(float dt)
        {
            for (int i = 0; i < _scorches.Length; i++)
            {
                if (_scorches[i].Age < ScorchSeconds)
                    _scorches[i].Age += dt;
            }
        }

        private Vector3 Ground(Vector3 position, float height)
        {
            return new Vector3(position.x, SampleGround(position) + height, position.z);
        }

        private float SampleGround(Vector3 position)
        {
            Func<Vector3, float> sampler = GroundHeight;
            return sampler == null ? position.y : sampler(position);
        }

        private static Color TracerColor(FxEvent fxEvent)
        {
            if (fxEvent.TracerColor != Color.clear)
                return fxEvent.TracerColor;
            if (fxEvent.SourceSide == "blue")
                return new Color(1.0f, 0.85f, 0.45f, 1.0f);
            if (fxEvent.SourceSide == "red")
                return new Color(1.0f, 0.6f, 0.35f, 1.0f);
            return Color.white;
        }

        private static float ScorchAlpha(float age)
        {
            return Mathf.Clamp01(1.0f - (age - ScorchSeconds * 0.6f) / (ScorchSeconds * 0.4f));
        }
    }
}
