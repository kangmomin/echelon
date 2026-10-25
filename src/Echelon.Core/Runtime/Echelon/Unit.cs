using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public enum UnitLevel
    {
        TaskForce,
        Company,
        Platoon
    }

    public class Unit
    {
        public enum Comms
        {
            Good,
            Poor,
            Cut
        }

        public static readonly string[] LevelNames = { "대대 전투단", "중대", "소대" };
        public static readonly string[] CommsNames = { "양호", "불량", "두절" };
        public static readonly double NoFacing = BitConverter.Int64BitsToDouble(unchecked((long)0x7ff8000000000000UL));
        public static readonly Dictionary<string, double> SkillDelay = new Dictionary<string, double>
        {
            ["recruit"] = 1.3,
            ["regular"] = 1.0,
            ["veteran"] = 0.8,
            ["elite"] = 0.65
        };

        public int Id { get; set; }
        public string DisplayName { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string Side { get; set; } = "blue";
        public UnitLevel Level { get; set; } = UnitLevel.Platoon;
        public Unit? Parent { get; set; }
        public string TemplateId { get; set; } = "";
        public string Kind { get; set; } = "";
        public Guide Guide { get; set; } = new Guide();
        public FormationDef? Formation { get; set; }
        public Sop Sop { get; set; } = new Sop();
        public string Skill { get; set; } = "regular";
        public Order? Mission { get; set; }
        public List<Order> QueuedOrders { get; set; } = new List<Order>();
        public bool Detached { get; set; }
        public Element? Commander { get; set; }
        public long SuccessionUntilTick { get; set; } = -1;
        public double SuccessionDelayMult { get; set; } = 1.0;
        public Element? PendingSuccessor { get; set; }
        public bool AdHoc { get; set; }
        public bool AwaitingFollow { get; set; }
        public double Morale { get; set; } = 100.0;
        public double CommsQuality { get; set; } = 1.0;
        public Comms CommsState { get; set; } = Comms.Good;
        public int CommsHops { get; set; } = 1;
        public long ReformUntilTick { get; set; } = -1;
        public bool OnMap { get; set; } = true;
        public int Phase { get; set; }
        public string Role { get; set; } = "";
        public string Posture { get; set; } = "idle";
        public double DefendFacing { get; set; } = NoFacing;
        public long RoutedUntilTick { get; set; } = -1;
        public int InitialElementCount { get; set; }
        public long LastContactTick { get; set; } = -1000000;
        public long LastHitTick { get; set; } = -1000000;
        public long ContactHaltUntil { get; set; } = -1;
        public long NextHaltTick { get; set; } = -1;
        public long NextDisperseTick { get; set; }
        public bool Routed { get; set; }
        public bool AutoWithdrawn { get; set; }
        public double FormationError { get; set; }
        public bool ColumnMode { get; set; }
        public double ColumnHoldM { get; set; }
        public long SettlingSinceTick { get; set; } = -1;
        public double GuideSpeedLimit { get; set; }

        public virtual List<Unit> ChildrenUnits()
        {
            return new List<Unit>();
        }

        public virtual Element? Leader()
        {
            return Commander != null && Commander.IsAlive() ? Commander : null;
        }

        public virtual List<Element> AllElements()
        {
            return new List<Element>();
        }

        public List<Element> AliveElements()
        {
            List<Element> output = new List<Element>();
            foreach (Element element in AllElements())
            {
                if (element.IsAlive())
                {
                    output.Add(element);
                }
            }

            return output;
        }

        public List<Element> ActiveElements()
        {
            List<Element> output = new List<Element>();
            foreach (Element element in AllElements())
            {
                if (element.IsActive())
                {
                    output.Add(element);
                }
            }

            return output;
        }

        public double Strength()
        {
            List<Element> elements = AllElements();
            if (elements.Count == 0)
            {
                return 0.0;
            }

            double total = 0.0;
            foreach (Element element in elements)
            {
                total += element.Strength();
            }

            int denominator = Math.Max(Math.Max(InitialElementCount, elements.Count), 1);
            return total / denominator;
        }

        public virtual bool IsDestroyed()
        {
            foreach (Element element in AllElements())
            {
                if (element.State == ElementState.OK || element.State == ElementState.DAMAGED)
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsCombatIneffective()
        {
            return Strength() <= 0.5;
        }

        public SimVec2 Centroid()
        {
            List<Element> elements = ActiveElements();
            if (elements.Count == 0)
            {
                return Guide.Pos;
            }

            SimVec2 sum = SimVec2.Zero;
            foreach (Element element in elements)
            {
                sum += element.Pos;
            }

            return sum / elements.Count;
        }

        public double SkillDelayFactor()
        {
            double factor = SkillDelay.TryGetValue(Skill, out double value) ? value : 1.0;
            return factor * SuccessionDelayMult;
        }

        public bool IsReforming(long tick)
        {
            return tick < ReformUntilTick;
        }

        public bool IsLeaderless(long tick)
        {
            return tick < SuccessionUntilTick;
        }

        public bool IsRouted(long tick)
        {
            return tick < RoutedUntilTick;
        }

        public string LevelName()
        {
            return LevelNames[(int)Level];
        }

        public string CommsName()
        {
            return CommsNames[(int)CommsState];
        }

        public virtual bool FollowsParent()
        {
            return false;
        }

        public TaskForce? TaskForce()
        {
            Unit? unit = this;
            while (unit != null && unit.Level != UnitLevel.TaskForce)
            {
                unit = unit.Parent;
            }

            return unit as TaskForce;
        }

        public string Label()
        {
            return ShortName.Length > 0 ? ShortName : DisplayName;
        }
    }
}
