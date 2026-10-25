using UnityEngine;

namespace Echelon.Unity.Presentation.Fx
{
    public enum FxParticleLayer
    {
        Smoke = 0,
        Fire = 1
    }

    public readonly struct FxParticleState
    {
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public Color Color { get; }
        public float Size { get; }
        public float Rotation { get; }
        public float Seed { get; }
        public float Age { get; }
        public float Life { get; }
        public float NormalizedAge { get; }

        internal FxParticleState(Vector3 position, Vector3 velocity, Color color, float size,
            float rotation, float seed, float age, float life, float normalizedAge)
        {
            Position = position;
            Velocity = velocity;
            Color = color;
            Size = size;
            Rotation = rotation;
            Seed = seed;
            Age = age;
            Life = life;
            NormalizedAge = normalizedAge;
        }
    }

    public readonly struct FxProjectileState
    {
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public Vector3 Head { get; }
        public Vector3 Tail { get; }
        public Color Color { get; }
        public string Weapon { get; }
        public float Age { get; }
        public float Duration { get; }
        public float TailFraction { get; }
        public float TrailAccumulator { get; }
        public bool Hit { get; }
        public bool ShowImpact { get; }
        public bool IsMissile
        {
            get { return Weapon == FxWeapon.SAM || Weapon == FxWeapon.ATGM; }
        }

        internal FxProjectileState(Vector3 start, Vector3 end, Vector3 head, Vector3 tail,
            Color color, string weapon, float age, float duration, float tailFraction,
            float trailAccumulator, bool hit, bool showImpact)
        {
            Start = start;
            End = end;
            Head = head;
            Tail = tail;
            Color = color;
            Weapon = weapon;
            Age = age;
            Duration = duration;
            TailFraction = tailFraction;
            TrailAccumulator = trailAccumulator;
            Hit = hit;
            ShowImpact = showImpact;
        }
    }

    public readonly struct FxWreckState
    {
        public Vector3 Position { get; }
        public float Age { get; }

        internal FxWreckState(Vector3 position, float age)
        {
            Position = position;
            Age = age;
        }
    }

    public readonly struct FxScorchState
    {
        public Vector3 Position { get; }
        public float Radius { get; }
        public float Rotation { get; }
        public float Seed { get; }
        public float Age { get; }
        public float Alpha { get; }

        internal FxScorchState(Vector3 position, float radius, float rotation, float seed, float age, float alpha)
        {
            Position = position;
            Radius = radius;
            Rotation = rotation;
            Seed = seed;
            Age = age;
            Alpha = alpha;
        }
    }

    public readonly struct FxFlashState
    {
        public bool Active { get; }
        public Vector3 Position { get; }
        public Color Color { get; }
        public float Range { get; }
        public float Intensity { get; }
        public float Age { get; }
        public float Life { get; }

        internal FxFlashState(bool active, Vector3 position, Color color, float range,
            float intensity, float age, float life)
        {
            Active = active;
            Position = position;
            Color = color;
            Range = range;
            Intensity = intensity;
            Age = age;
            Life = life;
        }
    }
}
