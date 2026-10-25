using System;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class ForceDeploymentTests
    {
        private static readonly MapRect BlueZone = new MapRect(0, 0, 4000, 800);
        private static readonly MapRect RedZone = new MapRect(0, 3200, 4000, 800);
        private static readonly SimVec2 MapSize = new SimVec2(4000, 4000);

        [Test]
        public void FacingAndZonePointsUseNorthMapCoordinates()
        {
            Assert.That(Deployment.Facing(BlueZone, MapSize), Is.Zero);
            Assert.That(Deployment.Facing(RedZone, MapSize), Is.EqualTo(Math.PI).Within(0.00001));
            SimVec2 blueCompany = Deployment.ZonePoint(BlueZone, 0.0, 0.5, Deployment.FrontInsetM);
            Assert.That(blueCompany.X, Is.EqualTo(2000.0).Within(0.001));
            Assert.That(blueCompany.Y, Is.EqualTo(470.0).Within(0.001));
            SimVec2 redCompany = Deployment.ZonePoint(RedZone, Math.PI, 0.5, Deployment.FrontInsetM);
            Assert.That(redCompany.Y, Is.EqualTo(3530.0).Within(0.001));
        }

        [Test]
        public void AutoFillOnlyAddsMissingInitialWavePoses()
        {
            ForceSpec spec = DataTestAssets.Us();
            CompanySpec reserve = ForceSpec.MakeCompany(DataTestAssets.Catalog, "co_c", "us_tank_co");
            foreach (PlatoonSpec platoon in reserve.Platoons)
            {
                platoon.Wave = 1;
            }

            spec.Companies.Add(reserve);
            spec.Organic[0].Wave = 1;
            DeploymentPose existing = new DeploymentPose(123, 456, 0.7);
            spec.Deploy["cp"] = existing;
            Deployment.AutoFill(spec, BlueZone, MapSize);

            Assert.That(spec.Deploy["cp"], Is.EqualTo(existing));
            Assert.That(spec.Deploy.ContainsKey("co:co_a"), Is.True);
            Assert.That(spec.Deploy.ContainsKey("co:co_b"), Is.True);
            Assert.That(spec.Deploy.ContainsKey("co:co_c"), Is.False);
            Assert.That(spec.Deploy.ContainsKey("org:0"), Is.False);
            Assert.That(spec.Deploy.ContainsKey("org:1"), Is.True);
            Assert.That(spec.Deploy.ContainsKey("org:2"), Is.True);
            Assert.That(spec.Deploy.ContainsKey("att:0"), Is.True);
            Assert.That(spec.Deploy.ContainsKey("att:1"), Is.True);
            Assert.That(spec.Deploy["co:co_a"].North, Is.EqualTo(470.0).Within(0.001));
            Assert.That(spec.Deploy["co:co_a"].Heading, Is.Zero);
            Assert.That(spec.Deploy["org:1"].North, Is.EqualTo(170.0).Within(0.001));

            ForceSpec automaticCp = DataTestAssets.Us();
            Deployment.AutoFill(automaticCp, BlueZone, MapSize);
            Assert.That(automaticCp.Deploy["cp"].Position, Is.EqualTo(new SimVec2(2000, 150)));
        }

        [Test]
        public void StoredPoseHeadingMatchesGodotVector3Precision()
        {
            ForceSpec spec = DataTestAssets.Us();
            Deployment.AutoFill(spec, RedZone, MapSize);

            Assert.That(spec.Deploy["cp"].Heading, Is.EqualTo((double)(float)Math.PI));
        }

        [Test]
        public void ClampAndValidityUseZoneAndPassability()
        {
            SimVec2 clamped = Deployment.ClampToZone(new SimVec2(-100, 900), BlueZone);
            Assert.That(clamped, Is.EqualTo(new SimVec2(20, 780)));
            Assert.That(Deployment.IsValidPoint(clamped, BlueZone, null), Is.True);
            Assert.That(Deployment.IsValidPoint(new SimVec2(4000, 400), BlueZone, null), Is.False);

            FakeTerrain terrain = new FakeTerrain(false, new SimVec2(100, 100));
            Assert.That(Deployment.ClampToZone(new SimVec2(500, 500), BlueZone, terrain),
                Is.EqualTo(new SimVec2(100, 100)));
            Assert.That(Deployment.IsValidPoint(new SimVec2(500, 500), BlueZone, terrain), Is.False);
        }

        private sealed class FakeTerrain : IDeploymentTerrain
        {
            private readonly bool _passable;
            private readonly SimVec2 _nearest;

            internal FakeTerrain(bool passable, SimVec2 nearest)
            {
                _passable = passable;
                _nearest = nearest;
            }

            public bool IsPassable(SimVec2 point)
            {
                return _passable;
            }

            public SimVec2 NearestPassable(SimVec2 point)
            {
                return _nearest;
            }
        }
    }
}
