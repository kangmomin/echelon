using System;

namespace Echelon.Core
{
    public sealed class SimRng
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong DefaultSequence = 1442695040888963407UL;

        private ulong _state;
        private ulong _increment;

        public SimRng(long seed = 0)
        {
            Seed = seed;
        }

        public long SeedValue { get; private set; }

        public long Seed
        {
            get => SeedValue;
            set
            {
                SeedValue = value;
                Reseed(unchecked((ulong)value));
            }
        }

        public long State
        {
            get => GetState();
            set => SetState(value);
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong oldState = _state;
                _state = oldState * Multiplier + (_increment | 1UL);
                uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
                int rotation = (int)(oldState >> 59);
                return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
            }
        }

        public double NextFloat()
        {
            return NextSingle();
        }

        public double NextRange(double from, double to)
        {
            float lower = (float)from;
            float upper = (float)to;
            return NextSingle() * (upper - lower) + lower;
        }

        public int NextInt(int from, int to)
        {
            if (from == to)
            {
                return from;
            }

            long min = Math.Min(from, to);
            long max = Math.Max(from, to);
            uint difference = (uint)(max - min);
            if (difference == uint.MaxValue)
            {
                return (int)((long)NextUInt() + min);
            }

            return (int)((long)NextBounded(difference + 1U) + min);
        }

        public bool Chance(double probability)
        {
            return NextFloat() < probability;
        }

        public double NextGaussian()
        {
            double u1 = Math.Max(NextFloat(), 1e-9);
            double u2 = NextFloat();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(SimMath.Tau * u2);
        }

        public SimVec2 NextScatter(double sigma)
        {
            return new SimVec2(NextGaussian(), NextGaussian()) * sigma;
        }

        public long GetState()
        {
            return unchecked((long)_state);
        }

        public void SetState(long state)
        {
            _state = unchecked((ulong)state);
        }

        private void Reseed(ulong seed)
        {
            unchecked
            {
                _state = 0UL;
                _increment = (DefaultSequence << 1) | 1UL;
                NextUInt();
                _state += seed;
                NextUInt();
            }
        }

        private uint NextBounded(uint bound)
        {
            uint threshold = unchecked(0U - bound) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return value % bound;
                }
            }
        }

        private float NextSingle()
        {
            uint prototypeExponent = NextUInt();
            if (prototypeExponent == 0U)
            {
                return 0.0f;
            }

            uint randomSignificand = NextUInt() | 0x80000001U;
            float significand = randomSignificand;
            int denominatorShift = 32 + LeadingZeroCount(prototypeExponent);
            ulong denominator = 1UL << denominatorShift;
            return (float)((double)significand / denominator);
        }

        private static int LeadingZeroCount(uint value)
        {
            int count = 0;
            if ((value & 0xffff0000U) == 0)
            {
                count += 16;
                value <<= 16;
            }

            if ((value & 0xff000000U) == 0)
            {
                count += 8;
                value <<= 8;
            }

            if ((value & 0xf0000000U) == 0)
            {
                count += 4;
                value <<= 4;
            }

            if ((value & 0xc0000000U) == 0)
            {
                count += 2;
                value <<= 2;
            }

            if ((value & 0x80000000U) == 0)
            {
                count++;
            }

            return count;
        }
    }
}
