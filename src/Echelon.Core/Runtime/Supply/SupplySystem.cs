using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class SupplySystem : SimSystem, IOrderSupply
    {
        public const int Interval = 10;
        public const double TransferPerMin = 20.0;
        public const double RefillPerMin = 100.0;
        public const double ContactM = 150.0;
        public const double RefillRadiusM = 300.0;
        public const double RepairRadiusM = 150.0;
        public const double RepairSec = 300.0;
        public const int RemoveSec = 10;

        private readonly ISupplyBattle _battle;
        private readonly List<int> _taskOrder = new List<int>();
        private readonly List<int> _returningOrder = new List<int>();

        public SupplySystem(ISupplyBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public List<int> Requests { get; private set; } = new List<int>();
        public InsertionOrderedDictionary<int, int> Tasks { get; } = new InsertionOrderedDictionary<int, int>();
        public InsertionOrderedDictionary<int, bool> Returning { get; } = new InsertionOrderedDictionary<int, bool>();
        public double DeliveredTotal { get; private set; }

        public override string GetSystemName()
        {
            return "supply";
        }

        public override long StateHash()
        {
            long hash = SimHash.Initial;
            hash = SimHash.Append(hash, Requests.Count);
            foreach (int unitId in Requests)
            {
                hash = SimHash.Append(hash, unitId);
            }

            hash = SimHash.Append(hash, _taskOrder.Count);
            foreach (int sectionId in _taskOrder)
            {
                hash = SimHash.Append(hash, sectionId);
                hash = SimHash.Append(hash, Tasks[sectionId]);
            }

            hash = SimHash.Append(hash, (long)SimMath.Round(DeliveredTotal));
            foreach (Element element in _battle.Elements)
            {
                if (element.Def.SupplyCapacity > 0)
                {
                    hash = SimHash.Append(hash, (long)SimMath.Round(element.SupplyLeft));
                }
            }

            return hash;
        }

        public void Request(Platoon platoon)
        {
            if (platoon.IsDestroyed() || platoon.Kind == "supply" || platoon.IsAirWing)
            {
                return;
            }

            platoon.Posture = "resupplying";
            platoon.Guide.ClearPath();
            if (!Requests.Contains(platoon.Id) && !Tasks.ContainsValue(platoon.Id))
            {
                Requests.Add(platoon.Id);
            }

            _battle.World.Events.EmitEvent("resupply_requested", _battle.World.Tick, new Dictionary<string, object>
            {
                ["unit"] = platoon.Id,
                ["side"] = platoon.Side
            });
        }

        public static double ElementNeed(Element element)
        {
            if (element.Def.SupplyFull <= 0 || !element.IsAlive())
            {
                return 0.0;
            }

            double missing = 0.0;
            int count = 0;
            foreach (KeyValuePair<string, int> pair in element.Def.Ammo)
            {
                int full = pair.Value;
                if (full <= 0)
                {
                    continue;
                }

                int current = element.Ammo.TryGetValue(pair.Key, out int ammo) ? ammo : 0;
                missing += SimMath.Clamp(1.0 - current / (double)full, 0.0, 1.0);
                ++count;
            }

            return count > 0 ? element.Def.SupplyFull * (missing / count) : 0.0;
        }

        public static double AmmoRatio(Unit unit)
        {
            double full = 0.0;
            double need = 0.0;
            foreach (Element element in unit.AliveElements())
            {
                full += element.Def.SupplyFull;
                need += ElementNeed(element);
            }

            return full > 0.0 ? 1.0 - need / full : 1.0;
        }

        public static double PlatoonNeed(Platoon platoon)
        {
            double total = 0.0;
            foreach (Element element in platoon.Elements)
            {
                total += ElementNeed(element);
            }

            return total;
        }

        public static double Give(Element element, double units)
        {
            double need = ElementNeed(element);
            if (need <= 0.0 || units <= 0.0)
            {
                return 0.0;
            }

            double used = Math.Min(need, units);
            double fraction = used / element.Def.SupplyFull;
            foreach (KeyValuePair<string, int> pair in element.Def.Ammo)
            {
                int full = pair.Value;
                int current = element.Ammo.TryGetValue(pair.Key, out int ammo) ? ammo : 0;
                if (current >= full)
                {
                    continue;
                }

                int add = Math.Max(1, (int)Math.Ceiling(full * fraction));
                element.Ammo[pair.Key] = Math.Min(full, current + add);
            }

            return used;
        }

        public static double SectionSupply(Platoon platoon)
        {
            double total = 0.0;
            foreach (Element element in platoon.AliveElements())
            {
                total += element.SupplyLeft;
            }

            return total;
        }

        public static double SectionCapacity(Platoon platoon)
        {
            double total = 0.0;
            foreach (Element element in platoon.AliveElements())
            {
                total += element.Def.SupplyCapacity;
            }

            return total;
        }

        public override void Step(SimWorld world)
        {
            if (world.Tick % Interval != 0)
            {
                return;
            }

            double seconds = Interval * SimConst.TickDt;
            Assign(world.Tick);
            int[] taskIds = _taskOrder.ToArray();
            foreach (int sectionId in taskIds)
            {
                if (Tasks.TryGetValue(sectionId, out int platoonId))
                {
                    Work(sectionId, platoonId, world.Tick, seconds);
                }
            }

            int[] returningIds = _returningOrder.ToArray();
            foreach (int sectionId in returningIds)
            {
                if (Returning.ContainsKey(sectionId))
                {
                    Refill(sectionId, world.Tick, seconds);
                }
            }

            Repair(world.Tick, seconds);
        }

        private List<Platoon> Sections(string side)
        {
            List<Platoon> output = new List<Platoon>();
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce == null)
            {
                return output;
            }

            foreach (Platoon platoon in taskForce.AllPlatoons())
            {
                if (platoon.Kind == "supply" && platoon.OnMap && !platoon.IsDestroyed())
                {
                    output.Add(platoon);
                }
            }

            return output;
        }

        private void Assign(long tick)
        {
            List<int> still = new List<int>();
            foreach (int platoonId in Requests)
            {
                Platoon? platoon = _battle.GetUnit(platoonId) as Platoon;
                if (platoon == null || platoon.IsDestroyed() || platoon.Posture != "resupplying")
                {
                    continue;
                }

                Platoon? best = null;
                double bestDistance = double.PositiveInfinity;
                foreach (Platoon section in Sections(platoon.Side))
                {
                    if (Tasks.ContainsKey(section.Id) || Returning.ContainsKey(section.Id) || SectionSupply(section) < 1.0)
                    {
                        continue;
                    }

                    double distance = section.Centroid().DistanceTo(platoon.Centroid());
                    if (distance < bestDistance)
                    {
                        best = section;
                        bestDistance = distance;
                    }
                }

                if (best == null)
                {
                    still.Add(platoonId);
                    continue;
                }

                AddTask(best.Id, platoonId);
                Send(best, platoon);
                _battle.World.Events.EmitEvent("supply_dispatched", tick, new Dictionary<string, object>
                {
                    ["unit"] = best.Id,
                    ["target"] = platoonId,
                    ["side"] = platoon.Side
                });
            }

            Requests = still;
        }

        private void Send(Platoon section, Platoon platoon)
        {
            // 보급 중대의 반은 중대 대형에 묶여 있으면 출발하지 못한다. 보급하러 갈 때 중대에서 떨어진다.
            if (section.FollowsParent())
            {
                section.Detached = true;
            }

            _battle.SelfOrder(section, OrderType.MOVE,
                platoon.Centroid() - FormationSolver.ForwardVector(platoon.Guide.Heading) * 60.0);
        }

        private void Work(int sectionId, int platoonId, long tick, double seconds)
        {
            Platoon? section = _battle.GetUnit(sectionId) as Platoon;
            Platoon? platoon = _battle.GetUnit(platoonId) as Platoon;
            if (section == null || section.IsDestroyed())
            {
                RemoveTask(sectionId);
                if (platoon != null && !platoon.IsDestroyed() && platoon.Posture == "resupplying")
                {
                    Requests.Add(platoonId);
                }

                return;
            }

            if (platoon == null || platoon.IsDestroyed() || platoon.Posture != "resupplying")
            {
                RemoveTask(sectionId);
                return;
            }

            double distance = section.Centroid().DistanceTo(platoon.Centroid());
            if (distance > ContactM)
            {
                if (!section.Guide.IsMoving() && !section.Guide.IsWaitingForPath() &&
                    tick % (RemoveSec * SimConst.TickRate) == 0)
                {
                    Send(section, platoon);
                }

                return;
            }

            double budget = Math.Min(TransferPerMin / 60.0 * seconds, SectionSupply(section));
            double usedTotal = 0.0;
            foreach (Element element in platoon.Elements)
            {
                if (budget - usedTotal <= 0.0)
                {
                    break;
                }

                usedTotal += Give(element, budget - usedTotal);
            }

            Take(section, usedTotal);
            DeliveredTotal += usedTotal;
            if (PlatoonNeed(platoon) <= 0.01)
            {
                platoon.Posture = "idle";
                RemoveTask(sectionId);
                _battle.World.Events.EmitEvent("resupplied", tick, new Dictionary<string, object>
                {
                    ["unit"] = platoonId,
                    ["side"] = platoon.Side,
                    ["by"] = sectionId
                });
                if (SectionSupply(section) < SectionCapacity(section) * 0.25)
                {
                    GoRefill(section);
                }
            }
            else if (SectionSupply(section) < 1.0)
            {
                RemoveTask(sectionId);
                Requests.Add(platoonId);
                GoRefill(section);
            }
        }

        private static void Take(Platoon section, double amount)
        {
            double left = amount;
            foreach (Element element in section.AliveElements())
            {
                double taken = Math.Min(element.SupplyLeft, left);
                element.SupplyLeft -= taken;
                left -= taken;
                if (left <= 0.0)
                {
                    return;
                }
            }
        }

        private void GoRefill(Platoon section)
        {
            TaskForce? taskForce = section.TaskForce();
            if (taskForce == null)
            {
                return;
            }

            AddReturning(section.Id);
            _battle.SelfOrder(section, OrderType.MOVE, taskForce.EntryPos);
            _battle.World.Events.EmitEvent("supply_returning", _battle.World.Tick, new Dictionary<string, object>
            {
                ["unit"] = section.Id,
                ["side"] = section.Side
            });
        }

        private void Refill(int sectionId, long tick, double seconds)
        {
            Platoon? section = _battle.GetUnit(sectionId) as Platoon;
            if (section == null || section.IsDestroyed())
            {
                RemoveReturning(sectionId);
                return;
            }

            TaskForce? taskForce = section.TaskForce();
            if (taskForce == null || section.Centroid().DistanceTo(taskForce.EntryPos) > RefillRadiusM)
            {
                return;
            }

            double add = RefillPerMin / 60.0 * seconds;
            foreach (Element element in section.AliveElements())
            {
                double room = element.Def.SupplyCapacity - element.SupplyLeft;
                double transferred = Math.Min(room, add);
                element.SupplyLeft += transferred;
                add -= transferred;
            }

            if (SectionSupply(section) >= SectionCapacity(section) - 0.01)
            {
                RemoveReturning(sectionId);
                _battle.World.Events.EmitEvent("supply_refilled", tick, new Dictionary<string, object>
                {
                    ["unit"] = sectionId,
                    ["side"] = section.Side
                });
            }
        }

        private void Repair(long tick, double seconds)
        {
            foreach (Element element in _battle.Elements)
            {
                if (element.State != ElementState.DAMAGED || !element.IsActive() || element.Velocity.Length() > 0.5)
                {
                    continue;
                }

                bool near = false;
                foreach (Element other in _battle.ElementsNear(element.Pos, RepairRadiusM))
                {
                    if (other.Side == element.Side && other.Def.SupplyCapacity > 0 && other.IsActive() &&
                        other.Velocity.Length() < 0.5)
                    {
                        near = true;
                        break;
                    }
                }

                if (!near)
                {
                    continue;
                }

                element.RepairSec += seconds;
                if (element.RepairSec >= RepairSec)
                {
                    element.State = ElementState.OK;
                    element.DamageKind = "";
                    element.RepairSec = 0.0;
                    _battle.World.Events.EmitEvent("repaired", tick, new Dictionary<string, object>
                    {
                        ["element"] = element.Id,
                        ["side"] = element.Side,
                        ["unit"] = element.Platoon?.Id ?? -1
                    });
                }
            }
        }

        private void AddTask(int sectionId, int platoonId)
        {
            if (!Tasks.ContainsKey(sectionId))
            {
                _taskOrder.Add(sectionId);
            }

            Tasks[sectionId] = platoonId;
        }

        private void RemoveTask(int sectionId)
        {
            if (Tasks.Remove(sectionId))
            {
                _taskOrder.Remove(sectionId);
            }
        }

        private void AddReturning(int sectionId)
        {
            if (!Returning.ContainsKey(sectionId))
            {
                _returningOrder.Add(sectionId);
            }

            Returning[sectionId] = true;
        }

        private void RemoveReturning(int sectionId)
        {
            if (Returning.Remove(sectionId))
            {
                _returningOrder.Remove(sectionId);
            }
        }
    }
}
