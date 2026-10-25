using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class TaskForce : Unit
    {
        public List<Company> Companies { get; set; } = new List<Company>();
        public List<Platoon> Organic { get; set; } = new List<Platoon>();
        public Platoon? CpSection { get; set; }
        public TOETemplate? Toe { get; set; }
        public string Faction { get; set; } = "US";
        public InsertionOrderedDictionary<int, Dictionary<string, object>> KnownEnemies { get; set; } =
            new InsertionOrderedDictionary<int, Dictionary<string, object>>();
        public double Vp { get; set; }
        public double CommandCapacityPerMin { get; set; } = 6.0;
        public bool CpLost { get; set; }
        public int ArtilleryMissions { get; set; }
        public int CasSorties { get; set; }
        public SimVec2 EntryPos { get; set; } = SimVec2.Zero;

        public TaskForce()
        {
            Level = UnitLevel.TaskForce;
        }

        public override List<Unit> ChildrenUnits()
        {
            List<Unit> output = new List<Unit>();
            if (CpSection != null)
            {
                output.Add(CpSection);
            }

            foreach (Company company in Companies)
            {
                output.Add(company);
            }

            foreach (Platoon platoon in Organic)
            {
                output.Add(platoon);
            }

            return output;
        }

        public List<Unit> AllUnits()
        {
            List<Unit> output = new List<Unit> { this };
            if (CpSection != null)
            {
                output.Add(CpSection);
            }

            foreach (Company company in Companies)
            {
                output.Add(company);
                if (company.Hq != null)
                {
                    output.Add(company.Hq);
                }

                foreach (Platoon platoon in company.Platoons)
                {
                    output.Add(platoon);
                }
            }

            foreach (Platoon platoon in Organic)
            {
                output.Add(platoon);
            }

            return output;
        }

        public List<Platoon> AllPlatoons()
        {
            List<Platoon> output = new List<Platoon>();
            foreach (Unit unit in AllUnits())
            {
                if (unit.Level == UnitLevel.Platoon)
                {
                    output.Add((Platoon)unit);
                }
            }

            return output;
        }

        public override List<Element> AllElements()
        {
            List<Element> output = new List<Element>();
            foreach (Platoon platoon in AllPlatoons())
            {
                output.AddRange(platoon.Elements);
            }

            return output;
        }

        public override Element? Leader()
        {
            return Commander != null && Commander.IsAlive() ? Commander : null;
        }
    }
}
