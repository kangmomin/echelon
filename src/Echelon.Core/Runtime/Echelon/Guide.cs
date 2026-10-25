using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class Guide
    {
        public const double TrailSpacingM = 10.0;
        public const int TrailMaxPoints = 300;
        public const double TurnRate = Math.PI / 2.0;
        public const double ArriveEpsM = 2.0;

        public SimVec2 Pos { get; set; } = SimVec2.Zero;
        public double Heading { get; set; }
        public List<SimVec2> Path { get; set; } = new List<SimVec2>();
        public int PathIndex { get; set; }
        public double Speed { get; set; }
        public double FinalFacing { get; set; } = Unit.NoFacing;
        public List<SimVec2> Trail { get; set; } = new List<SimVec2>();
        public GridPathfinder.PathRequest? Pending { get; set; }
        public SimVec2 Destination { get; set; } = SimVec2.Zero;
        public bool HasDestination { get; set; }

        public void Reset(SimVec2 position, double heading)
        {
            Pos = position;
            Heading = heading;
            Path = new List<SimVec2>();
            PathIndex = 0;
            Speed = 0.0;
            Trail = new List<SimVec2> { position };
            Pending = null;
            HasDestination = false;
        }

        public void SetPath(List<SimVec2> points)
        {
            Path = points;
            PathIndex = points.Count > 1 ? 1 : points.Count;
            if (points.Count > 0)
            {
                Destination = points[points.Count - 1];
                HasDestination = true;
            }
        }

        public void SetPath(SimVec2[] points)
        {
            SetPath(new List<SimVec2>(points));
        }

        public void ClearPath()
        {
            Path = new List<SimVec2>();
            PathIndex = 0;
            if (Pending != null)
            {
                Pending.Cancelled = true;
            }

            Pending = null;
        }

        public bool IsMoving()
        {
            return PathIndex < Path.Count;
        }

        public bool IsWaitingForPath()
        {
            return Pending != null && !Pending.Done;
        }

        public double RemainingDistance()
        {
            if (!IsMoving())
            {
                return 0.0;
            }

            double distance = Pos.DistanceTo(Path[PathIndex]);
            for (int i = PathIndex; i < Path.Count - 1; ++i)
            {
                distance += Path[i].DistanceTo(Path[i + 1]);
            }

            return distance;
        }

        public double Advance(double dt, double maxSpeed)
        {
            Speed = 0.0;
            if (!IsMoving() || maxSpeed <= 0.0)
            {
                return 0.0;
            }

            double budget = maxSpeed * dt;
            double moved = 0.0;
            while (budget > 0.0 && PathIndex < Path.Count)
            {
                SimVec2 target = Path[PathIndex];
                SimVec2 toTarget = target - Pos;
                double distance = toTarget.Length();
                if (distance <= ArriveEpsM)
                {
                    Pos = target;
                    ++PathIndex;
                    continue;
                }

                TurnTowardCore(HeadingFromVector(toTarget), dt);
                double step = Math.Min(budget, distance);
                Pos += toTarget / distance * step;
                budget -= step;
                moved += step;
            }

            Speed = moved / dt;
            RecordTrail();
            return moved;
        }

        public double MoveToward(SimVec2 target, double dt, double maxSpeed, double face)
        {
            SimVec2 toTarget = target - Pos;
            double distance = toTarget.Length();
            Speed = 0.0;
            if (distance <= ArriveEpsM)
            {
                Pos = distance > 0.0 ? target : Pos;
                if (!double.IsNaN(face))
                {
                    TurnTowardCore(face, dt);
                }

                RecordTrail();
                return 0.0;
            }

            double step = Math.Min(maxSpeed * dt, distance);
            if (distance > 30.0)
            {
                TurnTowardCore(HeadingFromVector(toTarget), dt);
            }
            else if (!double.IsNaN(face))
            {
                TurnTowardCore(face, dt);
            }

            Pos += toTarget / distance * step;
            Speed = step / dt;
            RecordTrail();
            return step;
        }

        public void TurnToward(double targetHeading, double dt)
        {
            TurnTowardCore(targetHeading, dt);
        }

        public SimVec2 TrailPointBehind(double dist)
        {
            double remaining = dist;
            SimVec2 current = Pos;
            int index = Trail.Count - 1;
            while (index >= 0 && remaining > 0.0)
            {
                SimVec2 point = Trail[index];
                double segment = current.DistanceTo(point);
                if (segment >= remaining)
                {
                    return segment > 0.0 ? current.Lerp(point, remaining / segment) : point;
                }

                remaining -= segment;
                current = point;
                --index;
            }

            return current - ForwardVector(Heading) * remaining;
        }

        public List<SimVec2> PointsAhead(double lookAheadM, double stepM = 10.0)
        {
            List<SimVec2> output = new List<SimVec2>();
            if (!IsMoving())
            {
                return output;
            }

            SimVec2 current = Pos;
            double left = lookAheadM;
            int index = PathIndex;
            while (left > 0.0 && index < Path.Count)
            {
                SimVec2 next = Path[index];
                double segment = current.DistanceTo(next);
                double distance = 0.0;
                while (distance < segment && left > 0.0)
                {
                    output.Add(current.Lerp(next, distance / segment));
                    distance += stepM;
                    left -= stepM;
                }

                current = next;
                ++index;
            }

            return output;
        }

        private void TurnTowardCore(double targetHeading, double dt)
        {
            double difference = SimMath.AngleDifference(Heading, targetHeading);
            double maximumTurn = TurnRate * dt;
            Heading = SimMath.Wrap(Heading + SimMath.Clamp(difference, -maximumTurn, maximumTurn), -Math.PI, Math.PI);
        }

        private void RecordTrail()
        {
            if (Trail.Count == 0 || Trail[Trail.Count - 1].DistanceTo(Pos) >= TrailSpacingM)
            {
                Trail.Add(Pos);
                if (Trail.Count > TrailMaxPoints)
                {
                    Trail = Trail.GetRange(Trail.Count - TrailMaxPoints, TrailMaxPoints);
                }
            }
        }

        private static double HeadingFromVector(SimVec2 direction)
        {
            return Math.Atan2(direction.X, direction.Y);
        }

        private static SimVec2 ForwardVector(double heading)
        {
            return new SimVec2(Math.Sin(heading), Math.Cos(heading));
        }
    }
}
