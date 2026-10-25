using System;

namespace Echelon.Unity.Presentation.Terrain
{
    internal sealed class TerrainRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;
        private ulong _state;

        internal TerrainRandom(int seed)
        {
            _state = 0;
            NextUInt();
            _state += unchecked((uint)seed);
            NextUInt();
        }

        internal uint NextUInt()
        {
            ulong oldState = _state;
            _state = oldState * Multiplier + Increment;
            uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);
            return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
        }

        internal float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        internal float Range(float minimum, float maximum)
        {
            return minimum + (maximum - minimum) * NextFloat();
        }

        internal int NextInt(int maximumExclusive)
        {
            if (maximumExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            return (int)(NextUInt() % (uint)maximumExclusive);
        }

        internal int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            return minimumInclusive + NextInt(maximumExclusive - minimumInclusive);
        }
    }
}
