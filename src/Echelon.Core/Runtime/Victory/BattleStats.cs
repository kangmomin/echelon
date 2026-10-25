using System;
using System.Collections.Generic;
using System.Globalization;

namespace Echelon.Core
{
    public sealed class BattleStats
    {
        public static readonly string[] TimelineTypes =
        {
            "battle_started", "point_captured", "objective_done", "objective_failed", "reinforcement_arrived",
            "routed", "commander_lost", "cas_shot_down", "battle_ended", "auto_withdraw"
        };

        private static readonly HashSet<string> TimelineTypeSet = new HashSet<string>(TimelineTypes);
        private static readonly string[] SideNames = { "blue", "red" };
        private readonly IBattleStatsBattle _battle;

        public BattleStats(IBattleStatsBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _battle.World.Events.AddListener(OnEvent);
        }

        public Dictionary<string, Dictionary<string, int>> Losses { get; } =
            new Dictionary<string, Dictionary<string, int>>
            {
                ["blue"] = new Dictionary<string, int>(),
                ["red"] = new Dictionary<string, int>()
            };
        public Dictionary<string, int> Damaged { get; } = SideCounters();
        public Dictionary<string, List<double>> OrderDelays { get; } = new Dictionary<string, List<double>>
        {
            ["blue"] = new List<double>(),
            ["red"] = new List<double>()
        };
        public Dictionary<string, int> FireMissions { get; } = SideCounters();
        public Dictionary<string, int> CommsCuts { get; } = SideCounters();
        public Dictionary<string, int> Resupplies { get; } = SideCounters();
        public Dictionary<string, int> Shots { get; } = SideCounters();
        public long FirstContactTick { get; private set; } = -1;
        public List<Dictionary<string, object>> Timeline { get; } = new List<Dictionary<string, object>>();

        public void OnEvent(SimEvent simEvent)
        {
            if (simEvent == null)
            {
                throw new ArgumentNullException(nameof(simEvent));
            }

            string type = simEvent.Type;
            long tick = simEvent.Tick;
            Dictionary<string, object> payload = simEvent.Payload;
            string side = GetString(payload, "side");
            switch (type)
            {
                case "element_destroyed":
                    int elementId = GetInt(payload, "element", -1);
                    Element? element = _battle.GetElement(elementId);
                    if (element != null && Losses.TryGetValue(element.Side, out Dictionary<string, int>? byClass))
                    {
                        string elementClass = element.Def.ElementClass;
                        byClass[elementClass] = (byClass.TryGetValue(elementClass, out int previous) ? previous : 0) + 1;
                    }
                    break;
                case "element_damaged":
                    if (Damaged.ContainsKey(side))
                    {
                        Damaged[side] += 1;
                    }
                    break;
                case "order_received":
                    string issuer = GetString(payload, "issuer");
                    if ((issuer == "player" || issuer == "ai") && OrderDelays.TryGetValue(side, out List<double>? delays))
                    {
                        delays.Add(GetDouble(payload, "delay"));
                    }
                    break;
                case "fire_requested":
                    if (FireMissions.ContainsKey(side))
                    {
                        FireMissions[side] += 1;
                    }
                    break;
                case "comms_changed":
                    int state = GetInt(payload, "state");
                    if (state == (int)Unit.Comms.Cut && CommsCuts.ContainsKey(side))
                    {
                        CommsCuts[side] += 1;
                    }
                    break;
                case "resupplied":
                    if (Resupplies.ContainsKey(side))
                    {
                        Resupplies[side] += 1;
                    }
                    break;
                case "shot":
                    int shooterId = GetInt(payload, "from", -1);
                    Element? shooter = _battle.GetElement(shooterId);
                    if (shooter != null && Shots.ContainsKey(shooter.Side))
                    {
                        Shots[shooter.Side] += 1;
                        if (FirstContactTick < 0)
                        {
                            FirstContactTick = tick;
                        }
                    }
                    break;
            }

            if (TimelineTypeSet.Contains(type))
            {
                Timeline.Add(new Dictionary<string, object>
                {
                    ["tick"] = tick,
                    ["type"] = type,
                    ["side"] = side,
                    ["text"] = Describe(type, payload)
                });
            }
        }

