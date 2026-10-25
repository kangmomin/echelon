using System;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class MovementSystemTests
    {
        private static DataCatalog Catalog => DataTestAssets.Catalog;

        [Test]
        public void ElementStepStoresPreviousPositionVelocityAndArrivalHeading()
        {
            Battle b = EmptyBattle();
            try
            {
                Platoon p = LoosePlatoon(b, "us_tank_plt", new SimVec2(2700.0, 3300.0));
                foreach (Element item in p.Elements)
                {
                    item.HasMoveTarget = false;
                }

                Element e = p.Elements[0];
                SimVec2 oldPos = e.Pos;
                e.MoveTarget = b.Terrain.NearestPassable(oldPos + new SimVec2(20.0, 0.0), e.Mobility());
                e.HasMoveTarget = true;
                b.RebuildSpatial();
                b.Movement.Step(b.World);

                Assert.That(e.PrevPos, Is.EqualTo(oldPos));
                Assert.That(e.Pos, Is.Not.EqualTo(oldPos));
                Assert.That(e.Velocity.X, Is.EqualTo((e.Pos.X - oldPos.X) / SimConst.TickDt).Within(0.0001));
                Assert.That(e.Velocity.Y, Is.EqualTo((e.Pos.Y - oldPos.Y) / SimConst.TickDt).Within(0.0001));
                Assert.That(e.StationaryTicks, Is.Zero);

                SimVec2 arrivedPos = e.Pos;
                double oldHeading = e.Heading;
                e.RestHeading = oldHeading + 0.8;
                e.MoveTarget = e.Pos + new SimVec2(1.0, 0.0);
                b.Movement.Step(b.World);
                Assert.That(e.Pos, Is.EqualTo(arrivedPos));
                Assert.That(e.PrevPos, Is.EqualTo(arrivedPos));
                Assert.That(e.Velocity, Is.EqualTo(SimVec2.Zero));
                Assert.That(e.Heading, Is.Not.EqualTo(oldHeading));
                Assert.That(e.StationaryTicks, Is.EqualTo(1));
                Assert.That(e.StuckTicks, Is.Zero);
            }
            finally
            {
                b.Dispose();
            }
        }

        [Test]
        public void MountedPositionsSyncAndOffMapElementsStayOutOfSpatialHash()
        {
            Battle b = EmptyBattle();
            try
            {
                Platoon p = LoosePlatoon(b, "us_mech_plt", new SimVec2(1200.0, 500.0));
                foreach (Element item in p.Elements)
                {
                    item.HasMoveTarget = false;
                }

                Element vehicle = p.Vehicles()[0];
                Element squad = vehicle.Passengers[0];
                vehicle.Pos += new SimVec2(5.0, 0.0);
                b.Movement.Step(b.World);
                Assert.That(squad.Pos, Is.EqualTo(vehicle.Pos));
                Assert.That(squad.Heading, Is.EqualTo(vehicle.Heading));
                Assert.That(squad.Velocity, Is.EqualTo(SimVec2.Zero));

                SimVec2 entry = new SimVec2(600.0, 30.0);
                b.Factory.SetOffMap(p, entry, Math.PI);
                b.Movement.Step(b.World);
                foreach (Element item in p.Elements)
                {
                    Assert.That(item.Pos, Is.EqualTo(entry));
                    Assert.That(item.PrevPos, Is.EqualTo(entry));
                    Assert.That(item.Velocity, Is.EqualTo(SimVec2.Zero));
                    Assert.That(b.Spatial.Query(entry, 100.0), Does.Not.Contain(item));
                }
            }
            finally
            {
                b.Dispose();
            }
        }

        [Test]
        public void ForestLineAssignmentsUseWedgeAndTerrainScaling()
        {
            MapRect forest = new MapRect(700.0, 700.0, 700.0, 700.0);
            Battle b = EmptyBattle(FlatMap(forest));
            try
            {
                TaskForce blue = b.TaskForces["blue"];
                Platoon p = b.Factory.SpawnPlatoon(Catalog.GetUnitTemplate("us_tank_plt")!, "blue",
                    new SimVec2(1000.0, 1000.0), 0.0, blue);
                blue.Organic.Add(p);
                p.Formation = Catalog.GetFormation("line")!;
                b.Factory.PlaceElements(p);
                b.Movement.Step(b.World);

                var expected = FormationSolver.WorldSlots(Catalog.GetFormation("wedge")!,
                    p.FormationMembers().Count, p.Guide.Pos, p.Guide.Heading, "forest");
                var members = p.FormationMembers();
                Assert.That(members.Count, Is.EqualTo(expected.Count));
                for (int i = 0; i < members.Count; ++i)
                {
                    Assert.That(members[i].MoveTarget.X, Is.EqualTo(expected[i].X).Within(0.001));
                    Assert.That(members[i].MoveTarget.Y, Is.EqualTo(expected[i].Y).Within(0.001));
                }
            }
            finally
            {
                b.Dispose();
            }
        }

        private static Platoon LoosePlatoon(Battle b, string templateId, SimVec2 pos)
        {
            TaskForce blue = b.TaskForces["blue"];
            return b.Factory.SpawnPlatoon(Catalog.GetUnitTemplate(templateId)!, "blue", pos, 0.0, blue);
        }

        private static Battle EmptyBattle(MapDef? map = null)
        {
            Battle b = Battle.Create(Catalog, "skirmish_01", 1, true, map);
            b.Factory.CreateTaskForce("blue", Catalog.GetToe("us_armor_bn_tf")!,
                new SimVec2(200.0, 2000.0), Math.PI / 2.0);
            b.Factory.CreateTaskForce("red", Catalog.GetToe("ru_btg_bmp")!,
                new SimVec2(3800.0, 3800.0), -Math.PI / 2.0);
            return b;
        }

        private static MapDef FlatMap(MapRect forest)
        {
            MapDef map = new MapDef
            {
                Id = "movement_test_flat",
                DisplayName = "Movement test flat map",
                SizeM = new SimVec2(4000.0, 4000.0),
                CellM = 10.0,
                HeightSeed = 1,
                BaseM = 40.0,
                AmplitudeM = 0.0,
                Frequency = 0.001,
                Octaves = 1
            };
            MapDef.Feature feature = new MapDef.Feature { Name = "Test forest" };
            feature.Points.Add(forest.Position);
            feature.Points.Add(new SimVec2(forest.End.X, forest.Position.Y));
            feature.Points.Add(forest.End);
            feature.Points.Add(new SimVec2(forest.Position.X, forest.End.Y));
            map.Forests.Add(feature);
            return map;
        }
    }
}
