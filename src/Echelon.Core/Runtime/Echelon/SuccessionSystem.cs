using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class SuccessionSystem : SimSystem
    {
        public const int CheckInterval = 10;
        public const int XoMinTicks = 600;
        public const int XoMaxTicks = 900;
        public const int SeniorMult = 2;
        public const double SeniorDelayMult = 1.5;
        public const int PlatoonTicks = 200;
        public const int TfTicks = 600;

        private static readonly string[] Sides = { "blue", "red" };
        private readonly ICommandBattle _battle;

        public SuccessionSystem(ICommandBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public override string GetSystemName()
        {
            return "succession";
        }

        public override void Step(SimWorld world)
        {
            if (world.Tick % CheckInterval != 0)
            {
                return;
            }

            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                foreach (Unit unit in taskForce.AllUnits())
                {
                    if (unit.IsDestroyed() && unit.Level != UnitLevel.TaskForce)
                    {
                        continue;
                    }

                    Check(unit, world.Tick);
                }
            }
        }

        public override long StateHash()
        {
            long hash = SimHash.Initial;
            foreach (Unit unit in _battle.Units)
            {
                hash = SimHash.Append(hash, unit.SuccessionUntilTick);
                hash = SimHash.Append(hash, unit.Commander?.Id ?? -1);
            }

            return hash;
        }

        private void Check(Unit unit, long tick)
        {
            if (unit.SuccessionUntilTick >= 0)
            {
                if (tick < unit.SuccessionUntilTick)
                {
                    return;
                }

                Element? successor = unit.PendingSuccessor;
                unit.SuccessionUntilTick = -1;
                unit.PendingSuccessor = null;
                if (successor != null && successor.IsAlive())
                {
                    unit.Commander = successor;
                    _battle.World.Events.EmitEvent("succession_done", tick, new Dictionary<string, object>
                    {
                        ["unit"] = unit.Id,
                        ["side"] = unit.Side,
                        ["commander"] = successor.Id
                    });
                    return;
                }
            }

            if (unit.Commander != null && unit.Commander.IsAlive())
            {
                return;
            }

            Element? nextSuccessor = null;
            int wait = 0;
            switch (unit.Level)
            {
                case UnitLevel.Platoon:
                    nextSuccessor = FirstAlive(((Platoon)unit).Elements);
                    wait = PlatoonTicks;
                    break;
                case UnitLevel.Company:
                    Company company = (Company)unit;
                    if (company.Xo != null && company.Xo.IsAlive() && company.Xo != company.Commander)
                    {
                        nextSuccessor = company.Xo;
                        wait = _battle.World.Rng.NextInt(XoMinTicks, XoMaxTicks);
                    }
                    else
                    {
                        foreach (Platoon platoon in company.Platoons)
                        {
                            Element? leader = platoon.Leader();
                            if (leader != null)
                            {
                                nextSuccessor = leader;
                                break;
                            }
                        }

                        wait = _battle.World.Rng.NextInt(XoMinTicks, XoMaxTicks) * SeniorMult;
                        unit.SuccessionDelayMult = SeniorDelayMult;
                    }

                    break;
                case UnitLevel.TaskForce:
                    TaskForce taskForce = (TaskForce)unit;
                    taskForce.CpLost = true;
                    foreach (Company taskForceCompany in taskForce.Companies)
                    {
                        Element? leader = taskForceCompany.Leader();
                        if (leader != null)
                        {
                            nextSuccessor = leader;
                            break;
                        }
                    }

                    if (nextSuccessor == null)
                    {
                        nextSuccessor = FirstAlive(taskForce.AllElements());
                    }

                    wait = TfTicks;
                    break;
            }

            if (nextSuccessor == null)
            {
                return;
            }

            int lostId = unit.Commander?.Id ?? -1;
            _battle.Morale?.OnCommanderLost(unit);
            unit.Commander = null;
            unit.PendingSuccessor = nextSuccessor;
            unit.SuccessionUntilTick = tick + wait;
            _battle.World.Events.EmitEvent("commander_lost", tick, new Dictionary<string, object>
            {
                ["unit"] = unit.Id,
                ["side"] = unit.Side,
                ["element"] = lostId,
                ["seconds"] = wait / (double)SimConst.TickRate
            });
        }

        private static Element? FirstAlive(IEnumerable<Element> elements)
        {
            foreach (Element element in elements)
            {
                if (element.IsAlive())
                {
                    return element;
                }
            }

            return null;
        }
    }
}
