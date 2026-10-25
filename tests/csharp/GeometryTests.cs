using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class GeometryTests
    {
        [Test]
        public void ClosestPointClampsToSegmentAndHandlesDegenerateSegment()
        {
            SimVec2 a = new SimVec2(10.0, 20.0);
            SimVec2 b = new SimVec2(20.0, 20.0);
            Assert.That(Geometry2D.GetClosestPointToSegment(new SimVec2(0.0, 30.0), a, b), Is.EqualTo(a));
            Assert.That(Geometry2D.GetClosestPointToSegment(new SimVec2(15.0, 30.0), a, b), Is.EqualTo(new SimVec2(15.0, 20.0)));
            Assert.That(Geometry2D.GetClosestPointToSegment(new SimVec2(30.0, 30.0), a, b), Is.EqualTo(b));
            Assert.That(Geometry2D.GetClosestPointToSegment(new SimVec2(30.0, 30.0), a, a), Is.EqualTo(a));
            Assert.That(Geometry2D.GetDistanceToSegment(new SimVec2(15.0, 30.0), a, b), Is.EqualTo(10.0).Within(1e-6));
        }

        [Test]
        public void SegmentIntersectionMatchesGodotRules()
        {
            bool intersects = Geometry2D.SegmentIntersectsSegment(
                new SimVec2(0.0, 0.0), new SimVec2(10.0, 10.0),
                new SimVec2(0.0, 10.0), new SimVec2(10.0, 0.0), out SimVec2 result);
            Assert.That(intersects, Is.True);
            Assert.That(result.X, Is.EqualTo(5.0).Within(1e-5));
            Assert.That(result.Y, Is.EqualTo(5.0).Within(1e-5));

            Assert.That(Geometry2D.SegmentIntersectsSegment(
                new SimVec2(0.0, 0.0), new SimVec2(10.0, 0.0),
                new SimVec2(0.0, 1.0), new SimVec2(10.0, 1.0)), Is.False);
            Assert.That(Geometry2D.SegmentIntersectsSegment(
                new SimVec2(0.0, 0.0), new SimVec2(10.0, 0.0),
                new SimVec2(2.0, 0.0), new SimVec2(8.0, 0.0)), Is.False, "Collinear segments are not an intersection");
            Assert.That(Geometry2D.SegmentIntersectsSegment(
                SimVec2.Zero, SimVec2.Zero, SimVec2.Zero, SimVec2.One), Is.False);
        }

        [Test]
        public void PointInPolygonIncludesBoundaryAndSupportsConcavePolygons()
        {
            SimVec2[] square =
            {
                new SimVec2(0.0, 0.0), new SimVec2(10.0, 0.0),
                new SimVec2(10.0, 10.0), new SimVec2(0.0, 10.0)
            };
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(5.0, 5.0), square), Is.True);
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(15.0, 5.0), square), Is.False);
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(10.0, 5.0), square), Is.True);
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(0.0, 0.0), square), Is.True);
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(5.0, 0.0), square), Is.True);
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(5.0, 5.0), new[] { SimVec2.Zero, SimVec2.One }), Is.False);

            SimVec2[] concave =
            {
                new SimVec2(0.0, 0.0), new SimVec2(10.0, 0.0), new SimVec2(10.0, 10.0),
                new SimVec2(6.0, 10.0), new SimVec2(6.0, 4.0), new SimVec2(4.0, 4.0),
                new SimVec2(4.0, 10.0), new SimVec2(0.0, 10.0)
            };
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(2.0, 8.0), concave), Is.True);
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(5.0, 8.0), concave), Is.False);
            Assert.That(Geometry2D.IsPointInPolygon(new SimVec2(5.0, 2.0), concave), Is.True);
        }
    }
}
