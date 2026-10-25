using System;
using System.IO;
using System.Text.Json;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    internal static class GoldenData
    {
        private static readonly Lazy<JsonDocument> Document = new Lazy<JsonDocument>(() =>
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Golden", "rng", "godot-4.7.2.json");
            return JsonDocument.Parse(File.ReadAllText(path));
        });

        public static JsonElement Root => Document.Value.RootElement;

        public static double Double(string littleEndianHex)
        {
            return BitConverter.Int64BitsToDouble(unchecked((long)DecodeLittleEndian(littleEndianHex)));
        }

        public static float Single(string littleEndianHex)
        {
            return BitConverter.Int32BitsToSingle(unchecked((int)DecodeLittleEndian(littleEndianHex)));
        }

        public static SimVec2 Vector(JsonElement value)
        {
            return new SimVec2(Single(value.GetProperty("x").GetString()!), Single(value.GetProperty("y").GetString()!));
        }

        public static void AssertDoubleBits(string expectedHex, double actual)
        {
            Assert.That(BitConverter.DoubleToInt64Bits(actual), Is.EqualTo(BitConverter.DoubleToInt64Bits(Double(expectedHex))));
        }

        public static void AssertSingleBits(string expectedHex, float actual)
        {
            Assert.That(BitConverter.SingleToInt32Bits(actual), Is.EqualTo(BitConverter.SingleToInt32Bits(Single(expectedHex))));
        }

        public static void AssertVector(JsonElement expected, SimVec2 actual)
        {
            AssertSingleBits(expected.GetProperty("x").GetString()!, actual.X);
            AssertSingleBits(expected.GetProperty("y").GetString()!, actual.Y);
        }

        private static ulong DecodeLittleEndian(string hex)
        {
            ulong bits = 0;
            for (int i = 0; i < hex.Length / 2; i++)
            {
                bits |= (ulong)Convert.ToByte(hex.Substring(i * 2, 2), 16) << (i * 8);
            }

            return bits;
        }
    }
}
