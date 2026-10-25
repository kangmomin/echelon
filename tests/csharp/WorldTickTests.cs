using System.Collections.Generic;
using System.Globalization;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class WorldTickTests
    {
        [Test]
        public void TenTicksPerSecondAt1x()
        {
            SimWorld world = new SimWorld(1);
            Assert.That(RunFrames(world, 60, 1.0 / 60.0), Is.EqualTo(10));
            Assert.That(world.Tick, Is.EqualTo(10));
            Assert.That(world.GetSimTimeSec(), Is.EqualTo(1.0).Within(0.0001));
        }

        [Test]
        public void PartialTickAccumulates()
        {
            SimWorld world = new SimWorld(1);
            Assert.That(world.Advance(0.05), Is.EqualTo(0));
            Assert.That(world.GetInterpolationAlpha(), Is.EqualTo(0.5).Within(0.001));
            Assert.That(world.Advance(0.05), Is.EqualTo(1));
        }

        [Test]
        public void SpeedStepsScaleTickRate()
        {
            int[] expected = { 5, 10, 20, 40 };
            for (int i = 0; i < SimConst.SpeedSteps.Count; i++)
            {
                SimWorld world = new SimWorld(1);
                world.SetSpeedIndex(i);
                Assert.That(RunFrames(world, 60, 1.0 / 60.0), Is.EqualTo(expected[i]), $"speed {SimConst.SpeedSteps[i]}x");
            }
        }

        [Test]
        public void SpeedIndexIsClamped()
        {
            SimWorld world = new SimWorld(1);
            for (int i = 0; i < 10; i++)
            {
                world.SpeedUp();
            }

            Assert.That(world.SpeedIndex, Is.EqualTo(SimConst.SpeedSteps.Count - 1));
            for (int i = 0; i < 10; i++)
            {
                world.SpeedDown();
            }

            Assert.That(world.SpeedIndex, Is.EqualTo(0));
        }

        [Test]
        public void PauseStopsTimeAndDoesNotAccumulate()
        {
            SimWorld world = new SimWorld(1);
            world.SetPaused(true);
            Assert.That(RunFrames(world, 120, 1.0 / 60.0), Is.EqualTo(0));
            world.SetPaused(false);
            Assert.That(world.Advance(0.0), Is.EqualTo(0));
        }

        [Test]
        public void LargeHitchIsCapped()
        {
            SimWorld world = new SimWorld(1);
            Assert.That(world.Advance(5.0), Is.EqualTo(SimConst.MaxTicksPerAdvance));
            Assert.That(world.Advance(0.0), Is.LessThan(1));
        }

        [Test]
        public void NegativeDeltaIsIgnored()
        {
            SimWorld world = new SimWorld(1);
            Assert.That(world.Advance(-1.0), Is.EqualTo(0));
            Assert.That(world.Tick, Is.EqualTo(0));
        }

        [Test]
        public void SystemsRunInRegistrationOrderWithCurrentTick()
        {
            List<string> entries = new List<string>();
            SimWorld world = new SimWorld(1);
            world.AddSystem(new RecorderSystem("a", entries));
            world.AddSystem(new RecorderSystem("b", entries));
            world.StepTick();
            world.StepTick();
            Assert.That(entries, Is.EqualTo(new[] { "a@0", "b@0", "a@1", "b@1" }));
        }

        [Test]
        public void ControlChangesEmitEvents()
        {
            SimWorld world = new SimWorld(1);
            world.TogglePause();
            world.SpeedUp();
            List<SimEvent> events = world.Events.Drain();
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Type, Is.EqualTo("paused_changed"));
            Assert.That(events[1].Type, Is.EqualTo("speed_changed"));
            Assert.That(events[0].Payload["paused"], Is.EqualTo(true));
            Assert.That(events[1].Payload["speed"], Is.EqualTo(2.0));
            Assert.That(world.Events.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void SameSeedGivesSameStateHash()
        {
            Assert.That(HashAfter(42, 500), Is.EqualTo(HashAfter(42, 500)));
        }

        [Test]
        public void DifferentSeedGivesDifferentStateHash()
        {
            Assert.That(HashAfter(42, 500), Is.Not.EqualTo(HashAfter(43, 500)));
        }

        [Test]
        public void RngStateSaveAndRestore()
        {
            SimRng rng = new SimRng(7);
            rng.NextFloat();
            long saved = rng.GetState();
            double[] a = { rng.NextFloat(), rng.NextFloat(), rng.NextFloat() };
            rng.SetState(saved);
            double[] b = { rng.NextFloat(), rng.NextFloat(), rng.NextFloat() };
            Assert.That(a, Is.EqualTo(b));
        }

        private static int RunFrames(SimWorld world, int frames, double delta)
        {
            int total = 0;
            for (int i = 0; i < frames; i++)
            {
                total += world.Advance(delta);
            }

            return total;
        }

        private static long HashAfter(long seed, int ticks)
        {
            SimWorld world = new SimWorld(seed);
            world.AddSystem(new RandomWalkSystem());
            for (int i = 0; i < ticks; i++)
            {
                world.StepTick();
            }

            return world.StateHash();
        }

        private sealed class RecorderSystem : SimSystem
        {
            private readonly string _tag;
            private readonly List<string> _entries;

            public RecorderSystem(string tag, List<string> entries)
            {
                _tag = tag;
                _entries = entries;
            }

            public override void Step(SimWorld world)
            {
                _entries.Add(string.Format(CultureInfo.InvariantCulture, "{0}@{1}", _tag, world.Tick));
            }
        }

        private sealed class RandomWalkSystem : SimSystem
        {
            public SimVec2 Position { get; private set; } = SimVec2.Zero;

            public override void Step(SimWorld world)
            {
                Position += new SimVec2(world.Rng.NextRange(-1.0, 1.0), world.Rng.NextRange(-1.0, 1.0));
            }

            public override long StateHash()
            {
                return SimHash.Hash(Position);
            }
        }
    }
}
