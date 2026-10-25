using System;
using System.Globalization;

namespace Echelon.Core
{
    public readonly struct SimVec2 : IEquatable<SimVec2>
    {
        public static readonly SimVec2 Zero = new SimVec2(0.0f, 0.0f);
        public static readonly SimVec2 One = new SimVec2(1.0f, 1.0f);
        public static readonly SimVec2 Inf = new SimVec2(float.PositiveInfinity, float.PositiveInfinity);
        public static readonly SimVec2 Left = new SimVec2(-1.0f, 0.0f);
        public static readonly SimVec2 Right = new SimVec2(1.0f, 0.0f);
        public static readonly SimVec2 Up = new SimVec2(0.0f, -1.0f);
        public static readonly SimVec2 Down = new SimVec2(0.0f, 1.0f);

        public readonly float X;
        public readonly float Y;

        public SimVec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public SimVec2(double x, double y)
            : this((float)x, (float)y)
        {
        }

        public float this[int axis]
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

        public static SimVec2 FromAngle(double angle)
        {
            float radians = (float)angle;
            return new SimVec2(MathF.Cos(radians), MathF.Sin(radians));
        }

        public double Length()
        {
            return LengthSingle();
        }

        public double LengthSquared()
        {
            return LengthSquaredSingle();
        }

        public SimVec2 Normalized()
        {
            if (!IsFinite())
            {
                return Zero;
            }

            float lengthSquared = LengthSquaredSingle();
            if (lengthSquared == 0.0f)
            {
                return Zero;
            }

            float length = MathF.Sqrt(lengthSquared);
            return new SimVec2(X / length, Y / length);
        }

        public double DistanceTo(SimVec2 other)
        {
            return DistanceSingle(other);
        }

        public double DistanceSquaredTo(SimVec2 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        public double Dot(SimVec2 other)
        {
            return DotSingle(other);
        }

        public double Cross(SimVec2 other)
        {
            return CrossSingle(other);
        }

        public SimVec2 DirectionTo(SimVec2 other)
        {
            return new SimVec2(other.X - X, other.Y - Y).Normalized();
        }

        public double Angle()
        {
            return MathF.Atan2(Y, X);
        }

        public double AngleTo(SimVec2 other)
        {
            return MathF.Atan2(CrossSingle(other), DotSingle(other));
        }

        public double AngleToPoint(SimVec2 other)
        {
            return (other - this).Angle();
        }

        public double Aspect()
        {
            return X / Y;
        }

        public SimVec2 Lerp(SimVec2 to, double weight)
        {
            float amount = (float)weight;
            return new SimVec2(SimMath.Lerp(X, to.X, amount), SimMath.Lerp(Y, to.Y, amount));
        }

        public SimVec2 Rotated(double angle)
        {
            float radians = (float)angle;
            float sine = MathF.Sin(radians);
            float cosine = MathF.Cos(radians);
            return new SimVec2(X * cosine - Y * sine, X * sine + Y * cosine);
        }

        public SimVec2 MoveToward(SimVec2 to, double delta)
        {
            float amount = (float)delta;
            SimVec2 difference = to - this;
            float length = difference.LengthSingle();
            return length <= amount || length < (float)SimMath.CmpEpsilon
                ? to
                : this + difference / length * amount;
        }

        public SimVec2 LimitLength(double length = 1.0)
        {
            float limit = (float)length;
            float currentLength = LengthSingle();
            if (currentLength > 0.0f && limit < currentLength)
            {
                return this / currentLength * limit;
            }

            return this;
        }

        public SimVec2 Project(SimVec2 to)
        {
            return to * (DotSingle(to) / to.LengthSquaredSingle());
        }

        public SimVec2 Slide(SimVec2 normal)
        {
            return this - normal * DotSingle(normal);
        }

        public SimVec2 Bounce(SimVec2 normal)
        {
            return -Reflect(normal);
        }

        public SimVec2 Reflect(SimVec2 normal)
        {
            return 2.0f * normal * DotSingle(normal) - this;
        }

        public SimVec2 PosMod(double mod)
        {
            float divisor = (float)mod;
            return new SimVec2(PositiveModulo(X, divisor), PositiveModulo(Y, divisor));
        }

        public SimVec2 PosMod(SimVec2 mod)
        {
            return new SimVec2(PositiveModulo(X, mod.X), PositiveModulo(Y, mod.Y));
        }

        public SimVec2 Clamp(SimVec2 min, SimVec2 max)
        {
            return new SimVec2(SimMath.Clamp(X, min.X, max.X), SimMath.Clamp(Y, min.Y, max.Y));
        }

        public SimVec2 Clamp(double min, double max)
        {
            float minimum = (float)min;
            float maximum = (float)max;
            return new SimVec2(SimMath.Clamp(X, minimum, maximum), SimMath.Clamp(Y, minimum, maximum));
        }

        public SimVec2 Min(SimVec2 other)
        {
            return new SimVec2(X < other.X ? X : other.X, Y < other.Y ? Y : other.Y);
        }

        public SimVec2 Max(SimVec2 other)
        {
            return new SimVec2(X > other.X ? X : other.X, Y > other.Y ? Y : other.Y);
        }

        public SimVec2 Abs()
        {
            return new SimVec2(Math.Abs(X), Math.Abs(Y));
        }

        public SimVec2 Sign()
        {
            return new SimVec2(X > 0.0f ? 1.0f : X < 0.0f ? -1.0f : 0.0f, Y > 0.0f ? 1.0f : Y < 0.0f ? -1.0f : 0.0f);
        }

        public SimVec2 Floor()
        {
            return new SimVec2(MathF.Floor(X), MathF.Floor(Y));
        }

        public SimVec2 Ceiling()
        {
            return new SimVec2(MathF.Ceiling(X), MathF.Ceiling(Y));
        }

        public SimVec2 Rounded()
        {
            return new SimVec2(SimMath.Round(X), SimMath.Round(Y));
        }

        public SimVec2 Snapped(SimVec2 step)
        {
            return new SimVec2(SimMath.Snap(X, step.X), SimMath.Snap(Y, step.Y));
        }

        public SimVec2 Snapped(double step)
        {
            float amount = (float)step;
            return new SimVec2(SimMath.Snap(X, amount), SimMath.Snap(Y, amount));
        }

        public bool IsZeroApprox()
        {
            return SimMath.IsZeroApprox(X) && SimMath.IsZeroApprox(Y);
        }

        public bool IsEqualApprox(SimVec2 other)
        {
            return SimMath.IsEqualApprox(X, other.X) && SimMath.IsEqualApprox(Y, other.Y);
        }

        public bool IsFinite()
        {
            return IsFiniteComponent(X) && IsFiniteComponent(Y);
        }

        public bool Equals(SimVec2 other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is SimVec2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:R}, {1:R})", X, Y);
        }

