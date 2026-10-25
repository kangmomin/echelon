using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class TaskForceAI : SimSystem
    {
        public const int EvalInterval = 300;
        public const int EvalOffset = 7;
        public const double ReserveStrength = 0.5;
        public const double StickyBonus = 1.3;
        public const double EnemyNearM = 900.0;
        public const double ArriveM = 250.0;
        public const int FreshTicks = 300;
        public const double ClusterM = 150.0;
        public const int ArtilleryGapTicks = 1200;
        public const double LowAmmo = 0.35;

        private static readonly Dictionary<string, double> ClassValues = new Dictionary<string, double>
        {
            ["tank"] = 60.0,
            ["ifv"] = 30.0,
            ["recon"] = 30.0,
            ["inf_squad"] = 10.0,
            ["apc"] = 20.0,
            ["mortar"] = 15.0,
            ["cp"] = 20.0,
            ["fo"] = 15.0,
            ["aa"] = 25.0,
            ["ew"] = 15.0,
            ["truck"] = 5.0,
            ["heli"] = 70.0,
            ["at"] = 25.0,
            ["eng"] = 20.0
        };

        private readonly IAiBattle _battle;
        private readonly Dictionary<string, long> _lastArtillery = new Dictionary<string, long>
        {
            ["blue"] = -100000,
            ["red"] = -100000
        };
        private readonly Dictionary<string, long> _lastCas = new Dictionary<string, long>
        {
            ["blue"] = -100000,
            ["red"] = -100000
        };

        public TaskForceAI(IAiBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public List<string> Sides { get; } = new List<string>();
        public Dictionary<int, string> Assignment { get; } = new Dictionary<int, string>();
        public Dictionary<int, OrderType> LastOrder { get; } = new Dictionary<int, OrderType>();

        public override string GetSystemName()
        {
            return "tf_ai";
        }

        public override long StateHash()
        {
            long hash = SimHash.Initial;
            List<int> ids = new List<int>(Assignment.Keys);
            ids.Sort();
            foreach (int id in ids)
            {
                hash = SimHash.Append(hash, id);
                hash = AppendString(hash, Assignment[id]);
            }

            return hash;
        }

        public override void Step(SimWorld world)
        {
            if (_battle.Deploying || _battle.HasResult || world.Tick % EvalInterval != EvalOffset)
            {
                return;
            }

            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce != null && taskForce.Leader() != null)
                {
                    Evaluate(taskForce);
                }
            }
        }

        public void Evaluate(TaskForce taskForce)
        {
            if (taskForce == null)
            {
                throw new ArgumentNullException(nameof(taskForce));
            }

            AssignCompanies(taskForce);
            Organic(taskForce);
            Fires(taskForce);
            Resupply(taskForce);
        }

        public static double CompanyValue(Company company)
        {
            double value = 0.0;
            foreach (Element element in company.AliveElements())
            {
                value += ClassValue(element.Def.ElementClass) * element.Strength();
            }

            return value;
        }

        private static double ClassValue(string elementClass)
        {
            return ClassValues.TryGetValue(elementClass, out double value) ? value : 20.0;
        }

        private double PointValue(TaskForce taskForce, ScoringSystem.PointState point)
        {
            double value = point.Def.VpPerMin * 10.0;
            foreach (ScenarioDef.Objective objective in _battle.Scenario.Objectives)
            {
                if (objective.Point == point.Def.Id && objective.Side == taskForce.Side &&
                    !_battle.Scoring.ObjectivesDone.ContainsKey(objective.Id))
                {
                    value += objective.Vp;
                }
            }

            if (point.Owner == taskForce.Side && !point.Contested)
            {
                value *= 0.6;
            }

            return value;
        }

        private int EnemiesNear(TaskForce taskForce, SimVec2 position, double radius)
        {
            int count = 0;
            long tick = _battle.World.Tick;
            foreach (Dictionary<string, object> info in taskForce.KnownEnemies.Values)
            {
                long seenTick = ReadInt64(info, "tick");
                SimVec2 seenPosition = ReadPosition(info);
                if (tick - seenTick <= FreshTicks * 4 && seenPosition.DistanceTo(position) <= radius)
                {
                    ++count;
                }
            }

            return count;
        }

        private double EnemyValueNear(TaskForce taskForce, SimVec2 position, double radius)
        {
            double value = 0.0;
            long tick = _battle.World.Tick;
            foreach (Dictionary<string, object> info in taskForce.KnownEnemies.Values)
            {
                long seenTick = ReadInt64(info, "tick");
                SimVec2 seenPosition = ReadPosition(info);
                if (tick - seenTick <= FreshTicks * 4 && seenPosition.DistanceTo(position) <= radius)
                {
                    string elementClass = ReadString(info, "class");
                    value += ClassValue(elementClass);
                }
            }

            return value;
        }

        private void AssignCompanies(TaskForce taskForce)
        {
            List<Company> active = new List<Company>();
            foreach (Company company in taskForce.Companies)
            {
                if (!company.OnMap || company.IsDestroyed() || company.IsSupport)
                {
                    continue;
                }

                if (company.Strength() <= ReserveStrength || company.Routed)
                {
                    if (!Assignment.TryGetValue(company.Id, out string assigned) || assigned != "reserve")
                    {
                        Assignment[company.Id] = "reserve";
                        Issue(company, OrderType.WITHDRAW, Rear(taskForce), Unit.NoFacing);
                    }

                    continue;
                }

                active.Add(company);
            }

            if (active.Count == 0)
            {
                return;
            }

            List<ScoringSystem.PointState> points = _battle.Scoring.Points;
            Dictionary<string, int> pointLoad = new Dictionary<string, int>();
            List<Company> todo = new List<Company>(active);
            Dictionary<int, string> newAssignments = new Dictionary<int, string>();
            while (todo.Count > 0)
            {
                double bestScore = -1.0;
                Company? bestCompany = null;
                ScoringSystem.PointState? bestPoint = null;
                foreach (Company company in todo)
                {
                    foreach (ScoringSystem.PointState point in points)
                    {
                        int already = pointLoad.TryGetValue(point.Def.Id, out int load) ? load : 0;
                        double enemyValue = EnemyValueNear(taskForce, point.Def.Pos, EnemyNearM);
                        double distanceKm = company.Centroid().DistanceTo(point.Def.Pos) / 1000.0;
                        double ratio = enemyValue / Math.Max(CompanyValue(company), 1.0);
                        double score = PointValue(taskForce, point) / (1.0 + distanceKm) /
                            (1.0 + 3.0 * Math.Max(0.0, ratio - 0.7));
                        score /= 1.0 + already * 1.5;
                        if (Assignment.TryGetValue(company.Id, out string previous) && previous == point.Def.Id)
                        {
                            score *= StickyBonus;
                        }

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestCompany = company;
                            bestPoint = point;
                        }
                    }
                }

                if (bestCompany == null || bestPoint == null)
                {
                    break;
                }

                todo.Remove(bestCompany);
                newAssignments[bestCompany.Id] = bestPoint.Def.Id;
                pointLoad[bestPoint.Def.Id] = (pointLoad.TryGetValue(bestPoint.Def.Id, out int assigned) ? assigned : 0) + 1;
            }

            foreach (Company company in active)
            {
                if (!newAssignments.TryGetValue(company.Id, out string? pointId) || string.IsNullOrEmpty(pointId))
                {
                    continue;
                }

                ScoringSystem.PointState? point = _battle.Scoring.GetPoint(pointId);
                if (point == null)
                {
                    continue;
                }

                bool changed = !Assignment.TryGetValue(company.Id, out string oldPoint) || oldPoint != pointId;
                Assignment[company.Id] = pointId;
                bool held = point.Owner == taskForce.Side && !point.Contested;
                bool enemyNear = EnemiesNear(taskForce, point.Def.Pos, EnemyNearM) > 0;
                OrderType type = held ? OrderType.DEFEND : enemyNear ? OrderType.ATTACK_MOVE : OrderType.SEIZE;
                bool far = company.Centroid().DistanceTo(point.Def.Pos) > ArriveM;
                bool idle = company.Posture == "idle" || company.Mission == null || company.Mission.IsFinished();
                if (changed || !LastOrder.TryGetValue(company.Id, out OrderType lastType) || lastType != type || idle && far)
                {
                    double facing = FormationSolver.HeadingFromVector(EnemySideCenter(taskForce) - point.Def.Pos);
                    SimVec2 offset = SimVec2.Zero;
                    int indexHere = 0;
                    foreach (Company other in active)
                    {
                        if (other == company)
                        {
                            break;
                        }

                        if (newAssignments.TryGetValue(other.Id, out string otherPoint) && otherPoint == pointId)
                        {
                            ++indexHere;
                        }
                    }

                    if (indexHere > 0)
                    {
                        offset = FormationSolver.RightVector(facing) * 350.0 * (indexHere % 2 == 1 ? 1 : -1);
                    }

                    Issue(company, type, point.Def.Pos + offset, facing);
                }
            }
        }

        private SimVec2 EnemySideCenter(TaskForce taskForce)
        {
            string enemy = taskForce.Side == "blue" ? "red" : "blue";
            return _battle.Scenario.Sides.TryGetValue(enemy, out ScenarioDef.SideSetup? setup)
                ? setup.DeployZone.Center
                : _battle.Terrain.SizeM * 0.5;
        }

        private SimVec2 Rear(TaskForce taskForce)
        {
            return _battle.Scenario.Sides.TryGetValue(taskForce.Side, out ScenarioDef.SideSetup? setup)
                ? setup.DeployZone.Center
                : taskForce.EntryPos;
        }

        private Order Issue(Unit unit, OrderType type, SimVec2 position, double facing, JObject? parameters = null)
        {
            Order order = Order.Make(type, unit.Id, parameters);
            order.Issuer = "ai";
            TaskForce? taskForce = unit.TaskForce();
            order.IssuerUnitId = taskForce != null ? taskForce.Id : -1;
            SimVec2 clamped = _battle.Terrain.ClampToMap(position, 40.0);
            order.SetPos(_battle.Terrain.NearestPassable(clamped, Terrain.MobilityVehicle));
            if (!double.IsNaN(facing))
            {
                order.SetFacing(facing);
            }

            LastOrder[unit.Id] = type;
            return _battle.IssueOrder(order);
        }

        private Company? LeadCompany(TaskForce taskForce)
        {
            Company? best = null;
            double bestValue = -1.0;
            foreach (Company company in taskForce.Companies)
            {
                if (!company.OnMap || company.IsDestroyed() ||
                    !Assignment.TryGetValue(company.Id, out string pointId) || pointId == "reserve")
                {
                    continue;
                }

                ScoringSystem.PointState? point = _battle.Scoring.GetPoint(pointId);
                double value = point != null ? PointValue(taskForce, point) : 0.0;
                if (value > bestValue)
                {
                    bestValue = value;
                    best = company;
                }
            }

            return best;
        }

        private void Organic(TaskForce taskForce)
        {
            Company? lead = LeadCompany(taskForce);
            SimVec2 enemyDirection = (EnemySideCenter(taskForce) - taskForce.Centroid()).Normalized();
            foreach (Platoon platoon in taskForce.Organic)
            {
                if (!platoon.OnMap || platoon.IsDestroyed() || platoon.Routed)
                {
                    continue;
                }

                bool idle = platoon.Mission == null || platoon.Mission.IsFinished();
                switch (platoon.Kind)
                {
                    case "recon":
                        if (lead != null && idle)
                        {
                            SimVec2 target = _battle.Scoring.GetPoint(Assignment[lead.Id])!.Def.Pos;
                            SimVec2 screen = target - enemyDirection * 300.0;
                            if (platoon.Centroid().DistanceTo(screen) > ArriveM)
                            {
                                Issue(platoon, OrderType.MOVE, screen, FormationSolver.HeadingFromVector(enemyDirection));
                            }
                        }
                        break;
                    case "fo":
                    case "ew":
                    case "aa":
                        if (lead != null && idle)
                        {
                            double back = platoon.Kind == "fo" ? 600.0 : 1500.0;
                            SimVec2 spot = lead.Centroid() - enemyDirection * back;
                            if (platoon.Centroid().DistanceTo(spot) > 400.0)
                            {
                                Issue(platoon, OrderType.MOVE, spot, Unit.NoFacing);
                            }
                        }
                        break;
                    case "at":
                    case "sniper":
                    case "tank":
                    case "engineer":
                        if (lead != null && idle)
                        {
                            SimVec2 spot = lead.Centroid() - enemyDirection * 400.0;
                            if (platoon.Centroid().DistanceTo(spot) > 400.0)
                            {
                                Issue(platoon, OrderType.MOVE, spot, FormationSolver.HeadingFromVector(enemyDirection));
                            }
                        }
                        break;
                    case "aviation":
                        if (lead != null && idle)
                        {
                            SimVec2 objective = _battle.Scoring.GetPoint(Assignment[lead.Id])!.Def.Pos;
                            SimVec2 stand = objective - enemyDirection * 1500.0;
                            if (platoon.Centroid().DistanceTo(stand) > 400.0)
                            {
                                Issue(platoon, OrderType.ATTACK_MOVE, stand, FormationSolver.HeadingFromVector(enemyDirection));
                            }
                        }
                        break;
                    case "mortar":
                        if (lead != null && idle)
                        {
                            string weaponId = _battle.Fires.GetIndirectWeapon(platoon);
                            WeaponDef? weapon = _battle.Catalog.GetWeapon(weaponId);
                            SimVec2 target = _battle.Scoring.GetPoint(Assignment[lead.Id])!.Def.Pos;
                            if (weapon != null && platoon.Centroid().DistanceTo(target) > weapon.RangeMaxM * 0.7)
                            {
                                Issue(platoon, OrderType.MOVE, platoon.Centroid().MoveToward(target, 1200.0), Unit.NoFacing);
                            }
                        }
                        break;
                }
            }
        }

        private void Fires(TaskForce taskForce)
        {
            long tick = _battle.World.Tick;
            SimVec2 bestPosition = SimVec2.Inf;
            int bestCount = 0;
            int bestTanks = 0;
            foreach (Dictionary<string, object> info in taskForce.KnownEnemies.Values)
            {
                long seenTick = ReadInt64(info, "tick");
                if (tick - seenTick > FreshTicks)
                {
                    continue;
                }

                SimVec2 position = ReadPosition(info);
                int count = 0;
                int tanks = 0;
                foreach (Dictionary<string, object> other in taskForce.KnownEnemies.Values)
                {
                    long otherTick = ReadInt64(other, "tick");
                    SimVec2 otherPosition = ReadPosition(other);
                    if (tick - otherTick <= FreshTicks && otherPosition.DistanceTo(position) <= ClusterM)
                    {
                        ++count;
                        if (ReadString(other, "class") == "tank")
                        {
                            ++tanks;
                        }
                    }
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    bestPosition = position;
                    bestTanks = tanks;
                }
            }

            if (bestPosition == SimVec2.Inf)
            {
                return;
            }

            if (!_battle.Fires.HasActiveMission(taskForce.Side, FireSupportAsset.Mortar))
            {
                _battle.Fires.Request(taskForce.Side, FireSupportAsset.Mortar, bestPosition);
            }

            long lastArtillery = _lastArtillery.TryGetValue(taskForce.Side, out long artilleryTick) ? artilleryTick : -100000;
            if (bestCount >= 3 && taskForce.ArtilleryMissions > 0 && tick - lastArtillery > ArtilleryGapTicks)
            {
                IReadOnlyDictionary<string, object> result = _battle.Fires.Request(
                    taskForce.Side, FireSupportAsset.Artillery, bestPosition);
                if (RequestSucceeded(result))
                {
                    _lastArtillery[taskForce.Side] = tick;
                }
            }

            long lastCas = _lastCas.TryGetValue(taskForce.Side, out long casTick) ? casTick : -100000;
            if (bestTanks >= 2 && taskForce.CasSorties > 0 && tick - lastCas > ArtilleryGapTicks)
            {
                IReadOnlyDictionary<string, object> result = _battle.Fires.Request(
                    taskForce.Side, FireSupportAsset.Cas, bestPosition);
                if (RequestSucceeded(result))
                {
                    _lastCas[taskForce.Side] = tick;
                }
            }
        }

        private void Resupply(TaskForce taskForce)
        {
            long tick = _battle.World.Tick;
            foreach (Platoon platoon in taskForce.AllPlatoons())
            {
                if (!platoon.OnMap || platoon.IsDestroyed() || platoon.Kind == "supply" || platoon.Posture == "resupplying")
                {
                    continue;
                }

                if (PlatoonAI.InContact(platoon, tick) || _battle.AmmoRatio(platoon) > LowAmmo)
                {
                    continue;
                }

                Order order = Order.Make(OrderType.RESUPPLY, platoon.Id);
                order.Issuer = "ai";
                _battle.IssueOrder(order);
            }
        }

        private static bool RequestSucceeded(IReadOnlyDictionary<string, object> result)
        {
            return result.TryGetValue("ok", out object value) && value is bool succeeded && succeeded;
        }

        private static long ReadInt64(Dictionary<string, object> info, string key)
        {
            return Convert.ToInt64(info[key], CultureInfo.InvariantCulture);
        }

        private static SimVec2 ReadPosition(Dictionary<string, object> info)
        {
            return (SimVec2)info["pos"];
        }

        private static string ReadString(Dictionary<string, object> info, string key)
        {
            return info.TryGetValue(key, out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
                : "";
        }

        private static long AppendString(long hash, string value)
        {
            foreach (char character in value)
            {
                hash = SimHash.Append(hash, (int)character);
            }

            return hash;
        }
    }
}
