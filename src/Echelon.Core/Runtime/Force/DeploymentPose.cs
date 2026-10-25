using System;

namespace Echelon.Core
{
    public readonly struct DeploymentPose : IEquatable<DeploymentPose>
    {
        public double X { get; }
        public double North { get; }
        public double Heading { get; }
        public SimVec2 Position => new SimVec2(X, North);

        public DeploymentPose(double x, double north, double heading)
        {
            X = x;
            North = north;
            Heading = (float)heading;
        }

        public DeploymentPose(SimVec2 position, double heading)
            : this(position.X, position.Y, heading)
        {
        }

        public bool Equals(DeploymentPose other)
        {
            return X.Equals(other.X) && North.Equals(other.North) && Heading.Equals(other.Heading);
        }

        public override bool Equals(object? obj)
        {
            return obj is DeploymentPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ North.GetHashCode();
                return (hash * 397) ^ Heading.GetHashCode();
            }
        }

        public static bool operator ==(DeploymentPose left, DeploymentPose right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DeploymentPose left, DeploymentPose right)
        {
            return !left.Equals(right);
        }
    }
}
