namespace Echelon.Unity.Presentation.Fx
{
    internal sealed class FxRandom
    {
        private uint _state;

        internal uint State
        {
            get { return _state; }
        }

        internal FxRandom(uint seed)
        {
            Reset(seed);
        }

        internal void Reset(uint seed)
        {
            _state = seed == 0u ? 0x6D2B79F5u : seed;
        }

        internal float NextFloat()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (x & 0x00FFFFFFu) * (1.0f / 16777216.0f);
        }

        internal float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }
    }
}
