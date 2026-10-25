using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    /// <summary>
    /// 고정익 편대의 출격·연료·귀환·재무장과 보급 투하(D-050). 편대는 지도 밖에서 기다리다가 명령을 받으면
    /// 아군 쪽 지도 가장자리에서 들어온다. 연료가 떨어지면 들어온 가장자리로 돌아가 지도에서 빠지고,
    /// 재무장 시간이 지나면 다시 출격할 수 있다.
    /// </summary>
    public sealed class AirOpsSystem : SimSystem, IOrderAir
    {
        public const double EdgeInsetM = 20.0;
        public const double EntryMarginM = 200.0;
        public const double EntrySpacingM = 80.0;
        public const double ExitReachM = 150.0;
        public const double AirdropReachM = 200.0;
        public const double AirdropRadiusM = 300.0;

        private readonly Battle _battle;

        public AirOpsSystem(Battle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public override string GetSystemName()
        {
            return "air_ops";
        }

        public override bool OmitFromHash()
        {
            foreach (Element element in _battle.Elements)
            {
                if (element.IsFixedWing())
                {
                    return false;
                }
            }

            return true;
        }

        public override long StateHash()
        {
            long hash = SimHash.Initial;
            foreach (Element element in _battle.Elements)
            {
                if (!element.IsFixedWing())
                {
                    continue;
                }

                hash = SimHash.Append(hash, (int)element.Air);
                hash = SimHash.Append(hash, element.AirReadyTick);
                hash = SimHash.Append(hash, (long)SimMath.Round(element.FuelSec * 10.0));
            }

            return hash;
        }

        public override void Step(SimWorld world)
        {
            long tick = world.Tick;
            foreach (string side in Battle.Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                foreach (Platoon flight in taskForce.AllPlatoons())
                {
                    if (flight.IsAirWing)
                    {
                        StepFlight(flight, tick);
                    }
                }
            }
        }

        public bool Launch(Platoon flight, SimVec2 toward)
        {
            List<Element> ready = new List<Element>();
            foreach (Element element in flight.Elements)
            {
                if (element.IsAlive() && !element.OnMap && element.Air == AirState.READY)
                {
                    ready.Add(element);
                }
            }

            if (ready.Count == 0)
            {
                return false;
            }

            TaskForce? taskForce = flight.TaskForce();
            SimVec2 size = _battle.Terrain.SizeM;
            double edgeY = FriendlyEdgeY(taskForce);
            double x = SimMath.Clamp(toward.X, EntryMarginM, size.X - EntryMarginM);
            SimVec2 entry = new SimVec2(x, edgeY);
            double heading = FormationSolver.HeadingFromVector(toward - entry);
            for (int i = 0; i < ready.Count; ++i)
            {
                Element plane = ready[i];
                double offset = (i - (ready.Count - 1) * 0.5) * EntrySpacingM;
                SimVec2 pos = _battle.Terrain.ClampToMap(entry + new SimVec2(offset, 0.0), EdgeInsetM);
                plane.OnMap = true;
                plane.Air = AirState.AIRBORNE;
                plane.FuelSec = plane.Def.EnduranceSec;
                plane.Pos = pos;
                plane.PrevPos = pos;
                plane.Heading = heading;
                plane.PrevHeading = heading;
                plane.Velocity = SimVec2.Zero;
                plane.HasMoveTarget = false;
                plane.TargetId = -1;
                plane.ClearLocalPath();
            }

            flight.OnMap = true;
            flight.Guide.Reset(entry, heading);
            flight.Posture = "idle";
            flight.LastAssignKey = (float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            flight.AirdropTarget = SimVec2.Inf;
            _battle.World.Events.EmitEvent("air_launched", _battle.World.Tick, new Dictionary<string, object>
            {
                ["unit"] = flight.Id,
                ["side"] = flight.Side,
                ["pos"] = entry
            });
            return true;
        }

        public void ReturnToBase(Platoon flight, string reason)
        {
            TaskForce? taskForce = flight.TaskForce();
            double edgeY = FriendlyEdgeY(taskForce);
            bool any = false;
            foreach (Element plane in flight.Elements)
            {
                if (!plane.IsAlive() || !plane.OnMap || plane.Air != AirState.AIRBORNE)
                {
                    continue;
                }

                plane.Air = AirState.RETURNING;
                plane.AirExit = _battle.Terrain.ClampToMap(new SimVec2(plane.Pos.X, edgeY), EdgeInsetM);
                any = true;
            }

            flight.AirdropTarget = SimVec2.Inf;
            if (!any)
            {
                return;
            }

            if (flight.Mission != null && !flight.Mission.IsFinished())
            {
                flight.Mission.State = OrderState.CANCELLED;
            }

            flight.Mission = null;
            flight.QueuedOrders.Clear();
            flight.Guide.ClearPath();
            _battle.World.Events.EmitEvent("air_returning", _battle.World.Tick, new Dictionary<string, object>
            {
                ["unit"] = flight.Id,
                ["side"] = flight.Side,
                ["reason"] = reason
            });
        }

        public bool IsReturning(Platoon flight)
        {
            foreach (Element plane in flight.Elements)
            {
                if (plane.IsAlive() && plane.OnMap && plane.Air == AirState.AIRBORNE)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool CarriesSupply(Platoon flight)
        {
            foreach (Element plane in flight.Elements)
            {
                if (plane.IsAlive() && plane.Def.SupplyCapacity > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void StepFlight(Platoon flight, long tick)
        {
            bool outOfFuel = false;
            bool becameReady = false;
            bool anyAirborne = false;
            foreach (Element plane in flight.Elements)
            {
                if (!plane.IsAlive())
                {
                    continue;
                }

                switch (plane.Air)
                {
                    case AirState.REARMING:
                        if (tick >= plane.AirReadyTick)
                        {
                            Rearm(plane);
                            becameReady = true;
                        }

                        break;
                    case AirState.AIRBORNE:
                        plane.FuelSec = Math.Max(0.0, plane.FuelSec - SimConst.TickDt);
                        if (plane.FuelSec <= 0.0)
                        {
                            outOfFuel = true;
                        }

                        break;
                    case AirState.RETURNING:
                        if (plane.Pos.DistanceTo(plane.AirExit) < ExitReachM)
                        {
                            Exit(plane, tick);
                        }

                        break;
                }

                if (plane.OnMap && (plane.Air == AirState.AIRBORNE || plane.Air == AirState.RETURNING))
                {
                    anyAirborne = true;
                }
            }

            if (becameReady)
            {
                _battle.World.Events.EmitEvent("air_ready", tick, new Dictionary<string, object>
                {
                    ["unit"] = flight.Id,
                    ["side"] = flight.Side
                });
            }

            if (outOfFuel)
            {
                ReturnToBase(flight, "fuel");
            }

            if (flight.AirdropTarget != SimVec2.Inf)
            {
                StepAirdrop(flight, tick);
            }

            if (flight.OnMap && !anyAirborne)
            {
                flight.OnMap = false;
                flight.Mission = null;
                flight.Guide.ClearPath();
                flight.Posture = "idle";
            }
        }

        private void StepAirdrop(Platoon flight, long tick)
        {
            bool cargoLeft = false;
            foreach (Element plane in flight.Elements)
            {
                if (!plane.IsAlive() || !plane.OnMap || plane.Air != AirState.AIRBORNE || plane.SupplyLeft <= 0.0)
                {
                    continue;
                }

                if (plane.Pos.DistanceTo(flight.AirdropTarget) > AirdropReachM)
                {
                    cargoLeft = true;
                    continue;
                }

                Drop(plane, flight.AirdropTarget, tick);
            }

            if (!cargoLeft)
            {
                ReturnToBase(flight, "airdrop");
            }
        }

        /// <summary>
        /// 투하 지점 반경 안 아군에게 탄약을 나눠 준다. 낙하산 묶음이라 받을 사람이 없으면 남은 양은 버려진다.
        /// </summary>
        private void Drop(Element plane, SimVec2 target, long tick)
        {
            double budget = plane.SupplyLeft;
            double used = 0.0;
            foreach (Element element in _battle.Elements)
            {
                if (budget - used <= 0.0)
                {
                    break;
                }

                if (element.Side != plane.Side || !element.IsActive() || element.IsAir() ||
                    element.Pos.DistanceTo(target) > AirdropRadiusM)
                {
                    continue;
                }

                if (element.IsMounted())
                {
                    continue;
                }

                used += SupplySystem.Give(element, budget - used);
            }

            plane.SupplyLeft = 0.0;
            _battle.World.Events.EmitEvent("airdrop", tick, new Dictionary<string, object>
            {
                ["unit"] = plane.Platoon != null ? plane.Platoon.Id : -1,
                ["side"] = plane.Side,
                ["pos"] = target,
                ["amount"] = used
            });
        }

        private void Rearm(Element plane)
        {
            plane.Air = AirState.READY;
            plane.FuelSec = plane.Def.EnduranceSec;
            plane.SupplyLeft = plane.Def.SupplyCapacity;
            foreach (KeyValuePair<string, int> pair in plane.Def.Ammo)
            {
                plane.Ammo[pair.Key] = pair.Value;
            }
        }

        private void Exit(Element plane, long tick)
        {
            plane.OnMap = false;
            plane.Air = AirState.REARMING;
            plane.AirReadyTick = tick + OrderSystem.SecondsToTicks(plane.Def.RearmSec);
            plane.Velocity = SimVec2.Zero;
            plane.HasMoveTarget = false;
            plane.TargetId = -1;
            plane.ClearLocalPath();
        }

        private double FriendlyEdgeY(TaskForce? taskForce)
        {
            double height = _battle.Terrain.SizeM.Y;
            if (taskForce == null)
            {
                return EdgeInsetM;
            }

            return taskForce.EntryPos.Y < height * 0.5 ? EdgeInsetM : height - EdgeInsetM;
        }
    }
}
