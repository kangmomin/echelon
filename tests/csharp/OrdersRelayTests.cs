using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class OrdersRelayTests
    {
        [Test]
        public void QualityCurveRangeAttenuationJamAndDelayFactorMatchGodot()
        {
            Assert.That(CommsNet.QualityFor(60.0, 100.0, 1.0, 1.0), Is.EqualTo(1.0));
            Assert.That(CommsNet.QualityFor(80.0, 100.0, 1.0, 1.0), Is.EqualTo(0.65).Within(1e-9));
            Assert.That(CommsNet.QualityFor(100.0, 100.0, 1.0, 1.0), Is.EqualTo(0.3).Within(1e-9));
            Assert.That(CommsNet.QualityFor(100.01, 100.0, 1.0, 1.0), Is.EqualTo(0.0));
            Assert.That(CommsNet.QualityFor(80.0, 100.0, 0.8, 1.0), Is.EqualTo(0.3).Within(1e-9));
            Assert.That(CommsNet.QualityFor(80.01, 100.0, 0.8, 1.0), Is.EqualTo(0.0));
            Assert.That(CommsNet.QualityFor(60.0, 100.0, 1.0, CommsNet.JamFactor), Is.EqualTo(0.3));

            Assert.That(CommsNet.DelayFactor(1.0), Is.EqualTo(1.0));
            Assert.That(CommsNet.DelayFactor(0.3), Is.EqualTo(1.7).Within(1e-9));
            Assert.That(CommsNet.DelayFactor(0.0), Is.EqualTo(2.0));
            Assert.That(CommsNet.DelayFactor(-1.0), Is.EqualTo(2.0));
            Assert.That(CommsNet.DelayFactor(2.0), Is.EqualTo(1.0));
        }

        [Test]
        public void TerrainAttenuationMultipliesForestUrbanAndRidge()
        {
            Assert.That(CommsNet.TerrainAttenuation(true, false, false), Is.EqualTo(0.8));
            Assert.That(CommsNet.TerrainAttenuation(false, true, false), Is.EqualTo(0.7));
            Assert.That(CommsNet.TerrainAttenuation(false, false, true), Is.EqualTo(0.5));
            Assert.That(CommsNet.TerrainAttenuation(true, true, true), Is.EqualTo(0.28).Within(1e-9));
        }

        [Test]
        public void DirectLinkHasNoRelayAndStateThresholdIncludesRelayCount()
        {
            Dictionary<int, List<CommsEdge>> edges = new Dictionary<int, List<CommsEdge>>
            {
                [1] = new List<CommsEdge> { new CommsEdge(2, 0.8) }
            };
            CommsLink direct = CommsNet.FindLink(1, 2, edges, new HashSet<int>());
            Assert.That(direct.Ok, Is.True);
            Assert.That(direct.Quality, Is.EqualTo(0.8));
            Assert.That(direct.Relays, Is.EqualTo(0));
            Assert.That(CommsNet.StateOf(direct), Is.EqualTo(Unit.Comms.Good));

            direct.Relays = 1;
            Assert.That(CommsNet.StateOf(direct), Is.EqualTo(Unit.Comms.Poor));
            Assert.That(CommsNet.StateOf(CommsNet.FindLink(1, 9, edges, new HashSet<int>())), Is.EqualTo(Unit.Comms.Cut));

            CommsLink same = CommsNet.FindLink(1, 1, edges, new HashSet<int>());
            Assert.That(same.Ok, Is.True);
            Assert.That(same.Quality, Is.EqualTo(1.0));
            Assert.That(same.Relays, Is.EqualTo(0));
        }

        [Test]
        public void BreadthFirstRelayIsDeterministicAndUsesBottleneckQuality()
        {
            Dictionary<int, List<CommsEdge>> edges = new Dictionary<int, List<CommsEdge>>
            {
                [1] = new List<CommsEdge> { new CommsEdge(2, 0.9), new CommsEdge(3, 0.4) },
                [2] = new List<CommsEdge> { new CommsEdge(4, 0.8) },
                [3] = new List<CommsEdge> { new CommsEdge(4, 0.95) },
                [4] = new List<CommsEdge> { new CommsEdge(5, 0.7) }
            };
            HashSet<int> network = new HashSet<int> { 2, 3, 4 };

            CommsLink first = CommsNet.FindLink(1, 5, edges, network);
            CommsLink second = CommsNet.FindLink(1, 5, edges, network);
            Assert.That(first.Ok, Is.True);
            Assert.That(first.Relays, Is.EqualTo(2));
            Assert.That(first.Quality, Is.EqualTo(0.7).Within(1e-9));
            Assert.That(second.Quality, Is.EqualTo(first.Quality));
            Assert.That(second.Relays, Is.EqualTo(first.Relays));
        }

        [Test]
        public void RelayRejectsNodesOutsideSendersNetwork()
        {
            Dictionary<int, List<CommsEdge>> edges = new Dictionary<int, List<CommsEdge>>
            {
                [1] = new List<CommsEdge> { new CommsEdge(2, 0.9) },
                [2] = new List<CommsEdge> { new CommsEdge(3, 0.9) }
            };
            CommsLink link = CommsNet.FindLink(1, 3, edges, new HashSet<int>());
            Assert.That(link.Ok, Is.False);

            link = CommsNet.FindLink(1, 3, edges, new HashSet<int> { 2 });
            Assert.That(link.Ok, Is.True);
            Assert.That(link.Relays, Is.EqualTo(1));
        }

        [Test]
        public void RelayLimitAllowsThreeIntermediariesButNotFour()
        {
            Dictionary<int, List<CommsEdge>> edges = new Dictionary<int, List<CommsEdge>>();
            for (int i = 1; i <= 5; ++i)
            {
                edges[i] = new List<CommsEdge> { new CommsEdge(i + 1, 0.9) };
            }

            HashSet<int> network = new HashSet<int> { 2, 3, 4, 5 };
            CommsLink allowed = CommsNet.FindLink(1, 5, edges, network);
            Assert.That(allowed.Ok, Is.True);
            Assert.That(allowed.Relays, Is.EqualTo(3));
            Assert.That(CommsNet.FindLink(1, 6, edges, network).Ok, Is.False);
        }
    }
}
