using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    [Category("Support")]
    public sealed class SupportCompanyTests
    {
        private static readonly string[] SupportTemplates =
        {
            "supply_co", "artillery_co", "sam_co", "aa_gun_co", "heli_co", "air_wing"
        };

        private static DataCatalog Catalog => TestFixture.Catalog();

        [TestCase("US", "us_armor_bn_tf")]
        [TestCase("RU", "ru_btg_bmp")]
        public void EverySupportCompanyFitsASupportSlotWithinBudget(string faction, string toeId)
        {
            string prefix = faction.ToLowerInvariant();
            foreach (string kind in SupportTemplates)
            {
                ForceSpec spec = ForceSpec.FromToe(Catalog, toeId);
                spec.Companies.Add(ForceSpec.MakeCompany(Catalog, "sup_1", prefix + "_" + kind));
                // 기본 편성이 예산(2,000)을 거의 다 써서, 지원 중대를 넣으려면 다른 것을 빼야 한다. 여기서는 구조만 본다.
                ForceRules.Report report = ForceRules.Evaluate(Catalog, spec, 100000);
                Assert.That(report.Errors, Is.Empty, kind + ": " + string.Join(" / ", report.Errors));
                int cost = Catalog.GetUnitTemplate(prefix + "_" + kind)!.Cost;
                Assert.That(cost, Is.InRange(50, 600), kind);
                TestContext.Out.WriteLine("{0}_{1}: {2}", prefix, kind, cost);
            }
        }

        [Test]
        public void SupportSlotRejectsManeuverCompanies()
        {
            ForceSpec spec = ForceSpec.FromToe(Catalog, "us_armor_bn_tf");
            spec.Companies.Add(ForceSpec.MakeCompany(Catalog, "sup_1", "us_tank_co"));
            Assert.That(ForceRules.Evaluate(Catalog, spec, 2000).IsValid(), Is.False);
        }

        [Test]
        public void SupportCompaniesDeployBehindManeuverCompanies()
        {
            using Battle battle = Build(("sup_1", "us_supply_co"));
            TaskForce blue = battle.TaskForces["blue"];
            Company supply = blue.Companies.Single(c => c.Kind == "supply");
            foreach (Company company in blue.Companies.Where(c => !c.IsSupport))
            {
                Assert.That(supply.Guide.Pos.Y, Is.LessThan(company.Guide.Pos.Y), company.DisplayName);
            }
        }

        [Test]
        public void AirWingFlightsWaitOffMapAndLaunchWithoutCommandDelay()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            Platoon fighters = Flight(battle, "us_fighter_flight");
            Assert.That(fighters.OnMap, Is.False);
            Assert.That(fighters.Elements.All(e => !e.OnMap && e.Air == AirState.READY), Is.True);

            SimVec2 target = new SimVec2(2000.0, 2000.0);
            Order order = TestFixture.Move(battle, fighters, target);
            battle.RunTicks(2);

            Assert.That(order.ReceivedTick - order.TickIssued, Is.LessThanOrEqualTo(1), "no propagation delay");
            Assert.That(order.Cost, Is.EqualTo(0.0));
            Assert.That(fighters.OnMap, Is.True);
            foreach (Element plane in fighters.Elements)
            {
                Assert.That(plane.OnMap, Is.True);
                Assert.That(plane.Air, Is.EqualTo(AirState.AIRBORNE));
                Assert.That(plane.Pos.Y, Is.LessThan(100.0), "enters from the friendly (south) edge");
                Assert.That(plane.AltitudeM(), Is.EqualTo(plane.Def.AltitudeM));
            }
        }

        [Test]
        public void PlaneKeepsFlyingAroundTargetInsteadOfStopping()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            ClearRedAirDefense(battle);
            Platoon fighters = Flight(battle, "us_fighter_flight");
            SimVec2 target = new SimVec2(2000.0, 1200.0);
            TestFixture.Move(battle, fighters, target);
            battle.RunSeconds(40.0);
            Element plane = fighters.Elements[0];
            double radius = plane.MaxSpeedMps(battle.Terrain) / plane.TurnRate();
            for (int i = 0; i < 20; ++i)
            {
                battle.RunSeconds(1.0);
                Assert.That(plane.Velocity.Length(), Is.GreaterThan(100.0), "never hovers");
                Assert.That(plane.Pos.DistanceTo(fighters.Guide.Pos), Is.LessThan(radius * 3.0), "stays over the target");
            }
        }

        [Test]
        public void FlightReturnsOnBingoFuelAndRearmsBeforeTheNextSortie()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            ClearRedAirDefense(battle);
            Platoon fighters = Flight(battle, "us_fighter_flight");
            Element plane = fighters.Elements[0];
            TestFixture.Move(battle, fighters, new SimVec2(2000.0, 2000.0));
            plane.Ammo["aim120d"] = 0;

            battle.RunSeconds(plane.Def.EnduranceSec + 1.0);
            Assert.That(plane.Air, Is.EqualTo(AirState.RETURNING).Or.EqualTo(AirState.REARMING));
            battle.RunSeconds(60.0);
            Assert.That(plane.Air, Is.EqualTo(AirState.REARMING));
            Assert.That(plane.OnMap, Is.False);
            Assert.That(fighters.OnMap, Is.False);

            Order early = TestFixture.Move(battle, fighters, new SimVec2(2000.0, 2000.0));
            battle.RunTicks(2);
            Assert.That(early.State, Is.EqualTo(OrderState.CANCELLED), "cannot launch while rearming");

            battle.RunSeconds(plane.Def.RearmSec);
            Assert.That(plane.Air, Is.EqualTo(AirState.READY));
            Assert.That(plane.Ammo["aim120d"], Is.EqualTo(plane.Def.Ammo["aim120d"]), "rearmed");
            TestFixture.Move(battle, fighters, new SimVec2(2000.0, 2000.0));
            battle.RunTicks(2);
            Assert.That(plane.Air, Is.EqualTo(AirState.AIRBORNE));
            Assert.That(plane.FuelSec, Is.EqualTo(plane.Def.EnduranceSec).Within(0.5));
        }

        [Test]
        public void WithdrawOrderSendsTheFlightHome()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            ClearRedAirDefense(battle);
            Platoon attack = Flight(battle, "us_attack_flight");
            TestFixture.Move(battle, attack, new SimVec2(2000.0, 1200.0));
            battle.RunSeconds(10.0);
            TestFixture.Move(battle, attack, new SimVec2(2000.0, 1200.0), OrderType.WITHDRAW);
            battle.RunTicks(2);
            Assert.That(attack.Elements.All(e => e.Air == AirState.RETURNING), Is.True);
            battle.RunSeconds(60.0);
            Assert.That(attack.OnMap, Is.False);
        }

        [Test]
        public void CompanyOrderLaunchesEveryReadyFlight()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            ClearRedAirDefense(battle);
            Company wing = battle.TaskForces["blue"].Companies.Single(c => c.Kind == "fixed_wing");
            TestFixture.Move(battle, wing, new SimVec2(2000.0, 1500.0));
            battle.RunTicks(2);
            foreach (Platoon flight in wing.Platoons.Where(p => p.IsAirWing))
            {
                Assert.That(flight.OnMap, Is.True, flight.DisplayName);
            }

            Assert.That(wing.Hq!.Elements[0].Pos.Y, Is.LessThan(1000.0), "ground air-control team stays put");
        }

        [Test]
        public void FighterShootsDownEnemyHelicopters()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            ClearRedAirDefense(battle);
            Platoon helis = TestFixture.Platoon(battle, "ru_aviation_sec", "red", new SimVec2(2000.0, 2200.0), Math.PI);
            Platoon fighters = Flight(battle, "us_fighter_flight");
            TestFixture.Move(battle, fighters, new SimVec2(2000.0, 1800.0));
            battle.RunSeconds(120.0);
            Assert.That(helis.Elements.Count(e => !e.IsAlive()), Is.GreaterThan(0));
            Assert.That(fighters.Elements.Sum(e => e.Ammo["aim120d"] + e.Ammo["aim9x"]), Is.LessThan(12));
        }

        [Test]
        public void SamShootsDownAttackJets()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            TestFixture.Platoon(battle, "ru_ada_section", "red", new SimVec2(2000.0, 2600.0), Math.PI);
            TestFixture.Platoon(battle, "ru_ada_section", "red", new SimVec2(2200.0, 2600.0), Math.PI);
            Platoon attack = Flight(battle, "us_attack_flight");
            TestFixture.Move(battle, attack, new SimVec2(2000.0, 2400.0), OrderType.ATTACK_MOVE);
            battle.RunSeconds(120.0);
            Assert.That(attack.Elements.Count(e => !e.IsAlive()), Is.GreaterThan(0));
        }

        [Test]
        public void AttackJetsHitGroundVehicles()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            ClearRedAirDefense(battle);
            Platoon tanks = TestFixture.Platoon(battle, "ru_tank_plt", "red", new SimVec2(2000.0, 2000.0), Math.PI);
            Platoon attack = Flight(battle, "us_attack_flight");
            TestFixture.Move(battle, attack, new SimVec2(2000.0, 1900.0), OrderType.ATTACK_MOVE);
            battle.RunSeconds(120.0);
            Assert.That(attack.Elements.Sum(e => e.Ammo["agm65_maverick"]), Is.LessThan(8), "fired mavericks");
            Assert.That(tanks.Elements.Count(e => e.State != ElementState.OK), Is.GreaterThan(0));
        }

        [Test]
        public void AirdropResuppliesUnitsUnderTheDropPointAndTheFlightGoesHome()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            ClearRedAirDefense(battle);
            Platoon tanks = battle.TaskForces["blue"].Companies.First(c => c.Kind == "tank").Platoons[0];
            foreach (Element tank in tanks.Elements)
            {
                tank.Ammo["m256_apfsds"] = 0;
            }

            Platoon airlift = Flight(battle, "us_airlift_flight");
            Order order = TestFixture.Move(battle, airlift, tanks.Centroid(), OrderType.AIRDROP);
            battle.RunTicks(2);
            Assert.That(order.State, Is.Not.EqualTo(OrderState.CANCELLED));
            battle.RunSeconds(60.0);
            Assert.That(tanks.Elements.Sum(e => e.Ammo["m256_apfsds"]), Is.GreaterThan(0), "ammo dropped");
            Assert.That(airlift.Elements[0].SupplyLeft, Is.EqualTo(0.0));
            Assert.That(airlift.Elements[0].Air, Is.EqualTo(AirState.RETURNING).Or.EqualTo(AirState.REARMING));
        }

        [Test]
        public void AirdropIsRefusedForFlightsWithoutCargo()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            Order order = TestFixture.Move(battle, Flight(battle, "us_fighter_flight"), new SimVec2(2000.0, 1000.0),
                OrderType.AIRDROP);
            battle.RunTicks(2);
            Assert.That(order.State, Is.EqualTo(OrderState.CANCELLED));
        }

        [Test]
        public void AircraftStayOutOfCollapseValueAndPointCapture()
        {
            using Battle battle = Build(("sup_1", "us_air_wing"));
            ClearRedAirDefense(battle);
            battle.RunTicks(2);
            double before = battle.Scoring.ForceValue("blue");
            double baseline = battle.Scoring.CommittedValue["blue"];
            Platoon fighters = Flight(battle, "us_fighter_flight");
            TestFixture.Move(battle, fighters, new SimVec2(2000.0, 2000.0));
            battle.RunSeconds(5.0);
            Assert.That(battle.Scoring.ForceValue("blue"), Is.EqualTo(before));
            Assert.That(battle.Scoring.CommittedValue["blue"], Is.EqualTo(baseline));
            TestFixture.Kill(fighters.Elements[0]);
            Assert.That(battle.Scoring.ForceValue("blue"), Is.EqualTo(before));
        }

        [Test]
        public void ArtilleryCompanyFiresArtilleryMissionsOnTheMap()
        {
            using Battle battle = Build(("sup_1", "us_artillery_co"));
            TaskForce blue = battle.TaskForces["blue"];
            battle.RunSeconds(2.0);
            int missions = blue.ArtilleryMissions;
            Platoon battery = blue.AllPlatoons().First(p => p.Kind == "artillery");
            int ammo = battery.Elements.Sum(e => e.Ammo["m795_155mm"]);

            Dictionary<string, object> result = battle.Fires.Request("blue", FireSupportSystem.Asset.ARTILLERY,
                new SimVec2(2000.0, 2800.0));
            Assert.That(result["ok"], Is.True);
            var mission = (FireSupportSystem.Mission)result["mission"];
            Assert.That(mission.ShooterUnitId, Is.EqualTo(battery.Id));
            Assert.That(blue.ArtilleryMissions, Is.EqualTo(missions), "off-map missions untouched");

            Dictionary<string, object> second = battle.Fires.Request("blue", FireSupportSystem.Asset.ARTILLERY,
                new SimVec2(1500.0, 2800.0));
            Assert.That(second["ok"], Is.True);
            Assert.That(((FireSupportSystem.Mission)second["mission"]).ShooterUnitId, Is.Not.EqualTo(battery.Id),
                "the second battery takes the second mission");

            battle.RunSeconds(240.0);
            Assert.That(mission.Phase, Is.EqualTo(FireSupportSystem.Phase.DONE));
            Assert.That(battery.Elements.Sum(e => e.Ammo["m795_155mm"]), Is.LessThan(ammo));
        }

        [Test]
        public void SupplyCompanySectionsLeaveTheCompanyToResupply()
        {
            using Battle battle = Build(spec => spec.Organic.RemoveAll(p => p.Template == "us_supply_section"),
                ("sup_1", "us_supply_co"));
            TaskForce blue = battle.TaskForces["blue"];
            Platoon tanks = blue.Companies.First(c => c.Kind == "tank").Platoons[0];
            foreach (Element tank in tanks.Elements)
            {
                tank.Ammo["m256_apfsds"] = 0;
            }

            Company supply = blue.Companies.Single(c => c.Kind == "supply");
            TestFixture.Move(battle, tanks, tanks.Centroid(), OrderType.RESUPPLY);
            battle.RunSeconds(240.0);
            Assert.That(supply.Platoons.Any(p => p.Detached), Is.True, "a section left the company");
            Assert.That(tanks.Elements.Sum(e => e.Ammo["m256_apfsds"]), Is.GreaterThan(0), "resupplied");
        }

        [Test]
        public void TransportHelicoptersCarrySquadsAndLoseThemWhenShotDown()
        {
            using Battle battle = Build(("sup_1", "us_heli_co"));
            Platoon assault = battle.TaskForces["blue"].AllPlatoons().Single(p => p.TemplateId == "us_air_assault_plt");
            Assert.That(assault.Mounted, Is.True);
            Assert.That(assault.Squads().All(s => s.IsMounted()), Is.True);
            Assert.That(OrderExecutor.PathMobility(assault), Is.EqualTo(Terrain.MobilityAir));

            battle.SetMounted(assault, false);
            Assert.That(assault.Squads().All(s => !s.IsMounted()), Is.True);
            Assert.That(OrderExecutor.PathMobility(assault), Is.EqualTo(Terrain.MobilityFoot),
                "dismounted squads walk; helicopters trail them");
            battle.SetMounted(assault, true);

            Element heli = assault.Vehicles().First(v => v.Passengers.Count > 0);
            List<Element> riders = new List<Element>(heli.Passengers);
            battle.Combat.Destroy(heli, null, battle.Tick());
            Assert.That(riders.All(s => s.State == ElementState.DESTROYED), Is.True);
        }

        [Test]
        public void TaskForceAiKeepsSupportCompaniesOffObjectives()
        {
            using Battle battle = Build(("sup_1", "us_supply_co"), ("sup_2", "us_air_wing"));
            battle.TfAi.Sides.Add("blue");
            battle.TfAi.Evaluate(battle.TaskForces["blue"]);
            foreach (Company company in battle.TaskForces["blue"].Companies)
            {
                Assert.That(battle.TfAi.Assignment.ContainsKey(company.Id), Is.EqualTo(!company.IsSupport),
                    company.DisplayName);
            }
        }

        private static Battle Build(params (string Slot, string Template)[] extra)
        {
            return Build(null, extra);
        }

        private static Battle Build(Action<ForceSpec>? edit, params (string Slot, string Template)[] extra)
        {
            ForceSpec blue = Catalog.DefaultForce("US")!.DuplicateSpec();
            edit?.Invoke(blue);
            foreach ((string slot, string template) in extra)
            {
                blue.Companies.Add(ForceSpec.MakeCompany(Catalog, slot, template));
            }

            Battle battle = BattleSetup.Skirmish(Catalog, blue, Catalog.DefaultForce("RU")!, "skirmish_01", 7,
                Array.Empty<string>());
            battle.BeginBattle();
            return battle;
        }

        // 비행 규칙만 보는 테스트에서는 적 기본 편성의 Tor(사거리 15 km, 지도 전체)를 치운다.
        private static void ClearRedAirDefense(Battle battle)
        {
            foreach (Element element in battle.SideElements("red"))
            {
                if (element.Def.ElementClass == "aa")
                {
                    TestFixture.Kill(element);
                }
            }
        }

        private static Platoon Flight(Battle battle, string templateId)
        {
            return battle.TaskForces["blue"].AllPlatoons().Single(p => p.TemplateId == templateId);
        }
    }
}
