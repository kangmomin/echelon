using System;
using System.Buffers.Binary;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class TerrainNoiseTests
    {
        [TestCase(7, 0.0009, 4, 0.0, 0.0, "d7fdc210")]
        [TestCase(7, 0.0009, 4, 5.0, 5.0, "5bb98a3c")]
        [TestCase(7, 0.0009, 4, 1100.0, 1140.0, "f0645bbe")]
        [TestCase(7, 0.0009, 4, -5.0, -5.0, "06b98abc")]
        [TestCase(7, 0.0009, 4, -1000.25, 333.75, "92f6213d")]
        [TestCase(7, 0.0009, 4, 123456.5, -98765.25, "bba9e9bd")]
        [TestCase(7, 0.0009, 4, -1000000.0, 1000000.0, "c4c9c012")]
        [TestCase(0, 0.013, 1, 0.0, 0.0, "dfd31211")]
        [TestCase(0, 0.013, 1, 5.0, 5.0, "f93e823e")]
        [TestCase(0, 0.013, 1, 1100.0, 1140.0, "31b7ef3e")]
        [TestCase(0, 0.013, 1, -5.0, -5.0, "fe3e82be")]
        [TestCase(0, 0.013, 1, -1000.25, 333.75, "af52f73e")]
        [TestCase(0, 0.013, 1, 123456.5, -98765.25, "8eb61fbe")]
        [TestCase(0, 0.013, 1, -1000000.0, 1000000.0, "70c36a13")]
        [TestCase(-12345, 0.013, 5, 0.0, 0.0, "44bde410")]
        [TestCase(-12345, 0.013, 5, 5.0, 5.0, "a2ef9c3d")]
        [TestCase(-12345, 0.013, 5, 1100.0, 1140.0, "24bfba3e")]
        [TestCase(-12345, 0.013, 5, -5.0, -5.0, "66fa9abd")]
        [TestCase(-12345, 0.013, 5, -1000.25, 333.75, "f8a00dbe")]
        [TestCase(-12345, 0.013, 5, 123456.5, -98765.25, "6f65ac3e")]
        [TestCase(-12345, 0.013, 5, -1000000.0, 1000000.0, "144f1f11")]
        [TestCase(99991, 0.1, 3, 0.0, 0.0, "e25ba111")]
        [TestCase(99991, 0.1, 3, 5.0, 5.0, "1e7bbc3e")]
        [TestCase(99991, 0.1, 3, 1100.0, 1140.0, "dec03fbd")]
        [TestCase(99991, 0.1, 3, -5.0, -5.0, "2f5cbbbe")]
        [TestCase(99991, 0.1, 3, -1000.25, 333.75, "e417433e")]
        [TestCase(99991, 0.1, 3, 123456.5, -98765.25, "3c9928be")]
        [TestCase(99991, 0.1, 3, -1000000.0, 1000000.0, "ac151b92")]
        public void SimplexSmoothFbmMatchesGodotBits(int seed, double frequency, int octaves,
            double x, double y, string expectedHex)
        {
            GodotFastNoiseLite noise = new GodotFastNoiseLite(seed, (float)frequency, octaves);
            float actual = noise.GetNoise2D((float)x, (float)y);
            int expected = BinaryPrimitives.ReadInt32LittleEndian(Convert.FromHexString(expectedHex));
            Assert.That(BitConverter.SingleToInt32Bits(actual), Is.EqualTo(expected), actual.ToString("R"));
        }
    }
}
