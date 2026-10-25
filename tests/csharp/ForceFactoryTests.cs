using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class ForceFactoryTests
    {
        private static DataCatalog Catalog => TerrainTestSupport.Catalog.Value;

        [Test]
        public void SandboxCreatesStableIdsCompositionAndCommanders()
        {
            Battle b = BattleSetup.Sandbox(Catalog, 7);
            try
            {
                for (int i = 0; i < b.Units.Count; ++i)
                {
                    Assert.That(b.Units[i].Id, Is.EqualTo(i + 1), "unit registration order");
                }

                for (int i = 0; i < b.Elements.Count; ++i)
                {
                    Element e = b.Elements[i];
                    Assert.That(e.Id, Is.EqualTo(i + 1), "element registration order");
                    Assert.That(e.IndexInPlatoon, Is.EqualTo(e.Platoon!.Elements.IndexOf(e)));
                    Assert.That(e.Hp, Is.EqualTo(e.Def.Hp));
                    Assert.That(e.SupplyLeft, Is.EqualTo(e.Def.SupplyCapacity));
                    Assert.That(e.PrevPos, Is.EqualTo(e.Pos));
                    foreach (KeyValuePair<string, int> pair in e.Def.Ammo)
                    {
                        Assert.That(e.Ammo[pair.Key], Is.EqualTo(pair.Value));
                    }

                    foreach (string weapon in e.Def.Weapons)
                    {
                        Assert.That(e.Reload[weapon], Is.Zero);
                    }
                }

                TaskForce blue = b.TaskForces["blue"];
                TaskForce red = b.TaskForces["red"];
                Assert.That(b.Units[0], Is.SameAs(blue));
                Assert.That(b.Units[1], Is.SameAs(blue.CpSection));
                Assert.That(b.Units[2], Is.SameAs(red));
                Assert.That(b.Units[3], Is.SameAs(red.CpSection));
                Assert.That(b.Units[4], Is.SameAs(blue.Companies[0]));
                Assert.That(b.Units[5], Is.SameAs(blue.Companies[0].Hq));
                Assert.That(b.Units[6], Is.SameAs(blue.Companies[0].Platoons[0]));
                Assert.That(blue.Companies.Count, Is.EqualTo(2));
                Assert.That(blue.Organic.Count, Is.EqualTo(1));
                Assert.That(red.Companies.Count, Is.EqualTo(2));
                Assert.That(blue.Commander, Is.SameAs(blue.CpSection!.Elements[0]));
                Assert.That(blue.CpSection.IsHqSection, Is.True);

                Company company = blue.Companies[0];
                Assert.That(company.ShortName, Is.EqualTo("A"));
                Assert.That(company.DisplayName, Does.StartWith("A 중대 ("));
                Assert.That(company.Hq!.ShortName, Is.EqualTo("A/HQ"));
                Assert.That(company.Hq.IsHqSection, Is.True);
                Assert.That(company.Commander, Is.SameAs(company.Hq.Elements[0]));
                Assert.That(company.Xo, Is.SameAs(company.Hq.Elements[1]));
                Assert.That(company.InitialElementCount, Is.EqualTo(company.AllElements().Count));
                Assert.That(company.Platoons.Count, Is.EqualTo(3));
                for (int i = 0; i < company.Platoons.Count; ++i)
                {
                    Platoon p = company.Platoons[i];
                    Assert.That(p.Parent, Is.SameAs(company));
                    Assert.That(p.ShortName, Is.EqualTo("A/" + (i + 1)));
                    Assert.That(p.DisplayName, Is.EqualTo("A 중대 " + (i + 1) + "소대"));
                    Assert.That(p.Commander, Is.SameAs(p.Elements[0]));
                    Assert.That(p.InitialElementCount, Is.EqualTo(p.Elements.Count));
                }
            }
            finally
            {
                b.Dispose();
            }
        }

        [Test]
        public void SpawnPlatoonAppliesLoadoutsSeatsPassengersAndInitializesEngineers()
        {
            Battle b = EmptyBattle();
            try
            {
                TaskForce blue = b.TaskForces["blue"];
                Platoon mech = b.Factory.SpawnPlatoon(Catalog.GetUnitTemplate("us_mech_plt")!, "blue",
                    new SimVec2(1200.0, 500.0), 0.25, blue, loadout: "jav2");
                Assert.That(mech.Loadout, Is.EqualTo("jav2"));
                Assert.That(mech.Elements.Count, Is.EqualTo(8));
                Assert.That(mech.Mounted, Is.True);
                List<Element> vehicles = mech.Vehicles();
                List<Element> squads = mech.Squads();
                Assert.That(vehicles.Count, Is.EqualTo(4));
                Assert.That(squads.Count, Is.EqualTo(4));
                for (int i = 0; i < vehicles.Count; ++i)
                {
                    Assert.That(squads[i].Def.Id, Is.EqualTo("us_inf_squad_jav2"));
                    Assert.That(squads[i].MountedIn, Is.SameAs(vehicles[i]));
                    Assert.That(vehicles[i].Passengers, Is.EqualTo(new[] { squads[i] }));
                    Assert.That(squads[i].Pos, Is.EqualTo(vehicles[i].Pos));
                    Assert.That(squads[i].PrevPos, Is.EqualTo(vehicles[i].Pos));
                }

                Platoon tank = b.Factory.SpawnPlatoon(Catalog.GetUnitTemplate("us_tank_plt")!, "blue",
                    new SimVec2(1500.0, 500.0), 0.0, blue, loadout: "tusk");
                Assert.That(tank.Loadout, Is.EqualTo("tusk"));
                foreach (Element e in tank.Elements)
                {
                    Assert.That(e.Def.Id, Is.EqualTo("us_m1a2_sepv3_tusk"));
                }

                Platoon foot = b.Factory.SpawnPlatoon(Catalog.GetUnitTemplate("us_light_inf_plt")!, "blue",
                    new SimVec2(1800.0, 500.0), 0.0, blue);
                Assert.That(foot.Mounted, Is.False);
                Platoon engineer = b.Factory.SpawnPlatoon(Catalog.GetUnitTemplate("us_engineer_plt")!, "blue",
                    new SimVec2(2100.0, 500.0), 0.0, blue);
                Assert.That(engineer.MinesLeft, Is.EqualTo(2));
            }
            finally
            {
                b.Dispose();
            }
        }

        [Test]
        public void SkirmishBuildsWavesAttachmentsAndDeploymentWithoutMutatingSpecs()
        {
            ForceSpec blueSpec = Catalog.DefaultForce("US")!;
            ForceSpec redSpec = Catalog.DefaultForce("RU")!;
            Battle b = BattleSetup.Skirmish(Catalog, blueSpec, redSpec, seedValue: 19,
                aiSides: new[] { "blue", "red" });
            try
            {
                Assert.That(b.Deploying, Is.True);
                Assert.That(b.World.Paused, Is.True);
                Assert.That(b.TfAi.Sides, Is.EqualTo(new[] { "blue", "red" }));
                Assert.That(blueSpec.Deploy, Is.Empty);
                Assert.That(redSpec.Deploy, Is.Empty);

                TaskForce tf = b.TaskForces["blue"];
                ForceRules.Report report = ForceRules.Evaluate(Catalog, blueSpec, 2000);
                Assert.That(tf.Companies.Count, Is.EqualTo(2));
                Assert.That(tf.Organic.Count, Is.EqualTo(5));
                Assert.That(tf.AllElements().Count, Is.EqualTo(report.ElementCount));
                Assert.That(tf.ArtilleryMissions, Is.EqualTo(blueSpec.ArtilleryMissions));
                Assert.That(tf.CasSorties, Is.EqualTo(blueSpec.CasSorties));
                for (int i = 0; i < tf.Organic.Count; ++i)
                {
                    Assert.That(tf.Organic[i].Attached, Is.EqualTo(i >= 3));
                }

                Company reserveCompany = tf.Companies[1];
                Platoon reserve = reserveCompany.Platoons[2];
                Assert.That(reserveCompany.OnMap, Is.True);
                Assert.That(reserveCompany.Hq!.OnMap, Is.True);
                Assert.That(reserve.Phase, Is.EqualTo(1));
                Assert.That(reserve.OnMap, Is.False);
                foreach (Element e in reserve.Elements)
                {
                    Assert.That(e.OnMap, Is.False);
                    Assert.That(e.Pos, Is.EqualTo(tf.EntryPos));
                    Assert.That(e.PrevPos, Is.EqualTo(tf.EntryPos));
                    Assert.That(e.HasMoveTarget, Is.False);
                    Assert.That(e.Velocity, Is.EqualTo(SimVec2.Zero));
                }

                MapRect zone = b.Scenario.Sides["blue"].DeployZone;
                foreach (Element e in tf.AllElements())
                {
                    if (e.OnMap)
                    {
                        Assert.That(zone.Grow(30.0).Contains(e.Pos), Is.True, "initial element " + e.Id);
                    }
                }

                SimVec2 expectedEntry = b.Terrain.NearestPassable(
                    b.Terrain.ClampToMap(b.Scenario.Sides["blue"].EntryPoints[0], 30.0), Terrain.MobilityVehicle);
                Assert.That(tf.EntryPos, Is.EqualTo(expectedEntry));

                TaskForce red = b.TaskForces["red"];
                Company reserveCompanyRed = red.Companies[2];
                Assert.That(reserveCompanyRed.Phase, Is.EqualTo(1));
                Assert.That(reserveCompanyRed.OnMap, Is.False);
                Assert.That(reserveCompanyRed.Hq!.OnMap, Is.False);
                Assert.That(reserveCompanyRed.Guide.Pos, Is.EqualTo(red.EntryPos));
                foreach (Platoon p in reserveCompanyRed.Platoons)
                {
                    Assert.That(p.OnMap, Is.False);
                    Assert.That(p.Phase, Is.EqualTo(1));
                }
            }
            finally
            {
                b.Dispose();
            }
        }

        [Test]
        public void CrossAttachedPlatoonKeepsHomeCompanyNameAndAdHocFlags()
        {
            ForceSpec spec = Catalog.DefaultForce("US")!;
            Assert.That(spec.MovePlatoon("co_a", 0, "co_b"), Is.True);
            Battle b = BattleSetup.Skirmish(Catalog, spec, Catalog.DefaultForce("RU")!);
            try
            {
                TaskForce tf = b.TaskForces["blue"];
                Platoon crossAttached = tf.Companies[1].Platoons[3];
                Assert.That(crossAttached.DisplayName, Does.Contain("(A 중대에서 배속)"));
                Assert.That(tf.Companies[0].AdHoc, Is.False);
                Assert.That(tf.Companies[1].AdHoc, Is.False);
                Assert.That(crossAttached.AdHoc, Is.False);
            }
            finally
            {
                b.Dispose();
            }
        }

        [Test]
        public void DeploymentClampsCompaniesAndRejectsReserves()
        {
            Battle b = BattleSetup.Skirmish(Catalog, Catalog.DefaultForce("US")!, Catalog.DefaultForce("RU")!);
            try
            {
                TaskForce tf = b.TaskForces["blue"];
                Company company = tf.Companies[0];
                Platoon reserve = tf.Companies[1].Platoons[2];
                SimVec2 reservePos = reserve.Guide.Pos;
                MapRect zone = b.Scenario.Sides["blue"].DeployZone;

                Assert.That(b.DeployUnit(company, new SimVec2(1000.0, 3000.0), 0.4), Is.True);
                Assert.That(zone.Contains(company.Guide.Pos), Is.True);
                Assert.That(company.Guide.Heading, Is.EqualTo(0.4).Within(0.0001));
                Assert.That(b.DeployUnit(company.Platoons[0], new SimVec2(2000.0, 400.0), 0.0), Is.True);
                Assert.That(company.Guide.Pos.DistanceTo(new SimVec2(2000.0, 400.0)), Is.LessThan(30.0));
                Assert.That(reserve.Guide.Pos, Is.EqualTo(reservePos));
                Assert.That(b.DeployUnit(reserve, new SimVec2(2000.0, 400.0), 0.0), Is.False);

                b.BeginBattle();
                Assert.That(b.Deploying, Is.False);
                Assert.That(b.World.Paused, Is.False);
                Assert.That(b.DeployUnit(company, new SimVec2(1500.0, 400.0), 0.0), Is.False);
            }
            finally
            {
                b.Dispose();
            }
        }

        [Test]
        public void TutorialParksAndRevealsEnemyWithSupportConfiguration()
        {
            Battle b = BattleSetup.Tutorial(Catalog);
            try
            {
                TaskForce blue = b.TaskForces["blue"];
                TaskForce red = b.TaskForces["red"];
                Assert.That(blue.ArtilleryMissions, Is.EqualTo(1));
                Assert.That(blue.CasSorties, Is.EqualTo(1));
                Platoon enemy = red.Organic[0];
                Assert.That(enemy.Phase, Is.EqualTo(99));
                Assert.That(enemy.OnMap, Is.False);
                Assert.That(enemy.Guide.Pos, Is.EqualTo(new SimVec2(3900.0, 3950.0)));
                foreach (Element e in enemy.Elements)
                {
                    Assert.That(e.OnMap, Is.False);
                    Assert.That(e.Pos, Is.EqualTo(enemy.Guide.Pos));
                }

                BattleSetup.RevealPlatoon(b, enemy, BattleSetup.TutorialEnemy, Math.PI / 2.0);
                Assert.That(enemy.OnMap, Is.True);
                Assert.That(enemy.Posture, Is.EqualTo("idle"));
                Assert.That(enemy.Guide.Pos, Is.EqualTo(BattleSetup.TutorialEnemy));
                foreach (Element e in enemy.FormationMembers())
                {
                    Assert.That(e.OnMap, Is.True);
                    Assert.That(e.HasMoveTarget, Is.True);
                    Assert.That(e.PrevPos, Is.EqualTo(e.Pos));
                    Assert.That(e.PrevHeading, Is.EqualTo(e.Heading));
                }
            }
            finally
            {
                b.Dispose();
            }
        }

        private static Battle EmptyBattle()
        {
            Battle b = Battle.Create(Catalog, "skirmish_01", 1, true);
            b.Factory.CreateTaskForce("blue", Catalog.GetToe("us_armor_bn_tf")!,
                new SimVec2(2000.0, 150.0), 0.0);
            b.Factory.CreateTaskForce("red", Catalog.GetToe("ru_btg_bmp")!,
                new SimVec2(2000.0, 3850.0), Math.PI);
            return b;
        }
    }
}
