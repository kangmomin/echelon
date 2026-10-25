using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class EchelonModelGuideTests
    {
        [Test]
        public void ResetSetPathAndClearPreserveDestinationAndCancellationSemantics()
        {
            Guide guide = new Guide();
            GridPathfinder.PathRequest resetRequest = new GridPathfinder.PathRequest();
            guide.Pending = resetRequest;
            guide.Destination = new SimVec2(9.0, 9.0);
            guide.HasDestination = true;
            guide.FinalFacing = 1.25;
            SimVec2 position = new SimVec2(3.0, 4.0);
            guide.Reset(position, 0.5);
            Assert.That(guide.Pos, Is.EqualTo(position));
            Assert.That(guide.Heading, Is.EqualTo(0.5));
            Assert.That(guide.Path, Is.Empty);
            Assert.That(guide.PathIndex, Is.Zero);
            Assert.That(guide.Speed, Is.Zero);
            Assert.That(guide.Trail, Is.EqualTo(new[] { position }));
            Assert.That(guide.Pending, Is.Null);
            Assert.That(resetRequest.Cancelled, Is.False);
            Assert.That(guide.HasDestination, Is.False);
            Assert.That(guide.Destination, Is.EqualTo(new SimVec2(9.0, 9.0)));
            Assert.That(guide.FinalFacing, Is.EqualTo(1.25));

            guide.SetPath(new[] { position, new SimVec2(3.0, 14.0), new SimVec2(13.0, 14.0) });
            Assert.That(guide.PathIndex, Is.EqualTo(1));
            Assert.That(guide.IsMoving(), Is.True);
            Assert.That(guide.HasDestination, Is.True);
            Assert.That(guide.Destination, Is.EqualTo(new SimVec2(13.0, 14.0)));
            GridPathfinder.PathRequest clearRequest = new GridPathfinder.PathRequest();
            guide.Pending = clearRequest;
            Assert.That(guide.IsWaitingForPath(), Is.True);
            guide.ClearPath();
            Assert.That(clearRequest.Cancelled, Is.True);
            Assert.That(guide.Pending, Is.Null);
            Assert.That(guide.IsWaitingForPath(), Is.False);
            Assert.That(guide.IsMoving(), Is.False);
            Assert.That(guide.HasDestination, Is.True);
            Assert.That(guide.Destination, Is.EqualTo(new SimVec2(13.0, 14.0)));
            guide.SetPath(new List<SimVec2>());
            Assert.That(guide.HasDestination, Is.True);
            Assert.That(guide.Destination, Is.EqualTo(new SimVec2(13.0, 14.0)));
            guide.SetPath(new List<SimVec2> { position });
            Assert.That(guide.PathIndex, Is.EqualTo(1));
            Assert.That(guide.IsMoving(), Is.False);
        }

        [Test]
        public void AdvanceTraversesWholePathAndRecordsOnlySampledTrailPositions()
        {
            Guide guide = new Guide();
            guide.Reset(SimVec2.Zero, 0.0);
            SimVec2 destination = new SimVec2(10.0, 20.0);
            guide.SetPath(new List<SimVec2> { SimVec2.Zero, new SimVec2(0.0, 20.0), destination });
            Assert.That(guide.RemainingDistance(), Is.EqualTo(30.0).Within(1e-6));
            double moved = guide.Advance(1.0, 100.0);
            Assert.That(moved, Is.EqualTo(30.0).Within(1e-6));
            Assert.That(guide.Pos, Is.EqualTo(destination));
            Assert.That(guide.PathIndex, Is.EqualTo(3));
            Assert.That(guide.IsMoving(), Is.False);
            Assert.That(guide.Speed, Is.EqualTo(30.0).Within(1e-6));
            Assert.That(guide.Heading, Is.EqualTo(Math.PI / 2.0).Within(1e-6));
            Assert.That(guide.Trail, Is.EqualTo(new[] { SimVec2.Zero, destination }));
            Assert.That(guide.RemainingDistance(), Is.Zero);
        }

        [Test]
        public void AdvanceHonorsSpeedBudgetAndArrivalEpsilon()
        {
            Guide guide = new Guide();
            guide.Reset(SimVec2.Zero, 0.0);
            guide.SetPath(new List<SimVec2> { SimVec2.Zero, new SimVec2(0.0, 20.0) });
            Assert.That(guide.Advance(0.5, 10.0), Is.EqualTo(5.0).Within(1e-6));
            Assert.That(guide.Pos, Is.EqualTo(new SimVec2(0.0, 5.0)));
            Assert.That(guide.Speed, Is.EqualTo(10.0).Within(1e-6));
            Assert.That(guide.RemainingDistance(), Is.EqualTo(15.0).Within(1e-6));
            Assert.That(guide.Advance(0.5, 0.0), Is.Zero);
            Assert.That(guide.Pos, Is.EqualTo(new SimVec2(0.0, 5.0)));
            Assert.That(guide.Speed, Is.Zero);

            Guide near = new Guide();
            near.Reset(SimVec2.Zero, 0.0);
            near.SetPath(new List<SimVec2> { SimVec2.Zero, new SimVec2(1.0, 0.0) });
            Assert.That(near.Advance(0.1, 10.0), Is.Zero);
            Assert.That(near.Pos, Is.EqualTo(new SimVec2(1.0, 0.0)));
            Assert.That(near.IsMoving(), Is.False);
            Assert.That(near.Speed, Is.Zero);
        }

        [Test]
        public void MoveTowardUsesDirectionWhenFarAndFaceWhenNear()
        {
            Guide guide = new Guide();
            guide.Reset(SimVec2.Zero, 0.0);
            double moved = guide.MoveToward(new SimVec2(40.0, 0.0), 1.0, 10.0, double.NaN);
            Assert.That(moved, Is.EqualTo(10.0).Within(1e-6));
            Assert.That(guide.Pos, Is.EqualTo(new SimVec2(10.0, 0.0)));
            Assert.That(guide.Speed, Is.EqualTo(10.0).Within(1e-6));
            Assert.That(guide.Heading, Is.EqualTo(Math.PI / 2.0).Within(1e-6));

            moved = guide.MoveToward(new SimVec2(11.0, 0.0), 1.0, 0.0, 0.0);
            Assert.That(moved, Is.Zero);
            Assert.That(guide.Pos, Is.EqualTo(new SimVec2(11.0, 0.0)));
            Assert.That(guide.Heading, Is.Zero);
            Assert.That(guide.Speed, Is.Zero);

            Guide near = new Guide();
            near.Reset(SimVec2.Zero, 0.0);
            moved = near.MoveToward(new SimVec2(20.0, 0.0), 0.1, 50.0, Math.PI / 4.0);
            Assert.That(moved, Is.EqualTo(5.0).Within(1e-6));
            Assert.That(near.Pos, Is.EqualTo(new SimVec2(5.0, 0.0)));
            Assert.That(near.Heading, Is.EqualTo(Guide.TurnRate * 0.1).Within(1e-6));
            Assert.That(near.Speed, Is.EqualTo(50.0).Within(1e-6));
        }

        [Test]
        public void TurnTowardUsesShortestWrappedAngle()
        {
            Guide guide = new Guide { Heading = Math.PI - 0.1 };
            guide.TurnToward(-Math.PI + 0.1, 1.0);
            Assert.That(guide.Heading, Is.EqualTo(-Math.PI + 0.1).Within(1e-6));
            guide.Heading = 0.0;
            guide.TurnToward(Math.PI / 2.0, 0.1);
            Assert.That(guide.Heading, Is.EqualTo(Guide.TurnRate * 0.1).Within(1e-6));
        }

        [Test]
        public void TrailPointBehindWalksHistoryThenExtendsBackward()
        {
            Guide guide = new Guide();
            guide.Reset(SimVec2.Zero, 0.0);
            guide.Pos = new SimVec2(0.0, 25.0);
            guide.Trail = new List<SimVec2>
            {
                SimVec2.Zero,
                new SimVec2(0.0, 10.0),
                new SimVec2(0.0, 20.0)
            };
            Assert.That(guide.TrailPointBehind(5.0), Is.EqualTo(new SimVec2(0.0, 20.0)));
            Assert.That(guide.TrailPointBehind(15.0), Is.EqualTo(new SimVec2(0.0, 10.0)));
            Assert.That(guide.TrailPointBehind(35.0), Is.EqualTo(new SimVec2(0.0, -10.0)));
        }

        [Test]
        public void TrailIsCappedAtThreeHundredNewestPoints()
        {
            Guide guide = new Guide();
            guide.Reset(SimVec2.Zero, 0.0);
            List<SimVec2> trail = new List<SimVec2>();
            for (int i = 0; i < Guide.TrailMaxPoints; ++i)
            {
                trail.Add(new SimVec2(i, 0.0));
            }

            guide.Trail = trail;
            guide.Pos = new SimVec2(1000.0, 0.0);
            guide.MoveToward(guide.Pos, 1.0, 0.0, double.NaN);
            Assert.That(guide.Trail.Count, Is.EqualTo(Guide.TrailMaxPoints));
            Assert.That(guide.Trail[0], Is.EqualTo(new SimVec2(1.0, 0.0)));
            Assert.That(guide.Trail[guide.Trail.Count - 1], Is.EqualTo(guide.Pos));
        }

        [Test]
        public void PointsAheadSamplesEachPathSegmentUpToLookahead()
        {
            Guide guide = new Guide();
            guide.Reset(SimVec2.Zero, 0.0);
            Assert.That(guide.PointsAhead(100.0), Is.Empty);
            guide.SetPath(new List<SimVec2> { SimVec2.Zero, new SimVec2(0.0, 20.0), new SimVec2(10.0, 20.0) });
            List<SimVec2> points = guide.PointsAhead(25.0, 10.0);
            Assert.That(points, Is.EqualTo(new[]
            {
                SimVec2.Zero,
                new SimVec2(0.0, 10.0),
                new SimVec2(0.0, 20.0)
            }));
            Assert.That(guide.PointsAhead(0.0, 10.0), Is.Empty);
        }
    }
}
