using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class ScoringSystem : SimSystem
    {
        public const int Interval = 10;

        public sealed class PointState
        {
            public PointState(ScenarioDef.ControlPoint definition)
            {
                Def = definition ?? throw new ArgumentNullException(nameof(definition));
            }

            public ScenarioDef.ControlPoint Def { get; }
            public string Owner { get; set; } = "";
            public bool Contested { get; set; }
            public Dictionary<string, double> HeldSec { get; } = new Dictionary<string, double>
            {
                ["blue"] = 0.0,
                ["red"] = 0.0
            };
        }

        private static readonly string[] SideNames = { "blue", "red" };
        private readonly IScoringBattle _battle;
        private readonly HashSet<int> _committedElements = new HashSet<int>();

        public ScoringSystem(IScoringBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            foreach (ScenarioDef.ControlPoint controlPoint in battle.Scenario.ControlPoints)
            {
                Points.Add(new PointState(controlPoint));
            }
        }

        public List<PointState> Points { get; } = new List<PointState>();
        public Dictionary<string, bool> ObjectivesDone { get; } = new Dictionary<string, bool>();
        public Dictionary<string, double> Vp { get; } = new Dictionary<string, double>
        {
            ["blue"] = 0.0,
            ["red"] = 0.0
        };
        public Dictionary<string, double> CommittedValue { get; } = new Dictionary<string, double>();

        public override string GetSystemName()
        {
            return "scoring";
        }

        public override long StateHash()
        {
            long hash = SimHash.Append(SimHash.Initial, Round(Vp["blue"] * 10.0));
            hash = SimHash.Append(hash, Round(Vp["red"] * 10.0));
            foreach (PointState point in Points)
            {
                hash = AppendString(hash, point.Owner);
                hash = SimHash.Append(hash, point.Contested ? 1 : 0);
            }

            return hash;
        }

        public PointState? GetPoint(string pointId)
        {
            foreach (PointState point in Points)
            {
                if (point.Def.Id == pointId)
                {
                    return point;
                }
            }

            return null;
        }

        public double ForceValue(string side)
        {
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce == null)
            {
                return 0.0;
            }

            double total = 0.0;
            foreach (Element element in taskForce.AllElements())
            {
                // 고정익은 지도 밖을 오가는 지원 자산이라 전투력 비율에 넣지 않는다(D-050).
                if (element.OnMap && !element.IsFixedWing())
                {
                    total += element.Def.Cost * element.Strength();
                }
            }

            return total;
        }

        public double ValueRatio(string side)
        {
            double baseline = CommittedValue.TryGetValue(side, out double value) ? value : 0.0;
            return baseline <= 0.0 ? 1.0 : ForceValue(side) / baseline;
        }

        public override void Step(SimWorld world)
        {
            if (_battle.HasResult)
            {
                return;
            }

            UpdateCommittedValue();
            if (world.Tick % Interval != 0)
            {
                return;
            }

            double seconds = Interval * SimConst.TickDt;
            UpdatePoints(world.Tick, seconds);
            UpdateObjectives(world.Tick);
            CheckEnd(world);
        }

        private void UpdateCommittedValue()
        {
            foreach (string side in SideNames)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                foreach (Element element in taskForce.AllElements())
                {
                    if (!element.OnMap || element.IsFixedWing() || _committedElements.Contains(element.Id))
                    {
                        continue;
                    }

                    _committedElements.Add(element.Id);
                    CommittedValue[side] = (CommittedValue.TryGetValue(side, out double value) ? value : 0.0) + element.Def.Cost;
                }
            }
        }

        private void UpdatePoints(long tick, double seconds)
        {
            foreach (PointState point in Points)
            {
                Dictionary<string, int> present = new Dictionary<string, int>
                {
                    ["blue"] = 0,
                    ["red"] = 0
                };
                foreach (Element element in _battle.ElementsNear(point.Def.Pos, point.Def.RadiusM))
                {
                    if (element.IsMounted() || !element.IsActive() || element.IsFixedWing())
                    {
                        continue;
                    }

                    if (present.ContainsKey(element.Side))
                    {
                        present[element.Side] = present[element.Side] + 1;
                    }
                }

                bool blue = present["blue"] > 0;
                bool red = present["red"] > 0;
                point.Contested = blue && red;
                string newOwner = point.Owner;
                if (blue && !red)
                {
                    newOwner = "blue";
                }
                else if (red && !blue)
                {
                    newOwner = "red";
                }

                if (newOwner != point.Owner)
                {
                    string oldOwner = point.Owner;
                    point.Owner = newOwner;
                    _battle.World.Events.EmitEvent("point_captured", tick, new Dictionary<string, object>
                    {
                        ["point"] = point.Def.Id,
                        ["name"] = point.Def.DisplayName,
                        ["side"] = newOwner,
                        ["from"] = oldOwner
                    });
                }

                if (point.Owner != "")
                {
                    Vp[point.Owner] += point.Def.VpPerMin / 60.0 * seconds;
                    point.HeldSec[point.Owner] = point.HeldSec[point.Owner] + seconds;
                }
            }

            foreach (string side in SideNames)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce != null)
                {
                    taskForce.Vp = Vp[side];
                }
            }
        }

        private void UpdateObjectives(long tick)
        {
            double minutesNow = tick / (double)SimConst.TickRate / 60.0;
            foreach (ScenarioDef.Objective objective in _battle.Scenario.Objectives)
            {
                if (ObjectivesDone.ContainsKey(objective.Id))
                {
                    continue;
                }

                PointState? point = GetPoint(objective.Point);
                if (point == null)
                {
                    continue;
                }

                bool done = false;
                if (objective.ObjectiveType == "seize")
                {
                    if (minutesNow > objective.Minutes)
                    {
                        ObjectivesDone[objective.Id] = false;
                        _battle.World.Events.EmitEvent("objective_failed", tick, new Dictionary<string, object>
                        {
                            ["objective"] = objective.Id,
                            ["side"] = objective.Side,
                            ["name"] = point.Def.DisplayName
                        });
                        continue;
                    }

                    done = point.Owner == objective.Side && !point.Contested;
                }
                else
                {
                    double held = point.HeldSec.TryGetValue(objective.Side, out double seconds) ? seconds : 0.0;
                    done = held >= objective.Minutes * 60.0;
                }

                if (done)
                {
                    ObjectivesDone[objective.Id] = true;
                    Vp[objective.Side] += objective.Vp;
                    _battle.World.Events.EmitEvent("objective_done", tick, new Dictionary<string, object>
                    {
                        ["objective"] = objective.Id,
                        ["side"] = objective.Side,
                        ["vp"] = objective.Vp,
                        ["name"] = point.Def.DisplayName,
                        ["type"] = objective.ObjectiveType
                    });
                }
            }
        }

        private void CheckEnd(SimWorld world)
        {
            long tick = world.Tick;
            foreach (string side in SideNames)
            {
                if (ValueRatio(side) <= _battle.Scenario.CollapseThreshold)
                {
                    End(side == "blue" ? "red" : "blue", "collapse", tick, side);
                    return;
                }
            }

            if (world.GetSimTimeSec() >= _battle.Scenario.TimeLimitMin * 60.0)
            {
                double blue = Vp["blue"];
                double red = Vp["red"];
                string winner = blue > red ? "blue" : red > blue ? "red" : "";
                End(winner, "time", tick, "");
            }
        }

        private void End(string winner, string reason, long tick, string collapsed)
        {
            Dictionary<string, object> result = new Dictionary<string, object>
            {
                ["winner"] = winner,
                ["reason"] = reason,
                ["tick"] = tick,
                ["collapsed"] = collapsed,
                ["vp"] = new Dictionary<string, object>
                {
                    ["blue"] = Vp["blue"],
                    ["red"] = Vp["red"]
                },
                ["value_ratio"] = new Dictionary<string, object>
                {
                    ["blue"] = ValueRatio("blue"),
                    ["red"] = ValueRatio("red")
                }
            };
            _battle.SetResult(result);
        }

        private static long Round(double value)
        {
            return (long)Math.Round(value, MidpointRounding.AwayFromZero);
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
