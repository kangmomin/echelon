using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class OrderExecutor
    {
        public const double ShakenMorale = 40.0;

        private readonly ICommandBattle _battle;

        public OrderExecutor(ICommandBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public void Execute(Unit unit, Order order)
        {
            if (OrderSystem.IsAirWing(unit) && ExecuteAir(unit, order))
            {
                return;
            }

            if (order.Type == OrderType.AIRDROP)
            {
                Refuse(unit, order, "not_airlift");
                return;
            }

            if (order.Bypass && unit is Platoon && unit.FollowsParent())
            {
                unit.Detached = true;
            }

            if ((order.Type == OrderType.ATTACK_MOVE || order.Type == OrderType.SEIZE) && order.Issuer != "self" &&
                unit.Level != UnitLevel.TaskForce && (unit.Morale <= ShakenMorale || unit.IsCombatIneffective()))
            {
                order.State = OrderState.CANCELLED;
                _battle.World.Events.EmitEvent("order_refused", _battle.World.Tick, new Dictionary<string, object>
                {
                    ["unit"] = unit.Id,
                    ["side"] = unit.Side,
                    ["type"] = (int)order.Type
                });
                return;
            }

            if (order.IsMovement() && order.GetBool("append") && unit.Mission != null && !unit.Mission.IsFinished())
            {
                order.State = OrderState.RECEIVED;
                unit.QueuedOrders.Add(order);
                return;
            }

            if (order.IsEngineer() && !IsEngineer(unit))
            {
                order.State = OrderState.CANCELLED;
                _battle.World.Events.EmitEvent("order_refused", _battle.World.Tick, new Dictionary<string, object>
                {
                    ["unit"] = unit.Id,
                    ["side"] = unit.Side,
                    ["type"] = (int)order.Type,
                    ["reason"] = "not_engineer"
                });
                return;
            }

            order.State = OrderState.EXECUTING;
            switch (order.Type)
            {
                case OrderType.MOVE:
                case OrderType.FAST_MOVE:
                case OrderType.ATTACK_MOVE:
                case OrderType.SEIZE:
                case OrderType.DEFEND:
                case OrderType.WITHDRAW:
                case OrderType.LAY_MINES:
                case OrderType.CLEAR_MINES:
                case OrderType.FORTIFY:
                    StartMovement(unit, order);
                    break;
                case OrderType.HALT:
                    FinishMission(unit, OrderState.CANCELLED);
                    unit.Guide.ClearPath();
                    unit.Posture = "idle";
                    order.State = OrderState.DONE;
                    break;
                case OrderType.SET_FORMATION:
                    SetFormation(unit, order);
                    order.State = OrderState.DONE;
                    break;
                case OrderType.SET_SOP:
                    JObject changes = order.Params["sop"] as JObject ?? new JObject();
                    unit.Sop.Apply(changes);
                    if (order.GetBool("recursive", true))
                    {
                        foreach (Unit child in unit.ChildrenUnits())
                        {
                            child.Sop.Apply(changes);
                        }
                    }

                    order.State = OrderState.DONE;
                    break;
                case OrderType.DETACH:
                    unit.Detached = true;
                    order.State = OrderState.DONE;
                    break;
                case OrderType.REJOIN:
                    unit.Detached = false;
                    unit.AwaitingFollow = false;
                    FinishMission(unit, OrderState.CANCELLED);
                    unit.Guide.ClearPath();
                    unit.Posture = "idle";
                    order.State = OrderState.DONE;
                    break;
                case OrderType.MOUNT:
                case OrderType.DISMOUNT:
                    bool mount = order.Type == OrderType.MOUNT;
                    if (unit is Platoon platoon)
                    {
                        SetMounted(platoon, mount);
                    }

                    foreach (Unit child in unit.ChildrenUnits())
                    {
                        if (child is Platoon childPlatoon)
                        {
                            SetMounted(childPlatoon, mount);
                        }
                    }

                    order.State = OrderState.DONE;
                    break;
                case OrderType.CANCEL:
                    order.State = OrderState.DONE;
                    break;
                case OrderType.RESUPPLY:
                    FinishMission(unit, OrderState.CANCELLED);
                    if (unit is Platoon resupplyPlatoon)
                    {
                        _battle.Supply!.Request(resupplyPlatoon);
                    }

                    foreach (Unit child in unit.ChildrenUnits())
                    {
                        if (child is Platoon childPlatoon && !childPlatoon.IsHqSection)
                        {
                            _battle.Supply!.Request(childPlatoon);
                        }
                    }

                    order.State = OrderState.DONE;
                    break;
                case OrderType.FOLLOW:
                    unit.AwaitingFollow = false;
                    if (unit.Mission != null && unit.Mission.Issuer != "unit")
                    {
                        unit.Mission.State = OrderState.CANCELLED;
                    }

                    unit.Mission = null;
                    unit.Guide.ClearPath();
                    order.State = OrderState.DONE;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 고정익 명령. 중대에 내린 명령은 편대마다 그대로 내리고, 지도 밖 편대에 이동·공격 명령을 내리면 출격한다.
        /// 철수는 귀환이다. 처리했으면 true, 일반 처리(정지·SOP·대형)로 넘길 명령이면 false.
        /// </summary>
        private bool ExecuteAir(Unit unit, Order order)
        {
            if (unit is Company company)
            {
                order.State = OrderState.DONE;
                if (order.Issuer == "self")
                {
                    return true;
                }

                foreach (Platoon child in company.Platoons)
                {
                    if (!child.IsAirWing || child.IsDestroyed())
                    {
                        continue;
                    }

                    Order copy = Order.Make(order.Type, child.Id, (JObject)order.Params.DeepClone());
                    copy.Issuer = order.Issuer;
                    copy.IssuerUnitId = order.IssuerUnitId;
                    copy.ParentOrderId = order.Id;
                    _battle.RegisterOrder(copy);
                    copy.State = OrderState.RECEIVED;
                    copy.ReceivedTick = _battle.World.Tick;
                    Execute(child, copy);
                }

                return true;
            }

            if (!(unit is Platoon flight))
            {
                return false;
            }

            IOrderAir? air = _battle.Air;
            switch (order.Type)
            {
                case OrderType.MOVE:
                case OrderType.FAST_MOVE:
                case OrderType.ATTACK_MOVE:
                case OrderType.SEIZE:
                case OrderType.DEFEND:
                case OrderType.AIRDROP:
                    if (air == null)
                    {
                        Refuse(unit, order, "no_air_ops");
                        return true;
                    }

                    if (order.Type == OrderType.AIRDROP && !AirOpsSystem.CarriesSupply(flight))
                    {
                        Refuse(unit, order, "no_cargo");
                        return true;
                    }

                    SimVec2 target = _battle.Terrain.ClampToMap(order.HasPos() ? order.GetPos() : flight.Guide.Pos);
                    if (!flight.OnMap)
                    {
                        if (!air.Launch(flight, target))
                        {
                            Refuse(unit, order, "air_not_ready");
                            return true;
                        }
                    }
                    else if (air.IsReturning(flight))
                    {
                        Refuse(unit, order, "air_returning");
                        return true;
                    }

                    order.State = OrderState.EXECUTING;
                    flight.AirdropTarget = order.Type == OrderType.AIRDROP ? target : SimVec2.Inf;
                    StartMovement(flight, order);
                    return true;
                case OrderType.WITHDRAW:
                    FinishMission(flight, OrderState.CANCELLED);
                    air?.ReturnToBase(flight, "order");
                    order.State = OrderState.DONE;
                    return true;
                case OrderType.HALT:
                case OrderType.SET_SOP:
                case OrderType.SET_FORMATION:
                case OrderType.CANCEL:
                    return false;
                default:
                    Refuse(unit, order, "air_unit");
                    return true;
            }
        }

        private void Refuse(Unit unit, Order order, string reason)
        {
            order.State = OrderState.CANCELLED;
            _battle.World.Events.EmitEvent("order_refused", _battle.World.Tick, new Dictionary<string, object>
            {
                ["unit"] = unit.Id,
                ["side"] = unit.Side,
                ["type"] = (int)order.Type,
                ["reason"] = reason
            });
        }

        public void OnArrival(Unit unit)
        {
            Order? order = unit.Mission;
            if (order == null)
            {
                unit.Posture = "idle";
                return;
            }

            double facing = order.GetFacing();
            if (double.IsNaN(facing))
            {
                facing = unit.Guide.Heading;
            }

            if (order.IsEngineer())
            {
                unit.Guide.FinalFacing = facing;
                if (_battle.Engineers != null && unit is Platoon platoon && _battle.Engineers.BeginTask(platoon, order))
                {
                    return;
                }

                order.State = OrderState.CANCELLED;
                unit.Mission = null;
                unit.Posture = "idle";
                _battle.World.Events.EmitEvent("order_refused", _battle.World.Tick, new Dictionary<string, object>
                {
                    ["unit"] = unit.Id,
                    ["side"] = unit.Side,
                    ["type"] = (int)order.Type,
                    ["reason"] = "no_capacity"
                });
                return;
            }

            switch (order.Type)
            {
                case OrderType.SEIZE:
                case OrderType.DEFEND:
                    unit.Posture = "defending";
                    unit.DefendFacing = facing;
                    unit.Guide.FinalFacing = facing;
                    if (order.Type == OrderType.SEIZE && !order.Params.ContainsKey("formation"))
                    {
                        ApplyFormationId(unit, unit.Level == UnitLevel.Company ? "co_line" : "line");
                    }

                    break;
                case OrderType.WITHDRAW:
                    unit.Posture = "idle";
                    unit.Guide.FinalFacing = facing;
                    break;
                default:
                    unit.Posture = "idle";
                    unit.Guide.FinalFacing = order.GetFacing();
                    break;
            }

            order.State = OrderState.DONE;
            _battle.World.Events.EmitEvent("arrived", _battle.World.Tick, new Dictionary<string, object>
            {
                ["unit"] = unit.Id,
                ["side"] = unit.Side,
                ["type"] = (int)order.Type
            });
            StartNextQueued(unit);
        }

        public void SetMounted(Platoon platoon, bool mount)
        {
            if (!platoon.HasSquads() || !platoon.HasTransport() || platoon.Mounted == mount)
            {
                return;
            }

            platoon.Mounted = mount;
            if (mount)
            {
                foreach (Element squad in platoon.Squads())
                {
                    if (!squad.IsAlive())
                    {
                        continue;
                    }

                    Element? ride = FreeSeat(platoon, squad);
                    if (ride != null)
                    {
                        squad.MountedIn = ride;
                        ride.Passengers.Add(squad);
                        squad.Pos = ride.Pos;
                    }
                }
            }
            else
            {
                foreach (Element vehicle in platoon.Vehicles())
                {
                    foreach (Element squad in vehicle.Passengers)
                    {
                        squad.MountedIn = null;
                        SimVec2 dismountPosition = vehicle.Pos + FormationSolver.RightVector(vehicle.Heading) * 8.0;
                        squad.Pos = _battle.Terrain.NearestPassable(dismountPosition, Terrain.MobilityFoot);
                        squad.Heading = vehicle.Heading;
                    }

                    vehicle.Passengers.Clear();
                }
            }

            platoon.ReformUntilTick = _battle.World.Tick + 10 * SimConst.TickRate;
        }

        public static string PathMobility(Unit unit)
        {
            bool anyAir = false;
            foreach (Element element in unit.AliveElements())
            {
                if (element.IsAir())
                {
                    anyAir = true;
                }
                else if (element.IsVehicle())
                {
                    return Terrain.MobilityVehicle;
                }
            }

            if (anyAir)
            {
                // 수송헬기 소대가 보병을 내려놓았으면 소대는 보병 걸음으로 길을 찾는다(헬기는 뒤따른다).
                if (unit is Platoon platoon && platoon.HasSquads() && !platoon.Mounted)
                {
                    return Terrain.MobilityFoot;
                }

                return Terrain.MobilityAir;
            }

            foreach (Element element in unit.AliveElements())
            {
                if (element.IsVehicle())
                {
                    return Terrain.MobilityVehicle;
                }
            }

            return Terrain.MobilityFoot;
        }

        public static bool IsEngineer(Unit unit)
        {
            return unit is Platoon && unit.Kind == "engineer";
        }

        private void StartMovement(Unit unit, Order order)
        {
            if (unit.Mission != null && unit.Mission != order && !unit.Mission.IsFinished())
            {
                unit.Mission.State = OrderState.CANCELLED;
            }

            unit.Mission = order;
            SimVec2 target = order.HasPos() ? order.GetPos() : unit.Guide.Pos;
            target = _battle.Terrain.ClampToMap(target);
            unit.Guide.ClearPath();
            unit.SettlingSinceTick = -1;
            unit.Guide.FinalFacing = order.GetFacing();
            unit.DefendFacing = Unit.NoFacing;
            unit.Posture = order.Type == OrderType.WITHDRAW ? "withdrawing" : "moving";
            if (order.Params.ContainsKey("formation"))
            {
                ApplyFormationId(unit, order.GetString("formation"));
            }

            if (order.Params.ContainsKey("technique"))
            {
                unit.Sop.Apply(new JObject { ["technique"] = order.GetString("technique") });
            }

            if (unit is Company company)
            {
                BriefPlatoons(company, order);
            }

            if (unit.Guide.Pos.DistanceTo(target) < 5.0)
            {
                OnArrival(unit);
                return;
            }

            unit.Guide.Pending = _battle.Pathfinder.Request(unit.Guide.Pos, target, PathMobility(unit), unit.Side);
        }

        private void BriefPlatoons(Company company, Order order)
        {
            if (_battle.OrderSystem == null)
            {
                return;
            }

            List<Platoon> targets = company.FormationPlatoons();
            if (company.Hq != null && company.Hq.OnMap)
            {
                targets.Add(company.Hq);
            }

            foreach (Platoon platoon in targets)
            {
                if (platoon == company.Hq && company.Leader() != null && company.Leader()!.Platoon == platoon)
                {
                    continue;
                }

                platoon.AwaitingFollow = true;
                Order subOrder = Order.Make(OrderType.FOLLOW, platoon.Id);
                subOrder.ParentOrderId = order.Id;
                _battle.OrderSystem.SubmitSubOrder(subOrder, company);
            }
        }

        private void StartNextQueued(Unit unit)
        {
            if (unit.QueuedOrders.Count == 0)
            {
                return;
            }

            Order next = unit.QueuedOrders[0];
            unit.QueuedOrders.RemoveAt(0);
            Execute(unit, next);
        }

        private static void FinishMission(Unit unit, OrderState state)
        {
            if (unit.Mission != null && !unit.Mission.IsFinished())
            {
                unit.Mission.State = state;
            }

            unit.Mission = null;
            unit.QueuedOrders.Clear();
        }

        private void SetFormation(Unit unit, Order order)
        {
            ApplyFormationId(unit, order.GetString("formation"));
            if (order.Params.ContainsKey("child_formation"))
            {
                string childFormation = order.GetString("child_formation");
                foreach (Unit child in unit.ChildrenUnits())
                {
                    if (child is Platoon platoon && !platoon.IsHqSection)
                    {
                        ApplyFormationId(platoon, childFormation);
                    }
                }
            }
        }

        private bool ApplyFormationId(Unit unit, string formationId)
        {
            FormationDef? formation = _battle.Catalog.GetFormation(formationId);
            if (formation == null)
            {
                return false;
            }

            string wantedEchelon = unit.Level == UnitLevel.Company ? "company" : "platoon";
            if (formation.Echelon != wantedEchelon || formation == unit.Formation)
            {
                return false;
            }

            unit.Formation = formation;
            double seconds = formation.GetMod("transition_sec", 10.0) * formation.GetMod("control", 1.0) *
                unit.SkillDelayFactor();
            unit.ReformUntilTick = _battle.World.Tick + OrderSystem.SecondsToTicks(seconds);
            return true;
        }

        private static Element? FreeSeat(Platoon platoon, Element squad)
        {
            Element? best = null;
            double bestDistance = double.PositiveInfinity;
            foreach (Element vehicle in platoon.Vehicles())
            {
                if (vehicle.IsAlive() && vehicle.Passengers.Count < vehicle.Def.TransportCapacity)
                {
                    double distance = vehicle.Pos.DistanceSquaredTo(squad.Pos);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = vehicle;
                    }
                }
            }

            return best;
        }
    }
}
