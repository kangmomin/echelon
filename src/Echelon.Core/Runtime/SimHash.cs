using System;

namespace Echelon.Core
{
    public static class SimHash
    {
        public const long Initial = unchecked((long)0xcbf29ce484222325UL);
        private const ulong Prime = 0x100000001b3UL;

        public static long Append(long hash, long value)
        {
            unchecked
            {
                ulong result = (ulong)hash;
                ulong bits = (ulong)value;
                for (int i = 0; i < 8; i++)
                {
                    result = (result ^ (byte)bits) * Prime;
                    bits >>= 8;
                }

                return (long)result;
            }
        }

        public static long Append(long hash, int value)
        {
            unchecked
            {
                ulong result = (ulong)hash;
                uint bits = (uint)value;
                for (int i = 0; i < 4; i++)
                {
                    result = (result ^ (byte)bits) * Prime;
                    bits >>= 8;
                }

                return (long)result;
            }
        }

        public static long Hash(SimVec2 value)
        {
            long hash = Append(Initial, BitConverter.SingleToInt32Bits(value.X));
            return Append(hash, BitConverter.SingleToInt32Bits(value.Y));
        }

        public static long Hash(SimVec2I value)
        {
            return Append(Append(Initial, value.X), value.Y);
        }
    }
}
