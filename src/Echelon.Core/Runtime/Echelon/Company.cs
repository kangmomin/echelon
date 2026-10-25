using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class Company : Unit
    {
        public List<Platoon> Platoons { get; set; } = new List<Platoon>();
        public Platoon? Hq { get; set; }
        public Element? Xo { get; set; }

        public Company()
        {
            Level = UnitLevel.Company;
        }

        public override List<Unit> ChildrenUnits()
        {
            List<Unit> output = new List<Unit>();
            if (Hq != null)
            {
                output.Add(Hq);
            }

            output.AddRange(Platoons);
            return output;
        }

        public override List<Element> AllElements()
        {
            List<Element> output = new List<Element>();
            if (Hq != null)
            {
                output.AddRange(Hq.Elements);
            }

            foreach (Platoon platoon in Platoons)
            {
                output.AddRange(platoon.Elements);
            }

            return output;
        }

        public override bool IsDestroyed()
        {
            if (Hq != null && !Hq.IsDestroyed())
            {
                return false;
            }

            foreach (Platoon platoon in Platoons)
            {
                if (!platoon.IsDestroyed())
                {
                    return false;
                }
            }

            return true;
        }

        // 보급·포병·방공·헬기·고정익 중대는 거점을 잡는 기동 중대가 아니다(D-050).
        public bool IsSupport => Kind == "supply" || Kind == "artillery" || Kind == "aa" || Kind == "aviation" ||
            Kind == "fixed_wing";

        public List<Platoon> FormationPlatoons()
        {
            List<Platoon> output = new List<Platoon>();
            foreach (Platoon platoon in Platoons)
            {
                if (platoon.Parent == this && !platoon.Detached && platoon.OnMap && !platoon.IsDestroyed() &&
                    !platoon.IsAirWing)
                {
                    output.Add(platoon);
                }
            }

            return output;
        }

        public override Element? Leader()
        {
            return Commander != null && Commander.IsAlive() ? Commander : null;
        }

        public double BaseSpeed()
        {
            double best = double.PositiveInfinity;
            foreach (Platoon platoon in FormationPlatoons())
            {
                double speed = platoon.BaseSpeed();
                if (speed > 0.0)
                {
                    best = Math.Min(best, speed);
                }
            }

            if (Hq != null && !Hq.IsDestroyed())
            {
                double speed = Hq.BaseSpeed();
                if (speed > 0.0)
                {
                    best = Math.Min(best, speed);
                }
            }

            return double.IsPositiveInfinity(best) ? 0.0 : best;
        }
    }
}
