using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class EngineerSystem : SimSystem, ICombatEngineers
    {
        public const int StepInterval = 5;
        public const double MinefieldRadiusM = 70.0;
        public const int MinefieldCharges = 8;
        public const int MinefieldsPerPlatoon = 2;
        public const double LaySec = 45.0;
        public const double ClearSecPerCharge = 6.0;
        public const double ClearReachM = 150.0;
        public const double DetectRangeM = 150.0;
        public const double FortifySec = 60.0;
        public const double PositionRadiusM = 90.0;
        public const int PositionsPerPlatoon = 3;
        public const int PositionSettleTicks = 30;
        public const double PositionFactorValue = 0.6;
        public const double StrikePVehicle = 0.12;
        public const double StrikePFoot = 0.05;
        public const double MineInfCasualties = 2.0;
        public const double ThreatAmount = 6.0;
        public const int WorkBlockedByHitTicks = 100;

        public static readonly string[] Sides = { "blue", "red" };

        public sealed class Minefield
        {
            public int Id { get; set; }
            public SimVec2 Pos { get; set; }
            public double Radius { get; set; } = MinefieldRadiusM;
            public string Side { get; set; } = "";
            public int Charges { get; set; } = MinefieldCharges;
            public Dictionary<string, bool> Known { get; } = new Dictionary<string, bool>();
        }

        public sealed class FieldPosition
        {
            public SimVec2 Pos { get; set; }
            public double Radius { get; set; } = PositionRadiusM;
            public string Side { get; set; } = "";
        }

        private readonly IEngineerBattle _battle;
        private int _nextId = 1;

        public EngineerSystem(IEngineerBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public List<Minefield> Minefields { get; } = new List<Minefield>();
        public List<FieldPosition> Positions { get; } = new List<FieldPosition>();
        public int MineStrikes { get; private set; }

        public override string GetSystemName()
        {
            return "engineers";
        }

        public override bool OmitFromHash()
        {
            return Minefields.Count == 0 && Positions.Count == 0 && MineStrikes == 0;
        }

        public override long StateHash()
        {
            long hash = SimHash.Append(SimHash.Initial, MineStrikes);
            foreach (Minefield minefield in Minefields)
            {
                hash = SimHash.Append(hash, minefield.Id);
                hash = SimHash.Append(hash, minefield.Charges);
                hash = SimHash.Append(hash, BitConverter.SingleToInt32Bits(minefield.Pos.X));
                hash = SimHash.Append(hash, BitConverter.SingleToInt32Bits(minefield.Pos.Y));
            }

            foreach (FieldPosition position in Positions)
            {
                hash = SimHash.Append(hash, BitConverter.SingleToInt32Bits(position.Pos.X));
                hash = SimHash.Append(hash, BitConverter.SingleToInt32Bits(position.Pos.Y));
            }

            return hash;
        }

        public static bool IsEngineer(Unit unit)
        {
            return unit is Platoon && unit.Kind == "engineer";
        }

        public override void Step(SimWorld world)
        {
            if (world.Tick % StepInterval != 0)
            {
                return;
            }

            double deltaSeconds = StepInterval * SimConst.TickDt;
            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                foreach (Platoon platoon in taskForce.AllPlatoons())
                {
                    if (platoon.Kind == "engineer" && platoon.OnMap && !platoon.IsDestroyed())
                    {
                        Work(platoon, world.Tick, deltaSeconds);
                    }
                }
            }

            if (Minefields.Count != 0)
            {
                Strikes(world.Tick);
                EngineerDetection();
            }
        }

        public bool BeginTask(Platoon platoon, Order order)
        {
            switch (order.Type)
            {
                case OrderType.LAY_MINES:
                    if (platoon.MinesLeft <= 0)
                    {
                        return false;
                    }

                    platoon.EngTask = "lay";
                    platoon.Posture = "laying";
                    break;
                case OrderType.CLEAR_MINES:
                    platoon.EngTask = "clear";
                    platoon.Posture = "clearing";
                    break;
                case OrderType.FORTIFY:
                    if (platoon.PositionsBuilt >= PositionsPerPlatoon)
                    {
                        return false;
                    }

                    platoon.EngTask = "fortify";
                    platoon.Posture = "fortifying";
                    break;
                default:
                    return false;
            }

            platoon.EngProgress = 0.0;
            return true;
        }

        public Minefield LayMinefield(string side, SimVec2 position)
        {
            Minefield minefield = new Minefield
            {
                Id = _nextId++,
                Pos = position,
                Side = side
            };
            minefield.Known[side] = true;
            Minefields.Add(minefield);
            return minefield;
        }

        public double PositionFactor(Element element)
        {
            if (Positions.Count == 0 || element.IsAir() || element.StationaryTicks < PositionSettleTicks)
            {
                return 1.0;
            }

            foreach (FieldPosition position in Positions)
            {
                if (position.Side == element.Side &&
                    position.Pos.DistanceSquaredTo(element.Pos) <= position.Radius * position.Radius)
                {
                    return PositionFactorValue;
                }
            }

            return 1.0;
        }

        public List<Minefield> KnownMinefields(string side)
        {
            List<Minefield> output = new List<Minefield>();
            foreach (Minefield minefield in Minefields)
            {
                if (minefield.Known.TryGetValue(side, out bool known) && known)
                {
                    output.Add(minefield);
                }
            }

            return output;
        }

        private void Work(Platoon platoon, long tick, double deltaSeconds)
        {
            if (platoon.EngTask == "")
            {
                return;
            }

            if (platoon.Mission == null || platoon.Mission.IsFinished() || !platoon.Mission.IsEngineer())
            {
                platoon.EngTask = "";
                platoon.EngProgress = 0.0;
                return;
            }

            if (Sappers(platoon) == 0 || tick - platoon.LastHitTick < WorkBlockedByHitTicks)
            {
                return;
            }

            platoon.EngProgress += deltaSeconds;
            switch (platoon.EngTask)
            {
                case "lay":
                    if (platoon.EngProgress >= LaySec)
                    {
                        LayMinefield(platoon.Side, platoon.Guide.Pos);
                        --platoon.MinesLeft;
                        Finish(platoon, "minefield_laid", new Dictionary<string, object> { ["pos"] = platoon.Guide.Pos });
                    }

                    break;
                case "fortify":
                    if (platoon.EngProgress >= FortifySec)
                    {
                        FieldPosition position = new FieldPosition { Pos = platoon.Guide.Pos, Side = platoon.Side };
                        Positions.Add(position);
                        ++platoon.PositionsBuilt;
                        Finish(platoon, "position_built", new Dictionary<string, object> { ["pos"] = platoon.Guide.Pos });
                    }

                    break;
                case "clear":
                    Minefield? minefield = NearestMinefield(platoon.Guide.Pos, ClearReachM);
                    if (minefield == null)
                    {
                        Finish(platoon, "minefield_cleared", new Dictionary<string, object>
                        {
                            ["pos"] = platoon.Guide.Pos,
                            ["none"] = true
                        });
                        return;
                    }

                    minefield.Known[platoon.Side] = true;
                    double perCharge = ClearSecPerCharge * (HasBreacher(platoon) ? 0.5 : 1.0);
                    while (platoon.EngProgress >= perCharge && minefield.Charges > 0)
                    {
                        platoon.EngProgress -= perCharge;
                        --minefield.Charges;
                    }

                    if (minefield.Charges <= 0)
                    {
                        Minefields.Remove(minefield);
                        Finish(platoon, "minefield_cleared", new Dictionary<string, object> { ["pos"] = minefield.Pos });
                    }

                    break;
            }
        }

        private void Finish(Platoon platoon, string eventType, Dictionary<string, object> payload)
        {
            platoon.EngTask = "";
            platoon.EngProgress = 0.0;
            if (platoon.Mission != null)
            {
                platoon.Mission.State = OrderState.DONE;
            }

            platoon.Mission = null;
            platoon.Posture = "idle";
            payload["unit"] = platoon.Id;
            payload["side"] = platoon.Side;
            _battle.World.Events.EmitEvent(eventType, _battle.World.Tick, payload);
        }

        private static int Sappers(Platoon platoon)
        {
            int count = 0;
            foreach (Element element in platoon.Elements)
            {
                if (element.IsInfantry() && element.IsAlive())
                {
                    ++count;
                }
            }

            return count;
        }

        private static bool HasBreacher(Platoon platoon)
        {
            foreach (Element element in platoon.Elements)
            {
                if (element.Def.ElementClass == "eng" && element.IsAlive())
                {
                    return true;
                }
            }

            return false;
        }

        private Minefield? NearestMinefield(SimVec2 position, double reach)
        {
            Minefield? best = null;
            double bestDistance = double.PositiveInfinity;
            foreach (Minefield minefield in Minefields)
            {
                double distance = minefield.Pos.DistanceTo(position) - minefield.Radius;
                if (distance <= reach && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = minefield;
                }
            }

            return best;
        }

        private void Strikes(long tick)
        {
            SimRng rng = _battle.World.Rng;
            List<Minefield> fields = new List<Minefield>(Minefields);
            foreach (Minefield minefield in fields)
            {
                if (minefield.Charges <= 0)
                {
                    Minefields.Remove(minefield);
                    continue;
                }

                foreach (Element element in _battle.ElementsNear(minefield.Pos, minefield.Radius))
                {
                    if (element.Side == minefield.Side || element.IsMounted() || element.IsAir() || !element.IsAlive())
                    {
                        continue;
                    }

                    if (element.Velocity.Length() < 0.5)
                    {
                        continue;
                    }

                    double probability = element.IsInfantry() ? StrikePFoot : StrikePVehicle;
                    if (minefield.Known.TryGetValue(element.Side, out bool known) && known)
                    {
                        probability *= 0.5;
                    }

                    if (!rng.Chance(probability))
                    {
                        continue;
                    }

                    --minefield.Charges;
                    ++MineStrikes;
                    Reveal(minefield, element.Side);
                    _battle.World.Events.EmitEvent("mine_strike", tick, new Dictionary<string, object>
                    {
                        ["pos"] = element.Pos,
                        ["element"] = element.Id,
                        ["side"] = element.Side,
                        ["unit"] = element.Platoon?.Id ?? -1
                    });
                    if (element.IsInfantry())
                    {
                        int men = (int)SimMath.Round(MineInfCasualties);
                        element.Hp = Math.Max(0.0, element.Hp - men);
                        if (element.Platoon != null)
                        {
                            _battle.OnCasualties(element.Platoon, men);
                        }

                        if (element.Hp <= 0.0)
                        {
                            _battle.Destroy(element, null, tick);
                        }
                    }
                    else if (rng.Chance(0.5))
                    {
                        _battle.Destroy(element, null, tick);
                    }
                    else
                    {
                        element.State = ElementState.DAMAGED;
                        element.DamageKind = "mobility";
                        _battle.OnElementDamaged(element);
                        _battle.World.Events.EmitEvent("element_damaged", tick, new Dictionary<string, object>
                        {
                            ["element"] = element.Id,
                            ["side"] = element.Side,
                            ["kind"] = element.DamageKind,
                            ["unit"] = element.Platoon?.Id ?? -1
                        });
                    }

                    if (minefield.Charges <= 0)
                    {
                        break;
                    }
                }
            }
        }

        private void Reveal(Minefield minefield, string side)
        {
            if (minefield.Known.TryGetValue(side, out bool known) && known)
            {
                return;
            }

            minefield.Known[side] = true;
            _battle.AddThreat(side, minefield.Pos, minefield.Radius + 20.0, ThreatAmount);
            _battle.World.Events.EmitEvent("minefield_found", _battle.World.Tick, new Dictionary<string, object>
            {
                ["pos"] = minefield.Pos,
                ["side"] = side
            });
        }

        private void EngineerDetection()
        {
            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                foreach (Platoon platoon in taskForce.AllPlatoons())
                {
                    if (platoon.Kind != "engineer" || !platoon.OnMap || platoon.IsDestroyed())
                    {
                        continue;
                    }

                    foreach (Minefield minefield in Minefields)
                    {
                        bool known = minefield.Known.TryGetValue(side, out bool value) && value;
                        if (minefield.Side != side && !known &&
                            minefield.Pos.DistanceTo(platoon.Guide.Pos) < minefield.Radius + DetectRangeM)
                        {
                            Reveal(minefield, side);
                        }
                    }
                }
            }
        }
    }
}
