using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class PlatoonAI : SimSystem
    {
        public const int UpdateInterval = 10;
        public const int InContactTicks = 150;
        public const int HaltTicks = 150;
        public const int AttackAdvanceTicks = 120;
        public const double WithdrawDistanceM = 600.0;
        public const double BreakContactDistanceM = 400.0;
        public const double RoutDistanceM = 800.0;
        public const double AssaultMaxM = 1200.0;
        public const int ShelledTicks = 30;
        public const double DisperseM = 250.0;
        public const int DisperseCooldown = 600;
        public const int ContactMemoryTicks = DetectionSystem.ContactMemoryTicks;

        private static readonly string[] SideNames = { "blue", "red" };
        private readonly IAiBattle _battle;

        public PlatoonAI(IAiBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public override string GetSystemName()
        {
            return "platoon_ai";
        }

        public override void Step(SimWorld world)
        {
            if (world.Tick % UpdateInterval != 0)
            {
                return;
            }

            foreach (string side in SideNames)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                foreach (Company company in taskForce.Companies)
                {
                    if (company.OnMap && !company.IsDestroyed() && company.Kind != "fixed_wing")
                    {
                        CompanyReaction(company, world.Tick);
                    }
                }

                foreach (Platoon platoon in taskForce.AllPlatoons())
                {
                    // 고정익 편대는 SOP 반응(정지·철수)을 하지 않는다. 명령과 연료로만 움직인다.
                    if (platoon.OnMap && !platoon.IsDestroyed() && !platoon.IsAirWing)
                    {
                        PlatoonReaction(platoon, world.Tick);
                    }
                }
            }
        }

        public static bool InContact(Unit unit, long tick)
        {
            return tick - unit.LastContactTick < InContactTicks || tick - unit.LastHitTick < InContactTicks;
        }

        private void CompanyReaction(Company company, long tick)
        {
            bool contact = false;
            foreach (Platoon platoon in company.FormationPlatoons())
            {
                if (InContact(platoon, tick))
                {
                    contact = true;
                    company.LastContactTick = Math.Max(company.LastContactTick, platoon.LastContactTick);
                    company.LastHitTick = Math.Max(company.LastHitTick, platoon.LastHitTick);
                }
            }

            if (contact && company.Posture == "moving" && company.Mission != null && company.Mission.Type == OrderType.ATTACK_MOVE)
            {
                AttackHalt(company, tick);
            }
            else if (contact && company.Posture == "moving")
            {
                switch (company.Sop.OnContact)
                {
                    case "halt_and_fire":
                        company.ContactHaltUntil = tick + HaltTicks;
                        break;
                    case "break_contact":
                        SelfOrder(company, OrderType.WITHDRAW, AwayFromContact(company, BreakContactDistanceM));
                        break;
                }
            }

            Platoon? shelled = null;
            foreach (Platoon platoon in company.FormationPlatoons())
            {
                if (tick - platoon.LastShelledTick < ShelledTicks)
                {
                    shelled = platoon;
                }
            }

            if (shelled != null)
            {
                Disperse(company, shelled.LastShellPos, tick);
            }

            if (company.Sop.WithdrawAt > 0.0 && company.Strength() <= company.Sop.WithdrawAt &&
                company.Posture != "withdrawing" && !company.AutoWithdrawn)
            {
                company.AutoWithdrawn = true;
                SelfOrder(company, OrderType.WITHDRAW, RearPoint(company, WithdrawDistanceM));
                _battle.World.Events.EmitEvent("auto_withdraw", tick, new Dictionary<string, object>
                {
                    ["unit"] = company.Id,
                    ["side"] = company.Side
                });
            }
        }

        private void PlatoonReaction(Platoon platoon, long tick)
        {
            bool contact = InContact(platoon, tick);
            // 수송헬기는 날고 있으니 접촉했다고 저절로 내려 주지 않는다. 하차는 명령으로만.
            if (platoon.HasSquads() && platoon.HasTransport() && platoon.Mounted && !platoon.HasAirTransport() &&
                (platoon.Sop.MountPolicy == "stay_dismounted" ||
                 contact && platoon.Sop.MountPolicy == "dismount_on_contact"))
            {
                _battle.SetMounted(platoon, false);
            }

            if (platoon.Routed)
            {
                if (!platoon.Guide.IsMoving() && !platoon.Guide.IsWaitingForPath())
                {
                    platoon.Detached = true;
                    SelfOrder(platoon, OrderType.WITHDRAW, RearPoint(platoon, RoutDistanceM));
                }

                return;
            }

            if (platoon.FollowsParent())
            {
                return;
            }

            if (tick - platoon.LastShelledTick < ShelledTicks)
            {
                Disperse(platoon, platoon.LastShellPos, tick);
            }

            if (contact && platoon.Posture == "moving" && platoon.Mission != null && platoon.Mission.Type == OrderType.ATTACK_MOVE)
            {
                AttackHalt(platoon, tick);
            }
            else if (contact && (platoon.Posture == "moving" || platoon.Posture == "idle"))
            {
                switch (platoon.Sop.OnContact)
                {
                    case "halt_and_fire":
                        if (platoon.Posture == "moving")
                        {
                            platoon.ContactHaltUntil = tick + HaltTicks;
                        }
                        break;
                    case "break_contact":
                        if (platoon.Posture != "withdrawing")
                        {
                            SelfOrder(platoon, OrderType.WITHDRAW, AwayFromContact(platoon, BreakContactDistanceM));
                        }
                        break;
                    case "assault":
                        SimVec2 target = NearestContact(platoon, tick);
                        if (target != SimVec2.Inf && platoon.Centroid().DistanceTo(target) < AssaultMaxM && platoon.Posture != "moving")
                        {
                            SelfOrder(platoon, OrderType.ATTACK_MOVE, target);
                        }
                        break;
                }
            }

            if (platoon.Sop.WithdrawAt > 0.0 && platoon.Strength() <= platoon.Sop.WithdrawAt &&
                platoon.Posture != "withdrawing" && !platoon.AutoWithdrawn && !platoon.IsHqSection)
            {
                platoon.AutoWithdrawn = true;
                SelfOrder(platoon, OrderType.WITHDRAW, RearPoint(platoon, WithdrawDistanceM));
                _battle.World.Events.EmitEvent("auto_withdraw", tick, new Dictionary<string, object>
                {
                    ["unit"] = platoon.Id,
                    ["side"] = platoon.Side
                });
            }
        }

        private void Disperse(Unit unit, SimVec2 shellPosition, long tick)
        {
            if (unit.Sop.UnderFire != "disperse" || tick < unit.NextDisperseTick)
            {
                return;
            }

            if (unit.Posture != "idle" && unit.Posture != "defending")
            {
                return;
            }

            if (unit.Guide.IsMoving() || unit.Guide.IsWaitingForPath())
            {
                return;
            }

            unit.NextDisperseTick = tick + DisperseCooldown;
            SimVec2 here = unit.Centroid();
            SimVec2 away = here - shellPosition;
            if (away.Length() < 5.0)
            {
                away = FormationSolver.RightVector(unit.Guide.Heading);
            }

            SimVec2 destination = here + away.Normalized() * DisperseM;
            double facing = unit.Guide.Heading;
            SimVec2 passable = _battle.Terrain.NearestPassable(destination, Terrain.MobilityVehicle);
            _battle.SelfOrder(unit, OrderType.MOVE, passable, new JObject { ["facing"] = facing });
            _battle.World.Events.EmitEvent("disperse", tick, new Dictionary<string, object>
            {
                ["unit"] = unit.Id,
                ["side"] = unit.Side
            });
        }

        private static void AttackHalt(Unit unit, long tick)
        {
            if (tick < unit.NextHaltTick)
            {
                return;
            }

            unit.ContactHaltUntil = tick + HaltTicks;
            unit.NextHaltTick = tick + HaltTicks + AttackAdvanceTicks;
        }

        private void SelfOrder(Unit unit, OrderType type, SimVec2 position)
        {
            _battle.SelfOrder(unit, type, position);
        }

        private SimVec2 RearPoint(Unit unit, double distance)
        {
            SimVec2 here = unit.Centroid();
            SimVec2 home = here;
            if (_battle.Scenario.Sides.TryGetValue(unit.Side, out ScenarioDef.SideSetup? setup))
            {
                home = setup.DeployZone.Center;
            }

            SimVec2 direction = home - here;
            if (direction.Length() < 1.0)
            {
                direction = unit.Side == "blue" ? new SimVec2(0, -1) : new SimVec2(0, 1);
            }

            return here + direction.Normalized() * Math.Min(distance, Math.Max(direction.Length(), 50.0));
        }

        private SimVec2 AwayFromContact(Unit unit, double distance)
        {
            SimVec2 here = unit.Centroid();
            SimVec2 threat = NearestContactAny(unit, _battle.World.Tick);
            if (threat == SimVec2.Inf)
            {
                return RearPoint(unit, distance);
            }

            SimVec2 direction = (here - threat).Normalized();
            return here + direction * distance;
        }

        private SimVec2 NearestContact(Platoon platoon, long tick)
        {
            SimVec2 best = SimVec2.Inf;
            double bestDistance = double.PositiveInfinity;
            SimVec2 centroid = platoon.Centroid();
            foreach (KeyValuePair<int, long> contact in platoon.Contacts)
            {
                if (tick - contact.Value > ContactMemoryTicks)
                {
                    continue;
                }

                Element? target = _battle.GetElement(contact.Key);
                if (target == null || !target.IsAlive())
                {
                    continue;
                }

                double distance = target.Pos.DistanceSquaredTo(centroid);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = target.Pos;
                }
            }

            return best;
        }

        private SimVec2 NearestContactAny(Unit unit, long tick)
        {
            if (unit is Platoon platoon)
            {
                return NearestContact(platoon, tick);
            }

            SimVec2 best = SimVec2.Inf;
            double bestDistance = double.PositiveInfinity;
            SimVec2 centroid = unit.Centroid();
            foreach (Unit child in unit.ChildrenUnits())
            {
                if (child is Platoon childPlatoon)
                {
                    SimVec2 position = NearestContact(childPlatoon, tick);
                    if (position != SimVec2.Inf)
                    {
                        double distance = position.DistanceSquaredTo(centroid);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = position;
                        }
                    }
                }
            }

            return best;
        }
    }
}
