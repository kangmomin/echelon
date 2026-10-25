using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class OrderSystem : SimSystem
    {
        public const double BaseTfToCoSec = 15.0;
        public const double BaseCoToPltSec = 10.0;
        public const double BaseTfToPltBypassSec = 20.0;
        public const double BaseTfToOrganicSec = 15.0;
        public const double BaseExecutionSec = 5.0;
        public const int BlockTimeoutTicks = 1200;
        public const int RetryInterval = 10;
        public const int RebriefInterval = 50;
        public const double CostMission = 1.0;
        public const double CostBypass = 2.0;
        public const double CostFire = 1.0;
        public const double CostSop = 0.5;
        public const double CostFree = 0.0;
        public const double KSuppressed = 1.5;
        public const double KIneffective = 2.0;
        public const double KAdHoc = 1.2;

        private static readonly string[] Sides = { "blue", "red" };

        private readonly ICommandBattle _battle;
        private readonly CommsNet _comms;
        private readonly Dictionary<string, CommandCapacity> _capacity = new Dictionary<string, CommandCapacity>();
        private List<Order> _active = new List<Order>();

        public OrderSystem(ICommandBattle battle, CommsNet comms)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _comms = comms ?? throw new ArgumentNullException(nameof(comms));
            foreach (string side in Sides)
            {
                _capacity[side] = new CommandCapacity();
            }
        }

        public List<float> DelaySamples { get; } = new List<float>();
        public int CancelledCount { get; private set; }

        public override string GetSystemName()
        {
            return "orders";
        }

        public override long StateHash()
        {
            long hash = SimHash.Append(SimHash.Initial, _active.Count);
            hash = SimHash.Append(hash, DelaySamples.Count);
            return SimHash.Append(hash, CancelledCount);
        }

        public void Submit(Order order)
        {
            Unit? unit = _battle.GetUnit(order.TargetUnitId);
            TaskForce? taskForce = unit?.TaskForce();
            if (unit == null || taskForce == null)
            {
                order.State = OrderState.CANCELLED;
                return;
            }

            if (order.IssuerUnitId < 0)
            {
                order.IssuerUnitId = taskForce.Id;
            }

            if (order.Issuer != "unit")
            {
                order.Bypass = IsBypass(order, unit);
                order.Cost = CostOf(order, unit);
            }

            CommandCapacity capacity = _capacity[taskForce.Side];
            capacity.EnsureCapacity(CapacityMax(taskForce.Side));
            if (!capacity.Submit(order, CapacityMax(taskForce.Side)))
            {
                _battle.World.Events.EmitEvent("order_waiting_capacity", _battle.World.Tick, new Dictionary<string, object>
                {
                    ["order"] = order.Id,
                    ["side"] = taskForce.Side
                });
                return;
            }

            Start(order, unit);
        }

        public void SubmitSubOrder(Order order, Unit fromUnit)
        {
            order.Issuer = "unit";
            order.IssuerUnitId = fromUnit.Id;
            order.Cost = CostFree;
            _battle.RegisterOrder(order);
            Unit? unit = _battle.GetUnit(order.TargetUnitId);
            if (unit == null)
            {
                order.State = OrderState.CANCELLED;
                return;
            }

            Start(order, unit);
        }

        public double CostOf(Order order, Unit unit)
        {
            switch (order.Type)
            {
                case OrderType.SET_SOP:
                case OrderType.SET_FORMATION:
                case OrderType.HALT:
                case OrderType.DETACH:
                case OrderType.REJOIN:
                case OrderType.MOUNT:
                case OrderType.DISMOUNT:
                    return CostSop;
                case OrderType.CANCEL:
                    return CostFree;
                case OrderType.CALL_FIRE:
                    return CostFire;
            }

            return IsBypass(order, unit) ? CostBypass : CostMission;
        }

        public static bool IsBypass(Order order, Unit unit)
        {
            return order.Bypass || (order.IsMovement() && unit is Platoon && unit.FollowsParent() && order.Issuer != "unit");
        }

        public List<Order> PendingForUnit(int unitId)
        {
            List<Order> output = new List<Order>();
            foreach (Order order in _active)
            {
                if (order.TargetUnitId == unitId && order.Issuer != "unit")
                {
                    output.Add(order);
                }
            }

            foreach (string side in Sides)
            {
                foreach (Order order in _capacity[side].Queue)
                {
                    if (order.TargetUnitId == unitId)
                    {
                        output.Add(order);
                    }
                }
            }

            return output;
        }

        public int ActiveCount()
        {
            return _active.Count;
        }

        public int CapacityQueueSize(string side)
        {
            return _capacity[side].QueueCount;
        }

        public bool TrySpend(string side, double amount)
        {
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce == null)
            {
                return false;
            }

            return _capacity[side].TrySpend(CapacityMax(side), amount);
        }

        public double CapacityNow(string side)
        {
            double maximum = CapacityMax(side);
            return _capacity.TryGetValue(side, out CommandCapacity? capacity) ? capacity.CapacityNow(maximum) : maximum;
        }

        public double CapacityMax(string side)
        {
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce == null)
            {
                return 0.0;
            }

            return taskForce.CommandCapacityPerMin * (taskForce.Leader() == null || taskForce.CpLost ? 0.5 : 1.0);
        }

        public double EstimateDelaySec(Unit unit, OrderType orderType, bool bypass)
        {
            Order probe = Order.Make(orderType, unit.Id);
            probe.Bypass = bypass;
            List<OrderHop> hops = PlanHops(probe, unit);
            double total = 0.0;
            foreach (OrderHop hop in hops)
            {
                double estimate = HopSeconds(hop);
                if (estimate < 0.0)
                {
                    return -1.0;
                }

                total += estimate;
            }

            TaskForce? taskForce = unit.TaskForce();
            if (taskForce != null)
            {
                CommandCapacity capacity = _capacity[taskForce.Side];
                double maximum = CapacityMax(taskForce.Side);
                capacity.EnsureCapacity(maximum);
                double needed = CostOf(probe, unit) - capacity.RawTokens;
                if (needed > 0.0)
                {
                    total += needed / (maximum / 60.0);
                }
            }

            return total;
        }

        public override void Step(SimWorld world)
        {
            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                double maximum = CapacityMax(side);
                List<Order> ready = _capacity[side].RefillAndDequeue(maximum, SimConst.TickDt);
                foreach (Order order in ready)
                {
                    Unit? unit = _battle.GetUnit(order.TargetUnitId);
                    if (unit != null)
                    {
                        Start(order, unit);
                    }
                }
            }

            List<Order> stillActive = new List<Order>();
            for (int i = 0; i < _active.Count; ++i)
            {
                Order order = _active[i];
                if (Advance(order, world.Tick))
                {
                    stillActive.Add(order);
                }
            }

            _active = stillActive;
            if (world.Tick % RebriefInterval == 0)
            {
                RebriefOrphans();
            }
        }

        public static long SecondsToTicks(double seconds)
        {
            return (long)SimMath.Round(seconds * SimConst.TickRate);
        }

        private void RebriefOrphans()
        {
            HashSet<int> pending = new HashSet<int>();
            foreach (Order order in _active)
            {
                if (order.Type == OrderType.FOLLOW)
                {
                    pending.Add(order.TargetUnitId);
                }
            }

            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                foreach (Company company in taskForce.Companies)
                {
                    foreach (Unit platoon in company.ChildrenUnits())
                    {
                        if (platoon.AwaitingFollow && !pending.Contains(platoon.Id) && !platoon.IsDestroyed())
                        {
                            SubmitSubOrder(Order.Make(OrderType.FOLLOW, platoon.Id), company);
                        }
                    }
                }
            }
        }

        private void Start(Order order, Unit unit)
        {
            order.State = OrderState.PROPAGATING;
            order.Hops = PlanHops(order, unit);
            order.HopIndex = 0;
            order.BlockedSinceTick = -1;
            _active.Add(order);
        }

        private List<OrderHop> PlanHops(Order order, Unit unit)
        {
            List<OrderHop> hops = new List<OrderHop>();
            Unit? issuer = order.IssuerUnitId >= 0 ? _battle.GetUnit(order.IssuerUnitId) : unit.TaskForce();
            issuer ??= unit.TaskForce();
            if (issuer == null)
            {
                throw new InvalidOperationException("An order requires an issuing unit or task force.");
            }

            if (issuer != unit)
            {
                if (issuer.Level == UnitLevel.TaskForce && unit.Level == UnitLevel.Platoon)
                {
                    Platoon platoon = (Platoon)unit;
                    if (IsBypass(order, unit))
                    {
                        hops.Add(Hop(issuer, unit, BaseTfToPltBypassSec));
                    }
                    else if (platoon.Parent != null && platoon.Parent.Level == UnitLevel.Company)
                    {
                        hops.Add(Hop(issuer, platoon.Parent, BaseTfToCoSec));
                        hops.Add(Hop(platoon.Parent, unit, BaseCoToPltSec));
                    }
                    else
                    {
                        hops.Add(Hop(issuer, unit, BaseTfToOrganicSec));
                    }
                }
                else if (issuer.Level == UnitLevel.TaskForce && unit.Level == UnitLevel.Company)
                {
                    hops.Add(Hop(issuer, unit, BaseTfToCoSec));
                }
                else if (issuer.Level == UnitLevel.Company && unit.Level == UnitLevel.Platoon)
                {
                    hops.Add(Hop(issuer, unit, BaseCoToPltSec));
                }
                else
                {
                    hops.Add(Hop(issuer, unit, BaseCoToPltSec));
                }
            }

            if (unit.Level == UnitLevel.Platoon && NeedsExecutionDelay(order))
            {
                hops.Add(Hop(unit, unit, BaseExecutionSec));
            }

            return hops;
        }

        private static bool NeedsExecutionDelay(Order order)
        {
            return order.IsMovement() || order.Type == OrderType.FOLLOW || order.Type == OrderType.MOUNT ||
                order.Type == OrderType.DISMOUNT;
        }

        private static OrderHop Hop(Unit fromUnit, Unit toUnit, double baseSeconds)
        {
            return new OrderHop
            {
                FromUnitId = fromUnit.Id,
                ToUnitId = toUnit.Id,
                BaseSeconds = baseSeconds
            };
        }

        private bool Advance(Order order, long tick)
        {
            if (order.State == OrderState.CANCELLED)
            {
                return false;
            }

            while (order.HopIndex < order.Hops.Count)
            {
                OrderHop hop = order.Hops[order.HopIndex];
                if (hop.StartTick < 0)
                {
                    if (!TryStartHop(order, hop, tick))
                    {
                        if (order.BlockedSinceTick >= 0 && tick - order.BlockedSinceTick > BlockTimeoutTicks)
                        {
                            Cancel(order, "통신 두절");
                            return false;
                        }

                        return true;
                    }

                    continue;
                }

                if (tick < hop.ArriveTick)
                {
                    return true;
                }

                order.HopIndex++;
            }

            Deliver(order, tick);
            return false;
        }

        private bool TryStartHop(Order order, OrderHop hop, long tick)
        {
            if (order.BlockedSinceTick >= 0 && (tick - order.BlockedSinceTick) % RetryInterval != 0)
            {
                return false;
            }

            double seconds = HopSeconds(hop);
            if (seconds < 0.0)
            {
                if (order.BlockedSinceTick < 0)
                {
                    order.BlockedSinceTick = tick;
                    _battle.World.Events.EmitEvent("order_blocked", tick, new Dictionary<string, object>
                    {
                        ["order"] = order.Id,
                        ["unit"] = order.TargetUnitId
                    });
                }

                return false;
            }

            order.BlockedSinceTick = -1;
            hop.StartTick = tick;
            hop.ArriveTick = tick + SecondsToTicks(seconds);
            return true;
        }

        private double HopSeconds(OrderHop hop)
        {
            Unit? fromUnit = _battle.GetUnit(hop.FromUnitId);
            Unit? toUnit = _battle.GetUnit(hop.ToUnitId);
            if (fromUnit == null || toUnit == null || toUnit.IsDestroyed())
            {
                return -1.0;
            }

            long tick = _battle.World.Tick;
            if (toUnit.IsLeaderless(tick) || fromUnit.IsLeaderless(tick) || toUnit.Routed)
            {
                return -1.0;
            }

            double commsFactor = 1.0;
            int relays = 0;
            if (fromUnit != toUnit)
            {
                CommsLink link = _comms.LinkBetween(fromUnit, toUnit);
                if (!link.Ok)
                {
                    return -1.0;
                }

                commsFactor = CommsNet.DelayFactor(link.Quality);
                relays = link.Relays;
            }

            hop.Relays = relays;
            return hop.BaseSeconds * commsFactor * toUnit.SkillDelayFactor() * StateFactor(toUnit) +
                relays * CommsNet.RelayDelaySec;
        }

        private static double StateFactor(Unit unit)
        {
            double factor = 1.0;
            if (AverageSuppression(unit) >= 50.0)
            {
                factor *= KSuppressed;
            }

            if (unit.Level != UnitLevel.TaskForce && unit.IsCombatIneffective())
            {
                factor *= KIneffective;
            }

            if (unit.AdHoc)
            {
                factor *= KAdHoc;
            }

            return factor;
        }

        private static double AverageSuppression(Unit unit)
        {
            double total = 0.0;
            int count = 0;
            foreach (Element element in unit.AliveElements())
            {
                total += element.Suppression;
                count++;
            }

            return count > 0 ? total / count : 0.0;
        }

        private void Deliver(Order order, long tick)
        {
            Unit? unit = _battle.GetUnit(order.TargetUnitId);
            if (unit == null || unit.IsDestroyed())
            {
                order.State = OrderState.CANCELLED;
                return;
            }

            order.State = OrderState.RECEIVED;
            order.ReceivedTick = tick;
            double delay = (tick - order.TickIssued) / (double)SimConst.TickRate;
            if (order.Issuer != "unit")
            {
                DelaySamples.Add((float)delay);
            }

            _battle.World.Events.EmitEvent("order_received", tick, new Dictionary<string, object>
            {
                ["order"] = order.Id,
                ["unit"] = unit.Id,
                ["type"] = order.TypeName(),
                ["side"] = unit.Side,
                ["issuer"] = order.Issuer,
                ["delay"] = delay
            });
            _battle.Executor.Execute(unit, order);
        }

        private void Cancel(Order order, string reason)
        {
            order.State = OrderState.CANCELLED;
            CancelledCount++;
            _battle.World.Events.EmitEvent("order_cancelled", _battle.World.Tick, new Dictionary<string, object>
            {
                ["order"] = order.Id,
                ["unit"] = order.TargetUnitId,
                ["reason"] = reason
            });
        }
    }
}
