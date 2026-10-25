using System.Text.Json;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class RngGoldenTests
    {
        [Test]
        public void RawPcg32StreamsMatchGodot()
        {
            foreach (JsonElement stream in GoldenData.Root.GetProperty("raw").EnumerateArray())
            {
                long seed = stream.GetProperty("seed").GetInt64();
                SimRng rng = new SimRng(seed);
                Assert.That(rng.GetState(), Is.EqualTo(stream.GetProperty("initial_state").GetInt64()), $"initial state, seed {seed}");
                JsonElement values = stream.GetProperty("values");
                JsonElement states = stream.GetProperty("states");
                for (int i = 0; i < values.GetArrayLength(); i++)
                {
                    Assert.That(rng.NextUInt(), Is.EqualTo(values[i].GetUInt32()), $"draw {i}, seed {seed}");
                    Assert.That(rng.GetState(), Is.EqualTo(states[i].GetInt64()), $"state {i}, seed {seed}");
                }
            }
        }

        [Test]
        public void FloatStreamMatchesGodotBitsAndStates()
        {
            JsonElement stream = GoldenData.Root.GetProperty("floats");
            SimRng rng = new SimRng(stream.GetProperty("seed").GetInt64());
            JsonElement values = stream.GetProperty("values");
            JsonElement states = stream.GetProperty("states");
            for (int i = 0; i < values.GetArrayLength(); i++)
            {
                double actual = rng.NextFloat();
                GoldenData.AssertDoubleBits(values[i].GetString()!, actual);
                Assert.That(rng.GetState(), Is.EqualTo(states[i].GetInt64()), $"state {i}");
            }
        }

        [Test]
        public void FloatRangesMatchGodotBitsAndStates()
        {
            JsonElement stream = GoldenData.Root.GetProperty("ranges");
            SimRng rng = new SimRng(stream.GetProperty("seed").GetInt64());
            JsonElement inputs = stream.GetProperty("inputs");
            JsonElement values = stream.GetProperty("values");
            JsonElement states = stream.GetProperty("states");
            for (int i = 0; i < values.GetArrayLength(); i++)
            {
                double actual = rng.NextRange(inputs[i][0].GetDouble(), inputs[i][1].GetDouble());
                GoldenData.AssertDoubleBits(values[i].GetString()!, actual);
                Assert.That(rng.GetState(), Is.EqualTo(states[i].GetInt64()), $"state {i}");
            }
        }

        [Test]
        public void InclusiveIntegerRangesMatchGodot()
        {
            JsonElement stream = GoldenData.Root.GetProperty("ints");
            SimRng rng = new SimRng(stream.GetProperty("seed").GetInt64());
            JsonElement inputs = stream.GetProperty("inputs");
            JsonElement values = stream.GetProperty("values");
            JsonElement states = stream.GetProperty("states");
            for (int i = 0; i < values.GetArrayLength(); i++)
            {
                int actual = rng.NextInt(inputs[i][0].GetInt32(), inputs[i][1].GetInt32());
                Assert.That(actual, Is.EqualTo(values[i].GetInt32()), $"draw {i}");
                Assert.That(rng.GetState(), Is.EqualTo(states[i].GetInt64()), $"state {i}");
            }
        }

        [Test]
        public void StateRestoreMatchesGodotGolden()
        {
            JsonElement stream = GoldenData.Root.GetProperty("restore");
            SimRng rng = new SimRng(stream.GetProperty("seed").GetInt64());
            GoldenData.AssertDoubleBits(stream.GetProperty("first").GetString()!, rng.NextFloat());
            long saved = rng.GetState();
            Assert.That(saved, Is.EqualTo(stream.GetProperty("saved_state").GetInt64()));
            JsonElement expected = stream.GetProperty("expected");
            for (int i = 0; i < expected.GetArrayLength(); i++)
            {
                GoldenData.AssertDoubleBits(expected[i].GetString()!, rng.NextFloat());
            }

            rng.SetState(saved);
            for (int i = 0; i < expected.GetArrayLength(); i++)
            {
                GoldenData.AssertDoubleBits(expected[i].GetString()!, rng.NextFloat());
            }
        }

        [Test]
        public void GaussianMatchesGodotBitsAndDrawStates()
        {
            JsonElement stream = GoldenData.Root.GetProperty("gaussian");
            SimRng rng = new SimRng(stream.GetProperty("seed").GetInt64());
            JsonElement values = stream.GetProperty("values");
            JsonElement states = stream.GetProperty("states");
            for (int i = 0; i < values.GetArrayLength(); i++)
            {
                double actual = rng.NextGaussian();
                GoldenData.AssertDoubleBits(values[i].GetString()!, actual);
                Assert.That(rng.GetState(), Is.EqualTo(states[i].GetInt64()), $"state {i}");
            }
        }

        [Test]
        public void ScatterMatchesGodotAndConsumesFourFloatDraws()
        {
            foreach (JsonElement stream in GoldenData.Root.GetProperty("scatter").EnumerateArray())
            {
                SimRng rng = new SimRng(stream.GetProperty("seed").GetInt64());
                SimVec2 actual = rng.NextScatter(stream.GetProperty("sigma").GetDouble());
                GoldenData.AssertVector(stream.GetProperty("value"), actual);
                Assert.That(rng.GetState(), Is.EqualTo(stream.GetProperty("state").GetInt64()));
            }
        }

        [Test]
        public void ChanceIsDeterministicPerSeed()
        {
            SimRng first = new SimRng(77);
            SimRng second = new SimRng(77);
            for (int i = 0; i < 20; i++)
            {
                Assert.That(first.Chance(0.5), Is.EqualTo(second.Chance(0.5)));
                Assert.That(first.GetState(), Is.EqualTo(second.GetState()));
            }
        }

        [Test]
        public void ReseedingRestartsTheSameStream()
        {
            SimRng rng = new SimRng(42);
            uint expected = rng.NextUInt();
            rng.NextUInt();
            rng.Seed = 42;
            Assert.That(rng.SeedValue, Is.EqualTo(42));
            Assert.That(rng.NextUInt(), Is.EqualTo(expected));
        }
    }
}
