using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class ContactReport
    {
        public SimVec2 Position { get; set; }
        public string ElementClass { get; set; } = "";
        public long Tick { get; set; }
        public int PlatoonId { get; set; }
        public long DeliverTick { get; set; } = -1;
    }

    public sealed class DetectionSystem : SimSystem
    {
        public const int ScanInterval = 20;
        public const int MaxCandidates = 20;
        public const double BaseProbability = 0.6;
        public const double MaxProbability = 0.95;
        public const double ReportHopSec = 5.0;
        public const int ReportRetryInterval = 10;
        public const int ContactMemoryTicks = 100;
        public const int KnownExpireTicks = 1800;
        public const double TrackingBonus = 2.0;
        public const double AirHighVisibility = 1.5;

        private static readonly string[] Sides = { "blue", "red" };
        private static readonly Dictionary<int, double> TerrainConceal = new Dictionary<int, double>
        {
            [(int)Terrain.Type.FOREST] = 0.5,
            [(int)Terrain.Type.URBAN] = 0.6
        };

        private readonly ICombatBattle _battle;
        private readonly Dictionary<string, InsertionOrderedDictionary<int, ContactReport>> _pending =
            new Dictionary<string, InsertionOrderedDictionary<int, ContactReport>>();

        public DetectionSystem(ICombatBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            Seen["blue"] = new Dictionary<int, long>();
            Seen["red"] = new Dictionary<int, long>();
            _pending["blue"] = new InsertionOrderedDictionary<int, ContactReport>();
            _pending["red"] = new InsertionOrderedDictionary<int, ContactReport>();
        }

        public Dictionary<string, Dictionary<int, long>> Seen { get; } =
            new Dictionary<string, Dictionary<int, long>>();
        public int DetectionsTotal { get; private set; }
        public int ReportsDelivered { get; private set; }

        public override string GetSystemName()
        {
            return "detection";
        }

        public override long StateHash()
        {
            long hash = SimHash.Append(SimHash.Initial, DetectionsTotal);
            return SimHash.Append(hash, ReportsDelivered);
        }

        public override void Step(SimWorld world)
        {
            long tick = world.Tick;
            foreach (Element element in _battle.Elements)
            {
                if ((element.Id + tick) % ScanInterval != 0 || !element.IsActive() || element.IsMounted())
                {
                    continue;
                }

                Scan(element, tick);
            }

            ProcessReports(tick);
            if (tick % 50 == 0)
            {
                Expire(tick);
            }
        }

        public double Probability(Element observer, Element target, double distance, long tick)
        {
            double rangeFactor = SimMath.Clamp(
                1.0 - Math.Pow(distance / Math.Max(observer.Def.OpticsM, 1.0), 1.5), 0.0, 1.0);
            double conceal = 1.0 - target.Def.Stealth;
            double terrainFactor = TerrainConceal.TryGetValue(_battle.Terrain.TypeAt(target.Pos), out double concealment)
                ? concealment
                : 1.0;
            if (target.IsAir())
            {
                terrainFactor = target.AltitudeM() >= Element.AltitudeHighM ? AirHighVisibility : 1.0;
            }

            double stateFactor = 1.0;
            if (tick - target.LastFiredTick < 50)
            {
                stateFactor = 2.0;
            }
            else if (target.Velocity.Length() > 1.0)
            {
                stateFactor = 1.5;
            }
            else if (target.StationaryTicks > 300)
            {
                stateFactor = 0.5;
            }

            Dictionary<int, long> sideSeen = Seen[observer.Side];
            double tracking = sideSeen.TryGetValue(target.Id, out long lastSeen) &&
                lastSeen > tick - ContactMemoryTicks
                ? TrackingBonus
                : 1.0;
            double flankFactor = 1.0;
            Platoon? platoon = observer.Platoon;
            if (platoon != null && platoon.Formation != null)
            {
                double relative = Math.Abs(SimMath.AngleDifference(
                    platoon.Guide.Heading,
                    FormationSolver.HeadingFromVector(target.Pos - observer.Pos)));
                if (relative > Math.PI / 4.0)
                {
                    flankFactor = 0.5 + platoon.Formation.GetMod("flank_security", 0.5);
                }
            }

            double probability = BaseProbability * rangeFactor * target.Def.Size * conceal * terrainFactor *
                stateFactor * tracking * flankFactor;
            return SimMath.Clamp(probability, 0.0, MaxProbability);
        }

        public double RouteDelaySec(Platoon platoon)
        {
            TaskForce? taskForce = platoon.TaskForce();
            if (taskForce == null)
            {
                return -1.0;
            }

            CommsNet comms = _battle.Comms;
            if (platoon.Parent != null && platoon.Parent.Level == UnitLevel.Company)
            {
                Unit company = platoon.Parent;
                CommsLink first = comms.LinkBetween(company, platoon);
                CommsLink second = comms.LinkBetween(taskForce, company);
                if (!first.Ok || !second.Ok)
                {
                    return -1.0;
                }

                return ReportHopSec * (CommsNet.DelayFactor(first.Quality) + CommsNet.DelayFactor(second.Quality)) +
                    (first.Relays + second.Relays) * CommsNet.RelayDelaySec;
            }

            CommsLink link = comms.LinkBetween(taskForce, platoon);
            if (!link.Ok)
            {
                return -1.0;
            }

            return ReportHopSec * CommsNet.DelayFactor(link.Quality) + link.Relays * CommsNet.RelayDelaySec;
        }

        public bool SeenRecently(string side, int elementId, long tick)
        {
            if (!Seen.TryGetValue(side, out Dictionary<int, long>? sideSeen) ||
                !sideSeen.TryGetValue(elementId, out long lastSeen))
            {
                return false;
            }

            return tick - lastSeen <= ContactMemoryTicks;
        }

        public int PendingCount(string side)
        {
            return _pending.TryGetValue(side, out InsertionOrderedDictionary<int, ContactReport>? pending) ? pending.Count : 0;
        }

        public static int Confidence(IReadOnlyDictionary<string, object> info, long tick)
        {
            long age = tick - ReadTick(info);
            if (age <= 50)
            {
                return 0;
            }

            return age <= 300 ? 1 : 2;
        }

        private void Scan(Element observer, long tick)
        {
            double optics = observer.Def.OpticsM;
            double rangeSquared = optics * optics;
            List<Element> candidates = new List<Element>();
            foreach (Element target in _battle.Spatial.Query(observer.Pos, optics))
            {
                if (target.Side == observer.Side || !target.IsActive() || target.IsMounted())
                {
                    continue;
                }

                if (target.Pos.DistanceSquaredTo(observer.Pos) <= rangeSquared)
                {
                    candidates.Add(target);
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            candidates.Sort((left, right) =>
            {
                double leftDistance = left.Pos.DistanceSquaredTo(observer.Pos);
                double rightDistance = right.Pos.DistanceSquaredTo(observer.Pos);
                int comparison = leftDistance.CompareTo(rightDistance);
                return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
            });
            int count = Math.Min(candidates.Count, MaxCandidates);
            for (int i = 0; i < count; ++i)
            {
                Element target = candidates[i];
                if (!Los.Between(_battle.Terrain, _battle.Smokes, tick, observer, target))
                {
                    continue;
                }

                double probability = Probability(observer, target, observer.Pos.DistanceTo(target.Pos), tick);
                if (_battle.World.Rng.Chance(probability))
                {
                    OnDetected(observer, target, tick);
                }
            }
        }

        private void OnDetected(Element observer, Element target, long tick)
        {
            Platoon? platoon = observer.Platoon;
            if (platoon == null)
            {
                return;
            }

            platoon.Contacts[target.Id] = tick;
            platoon.LastContactTick = tick;
            if (platoon.Parent != null && platoon.Parent.Level == UnitLevel.Company)
            {
                platoon.Parent.LastContactTick = tick;
            }

            Seen[observer.Side][target.Id] = tick;
            ++DetectionsTotal;
            QueueReport(platoon, target, tick);
        }

        private void QueueReport(Platoon platoon, Element target, long tick)
        {
            InsertionOrderedDictionary<int, ContactReport> pending = _pending[platoon.Side];
            if (pending.TryGetValue(target.Id, out ContactReport? existing))
            {
                existing.Position = target.Pos;
                existing.Tick = tick;
                if (existing.DeliverTick < 0)
                {
                    double retryDelay = RouteDelaySec(platoon);
                    if (retryDelay >= 0.0)
                    {
                        existing.PlatoonId = platoon.Id;
                        existing.DeliverTick = tick + (long)SimMath.Round(retryDelay * SimConst.TickRate);
                    }
                }

                return;
            }

            double delay = RouteDelaySec(platoon);
            pending[target.Id] = new ContactReport
            {
                Position = target.Pos,
                ElementClass = target.Def.ElementClass,
                Tick = tick,
                PlatoonId = platoon.Id,
                DeliverTick = delay >= 0.0 ? tick + (long)SimMath.Round(delay * SimConst.TickRate) : -1
            };
        }

        private void ProcessReports(long tick)
        {
            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                InsertionOrderedDictionary<int, ContactReport> pending = _pending[side];
                if (pending.Count == 0 || taskForce == null)
                {
                    continue;
                }

                List<int> delivered = new List<int>();
                foreach (KeyValuePair<int, ContactReport> pair in pending)
                {
                    int targetId = pair.Key;
                    ContactReport report = pair.Value;
                    if (report.DeliverTick < 0)
                    {
                        if (tick % ReportRetryInterval == 0)
                        {
                            Platoon? platoon = _battle.GetUnit(report.PlatoonId) as Platoon;
                            double delay = platoon != null && !platoon.IsDestroyed() ? RouteDelaySec(platoon) : -1.0;
                            if (delay >= 0.0)
                            {
                                report.DeliverTick = tick + (long)SimMath.Round(delay * SimConst.TickRate);
                            }
                        }

                        continue;
                    }

                    if (tick < report.DeliverTick)
                    {
                        continue;
                    }

                    delivered.Add(targetId);
                    if (!taskForce.KnownEnemies.TryGetValue(targetId, out Dictionary<string, object>? old) ||
                        report.Tick >= ReadTick(old))
                    {
                        taskForce.KnownEnemies[targetId] = new Dictionary<string, object>
                        {
                            ["pos"] = report.Position,
                            ["tick"] = report.Tick,
                            ["class"] = report.ElementClass
                        };
                    }

                    ++ReportsDelivered;
                }

                foreach (int targetId in delivered)
                {
                    pending.Remove(targetId);
                }
            }
        }

        private void Expire(long tick)
        {
            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                List<int> expiredKnown = new List<int>();
                foreach (KeyValuePair<int, Dictionary<string, object>> pair in taskForce.KnownEnemies)
                {
                    long informationTick = ReadTick(pair.Value);
                    Element? target = _battle.GetElement(pair.Key);
                    if (tick - informationTick > KnownExpireTicks ||
                        (target != null && !target.IsAlive() && tick - informationTick > 100))
                    {
                        expiredKnown.Add(pair.Key);
                    }
                }

                foreach (int targetId in expiredKnown)
                {
                    taskForce.KnownEnemies.Remove(targetId);
                }

                foreach (Platoon platoon in taskForce.AllPlatoons())
                {
                    List<int> expiredContacts = new List<int>();
                    foreach (KeyValuePair<int, long> pair in platoon.Contacts)
                    {
                        if (tick - pair.Value > ContactMemoryTicks * 3)
                        {
                            expiredContacts.Add(pair.Key);
                        }
                    }

                    foreach (int targetId in expiredContacts)
                    {
                        platoon.Contacts.Remove(targetId);
                    }
                }
            }
        }

        private static long ReadTick(IReadOnlyDictionary<string, object> info)
        {
            if (!info.TryGetValue("tick", out object value))
            {
                return -1;
            }

            switch (value)
            {
                case long integer:
                    return integer;
                case int integer:
                    return integer;
                case double number:
                    return (long)number;
                case float number:
                    return (long)number;
                default:
                    return -1;
            }
        }
    }
}