        public void Deconstruct(out float x, out float y)
        {
            x = X;
            y = Y;
        }

        private float LengthSingle()
        {
            return MathF.Sqrt(LengthSquaredSingle());
        }

        private float LengthSquaredSingle()
        {
            return X * X + Y * Y;
        }

        private float DistanceSingle(SimVec2 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private float DotSingle(SimVec2 other)
        {
            return X * other.X + Y * other.Y;
        }

        private float CrossSingle(SimVec2 other)
        {
            return X * other.Y - Y * other.X;
        }

        private static bool IsFiniteComponent(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float PositiveModulo(float value, float modulus)
        {
            float result = value % modulus;
            if ((result < 0.0f && modulus > 0.0f) || (result > 0.0f && modulus < 0.0f))
            {
                result += modulus;
            }

            return result + 0.0f;
        }

        public static SimVec2 operator +(SimVec2 left, SimVec2 right)
        {
            return new SimVec2(left.X + right.X, left.Y + right.Y);
        }

        public static SimVec2 operator -(SimVec2 left, SimVec2 right)
        {
            return new SimVec2(left.X - right.X, left.Y - right.Y);
        }

        public static SimVec2 operator -(SimVec2 value)
        {
            return new SimVec2(-value.X, -value.Y);
        }

        public static SimVec2 operator *(SimVec2 left, SimVec2 right)
        {
            return new SimVec2(left.X * right.X, left.Y * right.Y);
        }

        public static SimVec2 operator *(SimVec2 value, float scalar)
        {
            return new SimVec2(value.X * scalar, value.Y * scalar);
        }

        public static SimVec2 operator *(SimVec2 value, double scalar)
        {
            return value * (float)scalar;
        }

        public static SimVec2 operator *(float scalar, SimVec2 value)
        {
            return value * scalar;
        }

        public static SimVec2 operator *(double scalar, SimVec2 value)
        {
            return value * scalar;
        }

        public static SimVec2 operator /(SimVec2 left, SimVec2 right)
        {
            return new SimVec2(left.X / right.X, left.Y / right.Y);
        }

        public static SimVec2 operator /(SimVec2 value, float scalar)
        {
            return new SimVec2(value.X / scalar, value.Y / scalar);
        }

        public static SimVec2 operator /(SimVec2 value, double scalar)
        {
            return value / (float)scalar;
        }

        public static bool operator ==(SimVec2 left, SimVec2 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SimVec2 left, SimVec2 right)
        {
            return !left.Equals(right);
        }

        public static explicit operator SimVec2I(SimVec2 value)
        {
            return new SimVec2I((int)value.X, (int)value.Y);
        }
    }
}
