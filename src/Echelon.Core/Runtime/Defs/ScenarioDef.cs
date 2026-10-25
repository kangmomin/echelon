using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class ScenarioDef
    {
        public static readonly string[] SideNames = { "blue", "red" };
        public static readonly string[] ObjectiveTypes = { "seize", "hold" };

        public sealed class SideSetup
        {
            public string Faction { get; set; } = "";
            public string Toe { get; set; } = "";
            public int Budget { get; set; }
            public MapRect DeployZone { get; set; }
            public List<SimVec2> EntryPoints { get; } = new List<SimVec2>();
        }

        public sealed class ControlPoint
        {
            public string Id { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public SimVec2 Pos { get; set; }
            public double RadiusM { get; set; }
            public double VpPerMin { get; set; }
        }

        public sealed class Objective
        {
            public string Id { get; set; } = "";
            public string Side { get; set; } = "";
            public string ObjectiveType { get; set; } = "";
            public string Point { get; set; } = "";
            public double Minutes { get; set; }
            public int Vp { get; set; }
        }

        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string MapId { get; set; } = "";
        public SimVec2 SizeM { get; set; }
        public int SeedValue { get; set; }
        public double TimeLimitMin { get; set; }
        public double CollapseThreshold { get; set; }
        public Dictionary<string, SideSetup> Sides { get; } = new Dictionary<string, SideSetup>();
        public List<ControlPoint> ControlPoints { get; } = new List<ControlPoint>();
        public List<Objective> Objectives { get; } = new List<Objective>();

        public static ScenarioDef FromJObject(JObject data, DefReader reader)
        {
            ScenarioDef scenario = new ScenarioDef();
            scenario.Id = reader.ReqString(data, "id");
            scenario.DisplayName = reader.ReqString(data, "name");
            scenario.MapId = reader.ReqString(data, "map");
            scenario.SizeM = reader.ReqVec2(data, "size_m");
            if (scenario.SizeM.X <= 0.0f || scenario.SizeM.Y <= 0.0f)
            {
                reader.Error("size_m 은 양수여야 함");
            }

            scenario.SeedValue = reader.ReqInt(data, "seed");
            scenario.TimeLimitMin = reader.ReqNumber(data, "time_limit_min");
            reader.CheckRange(scenario.TimeLimitMin, 1.0, 240.0, "time_limit_min");
            scenario.CollapseThreshold = reader.ReqNumber(data, "collapse_threshold");
            reader.CheckRange(scenario.CollapseThreshold, 0.0, 1.0, "collapse_threshold");

            MapRect mapRect = new MapRect(SimVec2.Zero, scenario.SizeM);
            JObject sides = reader.ReqObject(data, "sides");
            foreach (string sideName in SideNames)
            {
                JObject raw = reader.ReqObject(sides, sideName);
                SideSetup side = new SideSetup();
                side.Faction = reader.ReqString(raw, "faction");
                reader.CheckOneOf(side.Faction, ElementDef.Factions, "sides." + sideName + ".faction");
                side.Toe = reader.ReqString(raw, "toe");
                side.Budget = reader.ReqInt(raw, "budget");
                JArray zone = reader.ReqArray(raw, "deploy_zone");
                if (zone.Count == 2)
                {
                    SimVec2 first = reader.AsVec2(zone[0], "sides." + sideName + ".deploy_zone[0]");
                    SimVec2 second = reader.AsVec2(zone[1], "sides." + sideName + ".deploy_zone[1]");
                    side.DeployZone = MapRect.FromCorners(first, second);
                    if (!mapRect.Encloses(side.DeployZone))
                    {
                        reader.Error("sides." + sideName + ".deploy_zone 이 지도 밖으로 나감");
                    }
                }
                else
                {
                    reader.Error("sides." + sideName + ".deploy_zone 은 [[x1,y1],[x2,y2]] 형식이어야 함");
                }

                JArray entries = reader.ReqArray(raw, "entry_points");
                for (int i = 0; i < entries.Count; ++i)
                {
                    SimVec2 point = reader.AsVec2(entries[i],
                        "sides." + sideName + ".entry_points[" + i + "]");
                    if (!mapRect.Grow(0.01).Contains(point))
                    {
                        reader.Error("sides." + sideName + ".entry_points[" + i + "] 가 지도 밖임");
                    }

                    side.EntryPoints.Add(point);
                }

                if (side.EntryPoints.Count == 0)
                {
                    reader.Error("sides." + sideName + ".entry_points 가 비어 있음");
                }

                scenario.Sides[sideName] = side;
            }

            HashSet<string> controlPointIds = new HashSet<string>();
            JArray controlPoints = reader.ReqArray(data, "control_points");
            for (int i = 0; i < controlPoints.Count; ++i)
            {
                JObject raw = reader.AsObject(controlPoints[i], "control_points[" + i + "]");
                ControlPoint point = new ControlPoint();
                point.Id = reader.ReqString(raw, "id");
                point.DisplayName = reader.ReqString(raw, "name");
                point.Pos = reader.ReqVec2(raw, "pos");
                point.RadiusM = reader.ReqNumber(raw, "radius_m");
                point.VpPerMin = reader.ReqNumber(raw, "vp_per_min");
                if (!mapRect.Contains(point.Pos))
                {
                    reader.Error("control_points '" + point.Id + "' 위치가 지도 밖임");
                }

                if (!controlPointIds.Add(point.Id))
                {
                    reader.Error("control_points id '" + point.Id + "' 중복");
                }

                scenario.ControlPoints.Add(point);
            }

            JArray objectives = reader.OptArray(data, "objectives");
            for (int i = 0; i < objectives.Count; ++i)
            {
                JObject raw = reader.AsObject(objectives[i], "objectives[" + i + "]");
                Objective objective = new Objective();
                objective.Id = reader.ReqString(raw, "id");
                objective.Side = reader.ReqString(raw, "side");
                reader.CheckOneOf(objective.Side, SideNames, "objectives[" + i + "].side");
                objective.ObjectiveType = reader.ReqString(raw, "type");
                reader.CheckOneOf(objective.ObjectiveType, ObjectiveTypes, "objectives[" + i + "].type");
                objective.Point = reader.ReqString(raw, "point");
                if (!controlPointIds.Contains(objective.Point))
                {
                    reader.Error("objectives[" + i + "].point '" + objective.Point + "' 가 control_points 에 없음");
                }

                objective.Minutes = reader.ReqNumber(raw, "minutes");
                objective.Vp = reader.ReqInt(raw, "vp");
                scenario.Objectives.Add(objective);
            }

            return scenario;
        }
    }
}
