using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class Platoon : Unit
    {
        public List<Element> Elements { get; set; } = new List<Element>();
        public bool IsHqSection { get; set; }
        public bool Mounted { get; set; } = true;
        public string Loadout { get; set; } = "";
        public bool Attached { get; set; }
        public long LastShelledTick { get; set; } = -1000000;
        public SimVec2 LastShellPos { get; set; } = SimVec2.Zero;
        public (float X, float Y, float Z) LastAssignKey { get; set; } =
            (float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public int LastAssignMembers { get; set; } = -1;
        public InsertionOrderedDictionary<int, long> Contacts { get; set; } = new InsertionOrderedDictionary<int, long>();
        public string EngTask { get; set; } = "";
        public double EngProgress { get; set; }
        public int MinesLeft { get; set; }
        public int PositionsBuilt { get; set; }
        public SimVec2 AirdropTarget { get; set; } = SimVec2.Inf;

        public Platoon()
        {
            Level = UnitLevel.Platoon;
        }

        public override List<Element> AllElements()
        {
            return Elements;
        }

        public override bool IsDestroyed()
        {
            foreach (Element element in Elements)
            {
                if (element.State == ElementState.OK || element.State == ElementState.DAMAGED)
                {
                    return false;
                }
            }

            return true;
        }

        public List<Element> Vehicles()
        {
            List<Element> output = new List<Element>();
            foreach (Element element in Elements)
            {
                if (element.IsVehicle())
                {
                    output.Add(element);
                }
            }

            return output;
        }

        public List<Element> Squads()
        {
            List<Element> output = new List<Element>();
            foreach (Element element in Elements)
            {
                if (element.IsInfantry())
                {
                    output.Add(element);
                }
            }

            return output;
        }

        public bool HasSquads()
        {
            foreach (Element element in Elements)
            {
                if (element.IsInfantry())
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasTransport()
        {
            foreach (Element element in Elements)
            {
                if (element.Def.TransportCapacity > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAirTransport()
        {
            foreach (Element element in Elements)
            {
                if (element.Def.TransportCapacity > 0 && element.IsAir())
                {
                    return true;
                }
            }

            return false;
        }

        public List<Element> FormationMembers()
        {
            List<Element> output = new List<Element>();
            bool dismountedMech = HasSquads() && !Mounted && HasTransport();
            foreach (Element element in Elements)
            {
                if (!element.IsActive() || element.IsMounted() || !element.CanMove())
                {
                    continue;
                }

                if (dismountedMech && element.IsVehicle())
                {
                    continue;
                }

                output.Add(element);
            }

            return output;
        }

        public List<Element> TrailingVehicles()
        {
            List<Element> output = new List<Element>();
            if (!(HasSquads() && !Mounted && HasTransport()))
            {
                return output;
            }

            foreach (Element element in Elements)
            {
                if (element.IsVehicle() && element.IsActive() && element.CanMove())
                {
                    output.Add(element);
                }
            }

            return output;
        }

        public override Element? Leader()
        {
            if (Commander != null && Commander.IsAlive())
            {
                return Commander;
            }

            foreach (Element element in Elements)
            {
                if (element.IsAlive())
                {
                    return element;
                }
            }

            return null;
        }

        public double BaseSpeed()
        {
            double best = double.PositiveInfinity;
            foreach (Element element in FormationMembers())
            {
                best = Math.Min(best, element.BaseSpeedMps());
            }

            return double.IsPositiveInfinity(best) ? 0.0 : best;
        }

        // 고정익 편대는 중대에 속해도 대형을 따르지 않고 편대마다 따로 날린다.
        public bool IsAirWing => Kind == "fixed_wing";

        public override bool FollowsParent()
        {
            return Parent != null && Parent.Level == UnitLevel.Company && !Detached && !Routed && !IsAirWing;
        }

        public Company? Company()
        {
            return Parent != null && Parent.Level == UnitLevel.Company ? Parent as Company : null;
        }
    }
}
