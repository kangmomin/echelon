using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class CommsNet : SimSystem
    {
        public const int UpdateInterval = 20;
        public const int HalfInterval = 10;
        public const double FullQualityRange = 0.6;
        public const double EdgeQuality = 0.3;
        public const double ForestAtten = 0.8;
        public const double UrbanAtten = 0.7;
        public const double RidgeAtten = 0.5;
        public const double RidgeClearanceM = 40.0;
        public const int RidgeSamples = 12;
        public const double JamRadiusM = 1500.0;
        public const double JamFactor = 0.3;
        public const int MaxRelays = 3;
        public const double RelayDelaySec = 10.0;
        public const double GoodThreshold = 0.7;

        private static readonly string[] Sides = { "blue", "red" };
        private static readonly Dictionary<int, List<CommsEdge>> EmptyEdges = new Dictionary<int, List<CommsEdge>>();

        private readonly ICommandBattle _battle;
        private readonly Dictionary<string, List<Element>> _nodes = new Dictionary<string, List<Element>>();
        private readonly Dictionary<string, Dictionary<int, List<CommsEdge>>> _edges =
            new Dictionary<string, Dictionary<int, List<CommsEdge>>>();
        private readonly Dictionary<string, List<Element>> _jammers = new Dictionary<string, List<Element>>();
        private long _builtTick = -1;

        public CommsNet(ICommandBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public override string GetSystemName()
        {
            return "comms";
        }

        public override void Step(SimWorld world)
        {
            if (_builtTick < 0)
            {
                Rebuild();
                UpdateUnitStates();
                return;
            }

            long phase = world.Tick % UpdateInterval;
            if (phase == 0)
            {
                RebuildSide("blue");
                UpdateUnitStates("blue");
            }
            else if (phase == HalfInterval)
            {
                RebuildSide("red");
                UpdateUnitStates("red");
            }
        }

        public override long StateHash()
        {
            long hash = SimHash.Initial;
            foreach (Unit unit in _battle.Units)
            {
                hash = SimHash.Append(hash, (int)unit.CommsState);
                double quality = SimMath.Snap(unit.CommsQuality, 0.01);
                hash = SimHash.Append(hash, BitConverter.DoubleToInt64Bits(quality));
            }

            return hash;
        }

        public void Rebuild()
        {
            _builtTick = _battle.World.Tick;
            foreach (string side in Sides)
            {
                RebuildSide(side);
            }
        }

        public double LinkQuality(Element sender, Element receiver)
        {
            double distance = sender.Pos.DistanceTo(receiver.Pos);
            if (distance > sender.Def.RadioRangeM)
            {
                return 0.0;
            }

            double attenuation = Attenuation(sender, receiver);
            return QualityFor(distance, sender.Def.RadioRangeM, attenuation, ReceiverJamFactor(receiver));
        }

        public CommsLink LinkBetween(Unit fromUnit, Unit toUnit)
        {
            Element? from = fromUnit.Leader();
            Element? to = toUnit.Leader();
            CommsLink link = new CommsLink();
            if (from == null || to == null || !from.IsActive() || !to.IsActive())
            {
                return link;
            }

            if (from == to)
            {
                link.Ok = true;
                link.Quality = 1.0;
                return link;
            }

            Dictionary<int, List<CommsEdge>> edges = _edges.TryGetValue(fromUnit.Side, out Dictionary<int, List<CommsEdge>>? found)
                ? found
                : EmptyEdges;
            HashSet<int> network = NetMembers(fromUnit);
            return FindLink(from.Id, to.Id, edges, network);
        }

        public static CommsLink FindLink(
            int fromElementId,
            int toElementId,
            IReadOnlyDictionary<int, List<CommsEdge>> edges,
            ISet<int> networkMembers)
        {
            CommsLink link = new CommsLink();
            if (fromElementId == toElementId)
            {
                link.Ok = true;
                link.Quality = 1.0;
                return link;
            }

            if (edges.TryGetValue(fromElementId, out List<CommsEdge>? directRow))
            {
                foreach (CommsEdge edge in directRow)
                {
                    if (edge.ToElementId == toElementId && edge.Quality > 0.0)
                    {
                        link.Ok = true;
                        link.Quality = edge.Quality;
                        return link;
                    }
                }
            }

            Dictionary<int, double> bestQuality = new Dictionary<int, double> { [fromElementId] = 1.0 };
            List<int> frontier = new List<int> { fromElementId };
            for (int depth = 0; depth < MaxRelays + 1; ++depth)
            {
                List<int> next = new List<int>();
                foreach (int nodeId in frontier)
                {
                    if (!edges.TryGetValue(nodeId, out List<CommsEdge>? row))
                    {
                        continue;
                    }

                    foreach (CommsEdge edge in row)
                    {
                        int intermediateId = edge.ToElementId;
                        if (edge.Quality <= 0.0 || (intermediateId != toElementId && !networkMembers.Contains(intermediateId)))
                        {
                            continue;
                        }

                        double quality = Math.Min(bestQuality[nodeId], edge.Quality);
                        if (intermediateId == toElementId)
                        {
                            link.Ok = true;
                            link.Quality = quality;
                            link.Relays = depth;
                            return link;
                        }

                        if (!bestQuality.ContainsKey(intermediateId))
                        {
                            bestQuality.Add(intermediateId, quality);
                            next.Add(intermediateId);
                        }
                    }
                }

                frontier = next;
            }

            return link;
        }

        public static double DelayFactor(double quality)
        {
            return SimMath.Clamp(2.0 - quality, 1.0, 2.0);
        }

        public static Unit.Comms StateOf(CommsLink link)
        {
            if (!link.Ok)
            {
                return Unit.Comms.Cut;
            }

            return link.Quality >= GoodThreshold && link.Relays == 0 ? Unit.Comms.Good : Unit.Comms.Poor;
        }

        public static Unit? SuperiorOf(Unit unit)
        {
            return unit.Parent;
        }

        public static double QualityFor(double distance, double radioRange, double attenuation, double jamFactor)
        {
            if (distance > radioRange)
            {
                return 0.0;
            }

            double range = radioRange * attenuation;
            if (distance > range)
            {
                return 0.0;
            }

            double quality = 1.0;
            if (distance > range * FullQualityRange)
            {
                double weight = (distance - range * FullQualityRange) / (range * (1.0 - FullQualityRange));
                quality = SimMath.Lerp(1.0, EdgeQuality, weight);
            }

            return quality * jamFactor;
        }

        public static double TerrainAttenuation(bool forest, bool urban, bool ridge)
        {
            double factor = 1.0;
            if (forest)
            {
                factor *= ForestAtten;
            }

            if (urban)
            {
                factor *= UrbanAtten;
            }

            if (ridge)
            {
                factor *= RidgeAtten;
            }

            return factor;
        }

        public void UpdateUnitStates(string onlySide = "")
        {
            foreach (Unit unit in _battle.Units)
            {
                if (unit.Level == UnitLevel.TaskForce || !unit.OnMap || (onlySide.Length != 0 && unit.Side != onlySide))
                {
                    continue;
                }

                Unit? superior = SuperiorOf(unit);
                if (superior == null)
                {
                    continue;
                }

                CommsLink link = LinkBetween(superior, unit);
                Unit.Comms old = unit.CommsState;
                unit.CommsQuality = link.Quality;
                unit.CommsHops = link.Relays + 1;
                unit.CommsState = StateOf(link);
                if (old != unit.CommsState && (old == Unit.Comms.Cut || unit.CommsState == Unit.Comms.Cut))
                {
                    _battle.World.Events.EmitEvent("comms_changed", _battle.World.Tick, new Dictionary<string, object>
                    {
                        ["unit"] = unit.Id,
                        ["side"] = unit.Side,
                        ["state"] = (int)unit.CommsState
                    });
                }
            }
        }

        private void RebuildSide(string side)
        {
            List<Element> nodes = new List<Element>();
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce != null)
            {
                foreach (Unit unit in taskForce.AllUnits())
                {
                    Element? commander = unit.Leader();
                    if (commander != null && commander.IsActive() && !nodes.Contains(commander))
                    {
                        nodes.Add(commander);
                    }
                }
            }

            _nodes[side] = nodes;
            List<Element> jammers = new List<Element>();
            foreach (Element element in _battle.Elements)
            {
                if (element.Side != side && element.IsActive() && element.Def.ElementClass == "ew")
                {
                    jammers.Add(element);
                }
            }

            _jammers[side] = jammers;
            Dictionary<int, List<CommsEdge>> edges = new Dictionary<int, List<CommsEdge>>();
            foreach (Element from in nodes)
            {
                List<CommsEdge> row = new List<CommsEdge>();
                foreach (Element to in nodes)
                {
                    if (from == to)
                    {
                        continue;
                    }

                    double quality = LinkQuality(from, to);
                    if (quality > 0.0)
                    {
                        row.Add(new CommsEdge(to.Id, quality));
                    }
                }

                edges[from.Id] = row;
            }

            _edges[side] = edges;
        }

        private HashSet<int> NetMembers(Unit fromUnit)
        {
            HashSet<int> output = new HashSet<int>();
            List<Unit> members = new List<Unit>();
            if (fromUnit.Level == UnitLevel.Company)
            {
                members.Add(fromUnit);
                members.AddRange(fromUnit.ChildrenUnits());
            }
            else
            {
                TaskForce? taskForce = fromUnit.TaskForce();
                if (taskForce != null)
                {
                    members.Add(taskForce);
                    members.AddRange(taskForce.ChildrenUnits());
                }
            }

            foreach (Unit unit in members)
            {
                Element? leader = unit.Leader();
                if (leader != null)
                {
                    output.Add(leader.Id);
                }
            }

            return output;
        }

        private double Attenuation(Element from, Element to)
        {
            Terrain terrain = _battle.Terrain;
            int fromType = terrain.TypeAt(from.Pos);
            int toType = terrain.TypeAt(to.Pos);
            bool forest = fromType == (int)Terrain.Type.FOREST || toType == (int)Terrain.Type.FOREST;
            bool urban = fromType == (int)Terrain.Type.URBAN || toType == (int)Terrain.Type.URBAN;
            double fromHeight = terrain.GroundHeight(from.Pos);
            double toHeight = terrain.GroundHeight(to.Pos);
            double top = Math.Max(fromHeight, toHeight);
            bool ridge = false;
            for (int i = 1; i < RidgeSamples; ++i)
            {
                SimVec2 sample = from.Pos.Lerp(to.Pos, (double)i / RidgeSamples);
                if (terrain.GroundHeight(sample) > top + RidgeClearanceM)
                {
                    ridge = true;
                    break;
                }
            }

            return TerrainAttenuation(forest, urban, ridge);
        }

        private double ReceiverJamFactor(Element receiver)
        {
            if (_jammers.TryGetValue(receiver.Side, out List<Element>? jammers))
            {
                foreach (Element jammer in jammers)
                {
                    if (jammer.IsActive() && jammer.Pos.DistanceTo(receiver.Pos) <= JamRadiusM)
                    {
                        return JamFactor;
                    }
                }
            }

            return 1.0;
        }
    }
}
