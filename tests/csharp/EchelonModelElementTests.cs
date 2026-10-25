using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class EchelonModelElementTests
    {
        [Test]
        public void ClassStateMountAndTargetKindFollowDefinitionAndAliveState()
        {
            Element infantry = NewElement("inf_squad");
            infantry.Id = 7;
            infantry.Def.DisplayName = "Squad";
            Element vehicle = NewElement("tank");
            Element helicopter = NewElement("heli");
            Assert.That(infantry.IsInfantry(), Is.True);
            Assert.That(infantry.IsVehicle(), Is.False);
            Assert.That(vehicle.IsVehicle(), Is.True);
            Assert.That(helicopter.IsAir(), Is.True);
            Assert.That(infantry.Mobility(), Is.EqualTo(Terrain.MobilityFoot));
            Assert.That(vehicle.Mobility(), Is.EqualTo(Terrain.MobilityVehicle));
            Assert.That(helicopter.Mobility(), Is.EqualTo(Terrain.MobilityAir));
            Assert.That(infantry.ElementClass(), Is.EqualTo("inf_squad"));
            Assert.That(infantry.DisplayName(), Is.EqualTo("Squad #7"));
            Assert.That(infantry.TargetKind(), Is.EqualTo("infantry"));
            Assert.That(vehicle.TargetKind(), Is.EqualTo("vehicle"));
            Assert.That(helicopter.TargetKind(), Is.EqualTo("air"));
            Assert.That(infantry.IsAlive(), Is.True);
            Assert.That(infantry.IsActive(), Is.True);
            infantry.OnMap = false;
            Assert.That(infantry.IsActive(), Is.False);
            Assert.That(infantry.IsAlive(), Is.True);
            infantry.MountedIn = vehicle;
            Assert.That(infantry.IsMounted(), Is.True);
            infantry.MountedIn = null;
            Assert.That(infantry.IsMounted(), Is.False);
            helicopter.State = ElementState.DESTROYED;
            Assert.That(helicopter.TargetKind(), Is.EqualTo("vehicle"));
            Assert.That(helicopter.IsAlive(), Is.False);
        }

        [Test]
        public void AirAltitudeTurnRatesAndMovementDamageMatchElementType()
        {
            Element infantry = NewElement("inf_squad");
            Element vehicle = NewElement("tank");
            Element helicopter = NewElement("heli");
            Assert.That(helicopter.AltitudeM(), Is.EqualTo(Element.AltitudeLowM));
            Platoon platoon = new Platoon();
            helicopter.Platoon = platoon;
            platoon.Sop.Altitude = "high";
            Assert.That(helicopter.AltitudeM(), Is.EqualTo(Element.AltitudeHighM));
            Assert.That(vehicle.AltitudeM(), Is.Zero);
            helicopter.State = ElementState.DESTROYED;
            Assert.That(helicopter.AltitudeM(), Is.Zero);
            Assert.That(infantry.TurnRate(), Is.EqualTo(Math.PI));
            Assert.That(vehicle.TurnRate(), Is.EqualTo(Math.PI / 3.0));
            Assert.That(helicopter.TurnRate(), Is.EqualTo(Math.PI * 0.75));

            infantry.State = ElementState.DAMAGED;
            infantry.DamageKind = "mobility";
            Assert.That(infantry.CanMove(), Is.False);
            vehicle.State = ElementState.DAMAGED;
            vehicle.DamageKind = "mobility";
            Assert.That(vehicle.CanMove(), Is.True);
            vehicle.State = ElementState.DESTROYED;
            Assert.That(vehicle.CanMove(), Is.False);
            infantry.Heading = Math.PI - 0.1;
            infantry.TurnToward(-Math.PI + 0.1, 1.0);
            Assert.That(infantry.Heading, Is.EqualTo(-Math.PI + 0.1).Within(1e-6));
        }

        [Test]
        public void StrengthAndBaseSpeedDistinguishInfantryHpAndVehicleDamage()
        {
            Element vehicle = NewElement("tank", 36.0);
            Assert.That(vehicle.Strength(), Is.EqualTo(1.0));
            Assert.That(vehicle.BaseSpeedMps(), Is.EqualTo(10.0).Within(1e-9));
            vehicle.State = ElementState.DAMAGED;
            vehicle.DamageKind = "mobility";
            vehicle.Suppression = 100.0;
            Assert.That(vehicle.Strength(), Is.EqualTo(0.6));
            Assert.That(vehicle.BaseSpeedMps(), Is.EqualTo(5.0).Within(1e-9));
            vehicle.State = ElementState.DESTROYED;
            Assert.That(vehicle.Strength(), Is.Zero);

            Element infantry = NewElement("inf_squad", 18.0, 10.0);
            infantry.Hp = 5.0;
            Assert.That(infantry.Strength(), Is.EqualTo(0.5));
            infantry.Hp = 20.0;
            Assert.That(infantry.Strength(), Is.EqualTo(1.0));
            infantry.Hp = -5.0;
            Assert.That(infantry.Strength(), Is.Zero);
            Assert.That(infantry.BaseSpeedMps(), Is.EqualTo(5.0).Within(1e-9));
        }

        [Test]
        public void MaxSpeedUsesTerrainRoadMobilityDamageAndSuppression()
        {
            MapDef map = new MapDef
            {
                SizeM = new SimVec2(200.0, 200.0),
                CellM = 10.0,
                AmplitudeM = 0.0,
                Frequency = 0.01,
                Octaves = 1
            };
            MapDef.Feature road = new MapDef.Feature { WidthM = 20.0 };
            road.Points.Add(new SimVec2(0.0, 15.0));
            road.Points.Add(new SimVec2(200.0, 15.0));
            map.Roads.Add(road);
            Terrain terrain = Terrain.Build(map);
            Element vehicle = NewElement("tank", 36.0);
            vehicle.Def.SpeedRoadKmh = 72.0;
            vehicle.Pos = new SimVec2(15.0, 50.0);
            Assert.That(terrain.TypeAt(vehicle.Pos), Is.EqualTo((int)Terrain.Type.OPEN));
            Assert.That(vehicle.MaxSpeedMps(terrain), Is.EqualTo(10.0).Within(1e-9));
            vehicle.Suppression = 50.0;
            Assert.That(vehicle.MaxSpeedMps(terrain), Is.EqualTo(7.0).Within(1e-9));
            vehicle.State = ElementState.DAMAGED;
            vehicle.DamageKind = "mobility";
            Assert.That(vehicle.MaxSpeedMps(terrain), Is.EqualTo(3.5).Within(1e-9));
            vehicle.Pos = new SimVec2(15.0, 15.0);
            Assert.That(terrain.TypeAt(vehicle.Pos), Is.EqualTo((int)Terrain.Type.ROAD));
            Assert.That(vehicle.MaxSpeedMps(terrain), Is.EqualTo(7.0).Within(1e-9));
        }

        [Test]
        public void ClearLocalPathCancelsRequestAndResetsPathState()
        {
            Element element = NewElement("tank");
            element.LocalPath = new List<SimVec2> { SimVec2.Zero, SimVec2.One };
            element.LocalPathIndex = 1;
            GridPathfinder.PathRequest request = new GridPathfinder.PathRequest();
            element.PathRequest = request;
            element.ClearLocalPath();
            Assert.That(element.LocalPath, Is.Empty);
            Assert.That(element.LocalPathIndex, Is.Zero);
            Assert.That(request.Cancelled, Is.True);
            Assert.That(element.PathRequest, Is.Null);
        }

        private static Element NewElement(string elementClass, double offroadKmh = 36.0, double hp = 100.0)
        {
            return new Element
            {
                Def = new ElementDef
                {
                    ElementClass = elementClass,
                    DisplayName = elementClass,
                    Hp = hp,
                    SpeedOffroadKmh = offroadKmh,
                    SpeedRoadKmh = offroadKmh * 2.0
                },
                Hp = hp
            };
        }
    }
}
