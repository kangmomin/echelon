using System;
using UnityEngine;

namespace Echelon.Unity.Presentation.Fx
{
    internal sealed class FxParticlePool
    {
        private struct Particle
        {
            internal Vector3 Position;
            internal Vector3 Velocity;
            internal float Age;
            internal float Life;
            internal float Size0;
            internal float Size1;
            internal float Drag;
            internal float Gravity;
            internal float Rotation;
            internal float Spin;
            internal float Seed;
            internal Color Color0;
            internal Color Color1;
        }

        private readonly Particle[] _particles;

        internal int Capacity
        {
            get { return _particles.Length; }
        }

        internal int Count { get; private set; }

        internal FxParticlePool(int capacity)
        {
            _particles = new Particle[capacity];
        }

        internal void Clear()
        {
            Count = 0;
        }

        internal void Add(Vector3 position, Vector3 velocity, float life, float size0, float size1,
            Color color0, Color color1, float drag, float gravity, float rotation, float spin, float seed)
        {
            int index = Count++;
            _particles[index] = new Particle
            {
                Position = position,
                Velocity = velocity,
                Age = 0.0f,
                Life = Mathf.Max(life, 0.01f),
                Size0 = size0,
                Size1 = size1,
                Drag = drag,
                Gravity = gravity,
                Rotation = rotation,
                Spin = spin,
                Seed = seed,
                Color0 = color0,
                Color1 = color1
            };
        }

        internal void Step(float dt, Vector3 wind)
        {
            int index = 0;
            while (index < Count)
            {
                Particle particle = _particles[index];
                float age = particle.Age + dt;
                if (age >= particle.Life)
                {
                    Remove(index);
                    continue;
                }

                particle.Age = age;
                Vector3 velocity = particle.Velocity;
                velocity += (wind - velocity) * Mathf.Min(particle.Drag * dt, 1.0f);
                velocity.y -= particle.Gravity * dt;
                particle.Velocity = velocity;
                particle.Position += velocity * dt;
                _particles[index] = particle;
                index++;
            }
        }

        internal FxParticleState GetState(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException("index");

            Particle particle = _particles[index];
            float k = particle.Age / particle.Life;
            float inverse = 1.0f - k;
            float grow = 1.0f - inverse * inverse;
            float size = Mathf.Lerp(particle.Size0, particle.Size1, grow);
            Color color = Color.Lerp(particle.Color0, particle.Color1, k);
            return new FxParticleState(particle.Position, particle.Velocity, color, size,
                particle.Rotation + particle.Spin * particle.Age, particle.Seed,
                particle.Age, particle.Life, k);
        }

        private void Remove(int index)
        {
            int last = Count - 1;
            if (index != last)
                _particles[index] = _particles[last];
            Count = last;
        }
    }
}
