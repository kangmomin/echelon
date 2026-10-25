using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class MoraleSystem : SimSystem, ICombatMorale
    {
        public const double SuppressionDecay = 2.0;
        public const double SuppressionDecayLed = 3.0;
        public const double LossPlatoon = 15.0;
        public const double LossCompany = 3.0;
        public const double DamagePlatoon = 5.0;
        public const double CasualtyPerMan = 1.5;
        public const double CommanderLoss = 10.0;
        public const double CommsCutPerSec = 0.5;
        public const double CommsCutFloor = 30.0;
        public const double RecoveryPerSec = 0.3;
        public const int RecoveryQuietTicks = 300;
        public const double KillBonus = 3.0;
        public const double Shaken = 40.0;
        public const double Rout = 20.0;
        public const int RoutTicks = 600;
        public const double AfterRout = 30.0;
        public const int UpdateInterval = 10;

        private static readonly string[] Sides = { "blue", "red" };
        private readonly ICommandBattle _battle;

        public MoraleSystem(ICommandBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public override string GetSystemName()
        {
            return "morale";
        }

        public override long StateHash()
        {
            long hash = SimHash.Initial;
            foreach (Unit unit in _battle.Units)
            {
                hash = SimHash.Append(hash, (long)SimMath.Round(unit.Morale * 10.0));
            }

            return hash;
        }

        public override void Step(SimWorld world)
        {
            double dt = SimConst.TickDt;
            foreach (Element element in _battle.Elements)
            {
                if (element.Suppression <= 0.0 || !element.IsAlive())
                {
                    continue;
                }

                bool led = element.Platoon != null && element.Platoon.Leader() != null;
                double decay = led ? SuppressionDecayLed : SuppressionDecay;
                element.Suppression = Math.Max(0.0, element.Suppression - decay * dt);
            }

            if (world.Tick % UpdateInterval != 0)
            {
                return;
            }

            double seconds = UpdateInterval * dt;
            foreach (string side in Sides)
            {
                TaskForce? taskForce = _battle.GetTaskForce(side);
                if (taskForce == null)
                {
                    continue;
                }

                foreach (Platoon platoon in taskForce.AllPlatoons())
                {
                    if (platoon.IsDestroyed() || !platoon.OnMap)
                    {
                        continue;
                    }

                    if (platoon.CommsState == Unit.Comms.Cut && platoon.Morale > CommsCutFloor)
                    {
                        platoon.Morale = Math.Max(CommsCutFloor,
                            platoon.Morale - CommsCutPerSec * seconds);
                    }
                    else if (world.Tick - platoon.LastContactTick > RecoveryQuietTicks &&
                        world.Tick - platoon.LastHitTick > RecoveryQuietTicks)
                    {
                        platoon.Morale = Math.Min(100.0, platoon.Morale + RecoveryPerSec * seconds);
                    }

                    CheckRout(platoon, world.Tick);
                }

                foreach (Company company in taskForce.Companies)
                {
                    company.Morale = AverageMorale(company);
                }
            }
        }

        public void OnElementLost(Element element)
        {
            Platoon? platoon = element.Platoon;
            if (platoon == null)
            {
                return;
            }

            platoon.Morale = Math.Max(0.0, platoon.Morale - LossPlatoon);
            Company? company = platoon.Company();
            if (company != null)
            {
                foreach (Platoon other in company.Platoons)
                {
                    if (other != platoon)
                    {
                        other.Morale = Math.Max(0.0, other.Morale - LossCompany);
                    }
                }
            }

            Element? killer = FindElement(element.KilledBy);
            if (killer != null && killer.Platoon != null)
            {
                killer.Platoon.Morale = Math.Min(100.0, killer.Platoon.Morale + KillBonus);
            }
        }

        public void OnElementDamaged(Element element)
        {
            if (element.Platoon != null)
            {
                element.Platoon.Morale = Math.Max(0.0, element.Platoon.Morale - DamagePlatoon);
            }
        }

        public void OnCasualties(Platoon? platoon, int men)
        {
            if (platoon != null)
            {
                platoon.Morale = Math.Max(0.0, platoon.Morale - CasualtyPerMan * men);
            }
        }

        public void OnCommanderLost(Unit unit)
        {
            if (unit is Platoon)
            {
                unit.Morale = Math.Max(0.0, unit.Morale - CommanderLoss);
            }
            else if (unit is Company company)
            {
                foreach (Platoon platoon in company.Platoons)
                {
                    platoon.Morale = Math.Max(0.0, platoon.Morale - CommanderLoss);
                }
            }
        }

        public static bool IsShaken(Unit unit)
        {
            return unit.Morale <= Shaken;
        }

        private static double AverageMorale(Company company)
        {
            double total = 0.0;
            int count = 0;
            foreach (Platoon platoon in company.Platoons)
            {
                if (!platoon.IsDestroyed())
                {
                    total += platoon.Morale;
                    ++count;
                }
            }

            return count > 0 ? total / count : 0.0;
        }

        private void CheckRout(Platoon platoon, long tick)
        {
            if (platoon.Routed)
            {
                if (tick >= platoon.RoutedUntilTick)
                {
                    platoon.Routed = false;
                    platoon.Morale = Math.Max(platoon.Morale, AfterRout);
                    platoon.Posture = "idle";
                    _battle.World.Events.EmitEvent("rally", tick, new Dictionary<string, object>
                    {
                        ["unit"] = platoon.Id,
                        ["side"] = platoon.Side
                    });
                }

                return;
            }

            if (platoon.Morale <= Rout)
            {
                platoon.Routed = true;
                platoon.RoutedUntilTick = tick + RoutTicks;
                platoon.Posture = "routed";
                _battle.World.Events.EmitEvent("routed", tick, new Dictionary<string, object>
                {
                    ["unit"] = platoon.Id,
                    ["side"] = platoon.Side
                });
            }
        }

        private Element? FindElement(int elementId)
        {
            foreach (Element element in _battle.Elements)
            {
                if (element.Id == elementId)
                {
                    return element;
                }
            }

            return null;
        }
    }
}
