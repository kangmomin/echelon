using System.Text.Json;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class VectorAndMathTests
    {
        [Test]
        public void VectorArithmeticMatchesGodotBits()
        {
            JsonElement golden = GoldenData.Root.GetProperty("vectors");
            SimVec2 a = GoldenData.Vector(golden.GetProperty("a"));
            SimVec2 b = GoldenData.Vector(golden.GetProperty("b"));
            GoldenData.AssertVector(golden.GetProperty("a"), new SimVec2(3.2, -4.1));
            GoldenData.AssertVector(golden.GetProperty("b"), new SimVec2(-1.3, 2.7));
            GoldenData.AssertVector(golden.GetProperty("add"), a + b);
            GoldenData.AssertVector(golden.GetProperty("subtract"), a - b);
            GoldenData.AssertVector(golden.GetProperty("scale"), a * 0.1);
            GoldenData.AssertVector(golden.GetProperty("divide"), a / 0.1);
            GoldenData.AssertVector(golden.GetProperty("lerp"), a.Lerp(b, 0.37));
            GoldenData.AssertVector(golden.GetProperty("rotated"), a.Rotated(0.37));
            GoldenData.AssertVector(golden.GetProperty("normalized"), a.Normalized());
        }

        [Test]
        public void VectorScalarsMatchGodotBits()
        {
            JsonElement golden = GoldenData.Root.GetProperty("vectors");
            SimVec2 a = GoldenData.Vector(golden.GetProperty("a"));
            SimVec2 b = GoldenData.Vector(golden.GetProperty("b"));
            GoldenData.AssertSingleBits(golden.GetProperty("length").GetString()!, (float)a.Length());
            GoldenData.AssertSingleBits(golden.GetProperty("length_squared").GetString()!, (float)a.LengthSquared());
            GoldenData.AssertSingleBits(golden.GetProperty("distance").GetString()!, (float)a.DistanceTo(b));
            GoldenData.AssertSingleBits(golden.GetProperty("dot").GetString()!, (float)a.Dot(b));
            GoldenData.AssertSingleBits(golden.GetProperty("cross").GetString()!, (float)a.Cross(b));
            GoldenData.AssertSingleBits(golden.GetProperty("angle").GetString()!, (float)a.Angle());
            GoldenData.AssertSingleBits(golden.GetProperty("angle_to").GetString()!, (float)a.AngleTo(b));
        }

        [Test]
        public void ScalarHelpersMatchGodotGoldens()
        {
            JsonElement golden = GoldenData.Root.GetProperty("vectors");
            double difference = SimMath.AngleDifference(-SimMath.Pi + 0.1, SimMath.Pi - 0.1);
            GoldenData.AssertDoubleBits(golden.GetProperty("angle_difference").GetString()!, difference);
            GoldenData.AssertDoubleBits(golden.GetProperty("wrap").GetString()!, SimMath.Wrap(SimMath.Pi + 0.25, -SimMath.Pi, SimMath.Pi));
            GoldenData.AssertDoubleBits(golden.GetProperty("round_negative_half").GetString()!, SimMath.Round(-2.5));
            GoldenData.AssertDoubleBits(golden.GetProperty("round_positive_half").GetString()!, SimMath.Round(2.5));
        }

        [Test]
        public void VectorRoundingUsesHalfAwayFromZero()
        {
            SimVec2 value = new SimVec2(-2.5, 2.5);
            Assert.That(value.Rounded(), Is.EqualTo(new SimVec2(-3.0, 3.0)));
            Assert.That(value.Floor(), Is.EqualTo(new SimVec2(-3.0, 2.0)));
            Assert.That(value.Ceiling(), Is.EqualTo(new SimVec2(-2.0, 3.0)));
            Assert.That(new SimVec2(2.55, -2.55).Snapped(1.0), Is.EqualTo(new SimVec2(3.0, -3.0)));
        }

        [Test]
        public void NormalizationHandlesZeroAndNonFiniteValues()
        {
            Assert.That(SimVec2.Zero.Normalized(), Is.EqualTo(SimVec2.Zero));
            Assert.That(SimVec2.Inf.Normalized(), Is.EqualTo(SimVec2.Zero));
            Assert.That(new SimVec2(double.NaN, 1.0).Normalized(), Is.EqualTo(SimVec2.Zero));
            Assert.That(new SimVec2(3.0, 4.0).Normalized().Length(), Is.EqualTo(1.0).Within(1e-6));
        }

        [Test]
        public void VectorGeometryAndConversionsAreAvailable()
        {
            SimVec2 vector = new SimVec2(3.0, 4.0);
            Assert.That(vector.Length(), Is.EqualTo(5.0));
            Assert.That(vector.LengthSquared(), Is.EqualTo(25.0));
            Assert.That(vector.DistanceTo(SimVec2.Zero), Is.EqualTo(5.0));
            Assert.That(vector.Dot(new SimVec2(2.0, -1.0)), Is.EqualTo(2.0));
            Assert.That(vector.Cross(new SimVec2(2.0, -1.0)), Is.EqualTo(-11.0));
            Assert.That(SimVec2.Right.Rotated(SimMath.Pi / 2.0).Y, Is.EqualTo(1.0).Within(1e-6));
            Assert.That(SimVec2.Zero.DirectionTo(vector), Is.EqualTo(new SimVec2(0.6, 0.8)));
            Assert.That((SimVec2I)new SimVec2(3.9, -3.9), Is.EqualTo(new SimVec2I(3, -3)));
            Assert.That((SimVec2)new SimVec2I(3, -4), Is.EqualTo(new SimVec2(3.0, -4.0)));
        }

        [Test]
        public void IntegerVectorSupportsGridArithmetic()
        {
            SimVec2I value = new SimVec2I(3, 4);
            Assert.That(value.Length(), Is.EqualTo(5.0));
            Assert.That(value.LengthSquared(), Is.EqualTo(25));
            Assert.That(value + SimVec2I.One, Is.EqualTo(new SimVec2I(4, 5)));
            Assert.That(value - SimVec2I.One, Is.EqualTo(new SimVec2I(2, 3)));
            Assert.That(value * 2, Is.EqualTo(new SimVec2I(6, 8)));
            Assert.That(value / 2, Is.EqualTo(new SimVec2I(1, 2)));
            Assert.That(value.DistanceSquaredTo(SimVec2I.Zero), Is.EqualTo(25));
            Assert.That(value.Clamp(SimVec2I.Zero, new SimVec2I(3, 3)), Is.EqualTo(new SimVec2I(3, 3)));
        }

        [Test]
        public void ClampLerpWrapAndApproximationCoverEdgeCases()
        {
            Assert.That(SimMath.Clamp(12, 0, 10), Is.EqualTo(10));
            Assert.That(SimMath.Clamp(-2.0, 0.0, 10.0), Is.EqualTo(0.0));
            Assert.That(SimMath.Lerp(2.0, 4.0, 0.25), Is.EqualTo(2.5));
            Assert.That(SimMath.Wrap(-1, 0, 10), Is.EqualTo(9));
            Assert.That(SimMath.Wrap(11, 0, 10), Is.EqualTo(1));
            Assert.That(SimMath.Wrap(4.0, 2.0, 2.0), Is.EqualTo(2.0));
            Assert.That(SimMath.IsZeroApprox(0.000001));
            Assert.That(SimMath.IsZeroApprox(0.1), Is.False);
            Assert.That(SimMath.IsEqualApprox(double.PositiveInfinity, double.PositiveInfinity));
            Assert.That(SimVec2.Zero.IsZeroApprox());
        }
    }
}
