using System;
using System.Globalization;

namespace Echelon.Core
{
    public readonly struct SimVec2I : IEquatable<SimVec2I>
    {
        public static readonly SimVec2I Zero = new SimVec2I(0, 0);
        public static readonly SimVec2I One = new SimVec2I(1, 1);
        public static readonly SimVec2I Left = new SimVec2I(-1, 0);
        public static readonly SimVec2I Right = new SimVec2I(1, 0);
        public static readonly SimVec2I Up = new SimVec2I(0, -1);
        public static readonly SimVec2I Down = new SimVec2I(0, 1);

        public readonly int X;
        public readonly int Y;

        public SimVec2I(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int this[int axis]
        {
            get
            {
                if (axis == 0)
                {
                    return X;
                }

                if (axis == 1)
                {
                    return Y;
                }

                throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        public double Length()
        {
            return Math.Sqrt(LengthSquared());
        }

        public long LengthSquared()
        {
            return (long)X * X + (long)Y * Y;
        }

        public double DistanceTo(SimVec2I other)
        {
            return Math.Sqrt(DistanceSquaredTo(other));
        }

        public long DistanceSquaredTo(SimVec2I other)
        {
            long dx = (long)X - other.X;
            long dy = (long)Y - other.Y;
            return dx * dx + dy * dy;
        }

        public long Dot(SimVec2I other)
        {
            return (long)X * other.X + (long)Y * other.Y;
        }

        public long Cross(SimVec2I other)
        {
            return (long)X * other.Y - (long)Y * other.X;
        }

        public double Aspect()
        {
            return (double)X / Y;
        }

        public SimVec2I Abs()
        {
            return new SimVec2I(Math.Abs(X), Math.Abs(Y));
        }

        public SimVec2I Sign()
        {
            return new SimVec2I(X > 0 ? 1 : X < 0 ? -1 : 0, Y > 0 ? 1 : Y < 0 ? -1 : 0);
        }

        public SimVec2I Clamp(SimVec2I min, SimVec2I max)
        {
            return new SimVec2I(SimMath.Clamp(X, min.X, max.X), SimMath.Clamp(Y, min.Y, max.Y));
        }

        public SimVec2 ToSimVec2()
        {
            return new SimVec2(X, Y);
        }

        public bool Equals(SimVec2I other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is SimVec2I other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1})", X, Y);
        }

        public void Deconstruct(out int x, out int y)
        {
            x = X;
            y = Y;
        }

        public static SimVec2I operator +(SimVec2I left, SimVec2I right)
        {
            return new SimVec2I(left.X + right.X, left.Y + right.Y);
        }

        public static SimVec2I operator -(SimVec2I left, SimVec2I right)
        {
            return new SimVec2I(left.X - right.X, left.Y - right.Y);
        }

        public static SimVec2I operator -(SimVec2I value)
        {
            return new SimVec2I(-value.X, -value.Y);
        }

        public static SimVec2I operator *(SimVec2I left, SimVec2I right)
        {
            return new SimVec2I(left.X * right.X, left.Y * right.Y);
        }

        public static SimVec2I operator *(SimVec2I value, int scalar)
        {
            return new SimVec2I(value.X * scalar, value.Y * scalar);
        }

        public static SimVec2I operator *(int scalar, SimVec2I value)
        {
            return value * scalar;
        }

        public static SimVec2I operator /(SimVec2I left, SimVec2I right)
        {
            return new SimVec2I(left.X / right.X, left.Y / right.Y);
        }

        public static SimVec2I operator /(SimVec2I value, int scalar)
        {
            return new SimVec2I(value.X / scalar, value.Y / scalar);
        }

        public static bool operator ==(SimVec2I left, SimVec2I right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SimVec2I left, SimVec2I right)
        {
            return !left.Equals(right);
        }

        public static implicit operator SimVec2(SimVec2I value)
        {
            return value.ToSimVec2();
        }
    }
}