        public string Describe(string type, IReadOnlyDictionary<string, object> payload)
        {
            int unitId = GetInt(payload, "unit", -1);
            Unit? unit = _battle.GetUnit(unitId);
            string who = unit != null ? unit.Label() : "";
            string sideName = SideLabel(GetString(payload, "side"));
            switch (type)
            {
                case "battle_started":
                    return "전투 시작";
                case "point_captured":
                    return string.Format(CultureInfo.InvariantCulture, "{0}: {1} 점령", sideName, GetString(payload, "name"));
                case "objective_done":
                    return string.Format(CultureInfo.InvariantCulture, "{0}: 임무 목표 달성 — {1} (+{2})",
                        sideName, GetString(payload, "name"), GetInt(payload, "vp"));
                case "objective_failed":
                    return string.Format(CultureInfo.InvariantCulture, "{0}: 임무 목표 실패 — {1}",
                        sideName, GetString(payload, "name"));
                case "reinforcement_arrived":
                    return string.Format(CultureInfo.InvariantCulture, "{0}: {1} 증원 도착", sideName, who);
                case "routed":
                    return string.Format(CultureInfo.InvariantCulture, "{0}: {1} 패주", sideName, who);
                case "commander_lost":
                    return string.Format(CultureInfo.InvariantCulture, "{0}: {1} 지휘관 손실", sideName, who);
                case "cas_shot_down":
                    return string.Format(CultureInfo.InvariantCulture, "{0}: 근접항공지원 격추됨", sideName);
                case "auto_withdraw":
                    return string.Format(CultureInfo.InvariantCulture, "{0}: {1} 전투력 저하로 철수", sideName, who);
                case "battle_ended":
                    return "전투 종료";
                default:
                    return type;
            }
        }

        public static string SideLabel(object side)
        {
            string value = VictoryData.Text(side);
            return value == "blue" ? "청군" : value == "red" ? "적군" : value;
        }

        public Dictionary<string, object> Summary()
        {
            Dictionary<string, object> sides = new Dictionary<string, object>();
            foreach (string side in SideNames)
            {
                int lost = 0;
                foreach (int count in Losses[side].Values)
                {
                    lost += count;
                }

                TaskForce? taskForce = _battle.GetTaskForce(side);
                sides[side] = new Dictionary<string, object>
                {
                    ["name"] = taskForce != null ? taskForce.DisplayName : "",
                    ["losses"] = new Dictionary<string, int>(Losses[side]),
                    ["losses_total"] = lost,
                    ["damaged"] = Damaged[side],
                    ["elements"] = taskForce != null ? taskForce.AllElements().Count : 0,
                    ["order_delay_mean"] = Mean(OrderDelays[side]),
                    ["order_delay_max"] = Max(OrderDelays[side]),
                    ["orders"] = OrderDelays[side].Count,
                    ["fire_missions"] = FireMissions[side],
                    ["comms_cuts"] = CommsCuts[side],
                    ["resupplies"] = Resupplies[side],
                    ["shots"] = Shots[side],
                    ["vp"] = _battle.Scoring != null ? _battle.Scoring.Vp[side] : 0.0,
                    ["value_ratio"] = _battle.Scoring != null ? _battle.Scoring.ValueRatio(side) : 1.0
                };
            }

            List<Dictionary<string, object>> points = new List<Dictionary<string, object>>();
            if (_battle.Scoring != null)
            {
                foreach (ScoringSystem.PointState point in _battle.Scoring.Points)
                {
                    points.Add(new Dictionary<string, object>
                    {
                        ["id"] = point.Def.Id,
                        ["name"] = point.Def.DisplayName,
                        ["owner"] = point.Owner,
                        ["held_blue_sec"] = point.HeldSec["blue"],
                        ["held_red_sec"] = point.HeldSec["red"]
                    });
                }
            }

            List<Dictionary<string, object>> timeline = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> entry in Timeline)
            {
                timeline.Add(VictoryData.CopyDictionary(entry));
            }

            return new Dictionary<string, object>
            {
                ["result"] = VictoryData.CopyDictionary(_battle.Result),
                ["duration_sec"] = _battle.World.GetSimTimeSec(),
                ["first_contact_sec"] = FirstContactTick >= 0 ? FirstContactTick / (double)SimConst.TickRate : -1.0,
                ["sides"] = sides,
                ["points"] = points,
                ["timeline"] = timeline
            };
        }

        private static Dictionary<string, int> SideCounters()
        {
            return new Dictionary<string, int> { ["blue"] = 0, ["red"] = 0 };
        }

        private static double Mean(List<double> samples)
        {
            if (samples.Count == 0)
            {
                return 0.0;
            }

            double total = 0.0;
            foreach (double sample in samples)
            {
                total += sample;
            }

            return total / samples.Count;
        }

        private static double Max(List<double> samples)
        {
            double maximum = 0.0;
            foreach (double sample in samples)
            {
                maximum = Math.Max(maximum, sample);
            }

            return maximum;
        }

        private static string GetString(IReadOnlyDictionary<string, object> payload, string key, string defaultValue = "")
        {
            return payload.TryGetValue(key, out object value) ? VictoryData.Text(value) : defaultValue;
        }

        private static double GetDouble(IReadOnlyDictionary<string, object> payload, string key, double defaultValue = 0.0)
        {
            return payload.TryGetValue(key, out object value) ? VictoryData.Number(value) : defaultValue;
        }

        private static int GetInt(IReadOnlyDictionary<string, object> payload, string key, int defaultValue = 0)
        {
            return payload.TryGetValue(key, out object value) ? (int)VictoryData.Integer(value) : defaultValue;
        }
    }
}
