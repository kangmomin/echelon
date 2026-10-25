using System;

namespace Echelon.Core
{
    public readonly struct MapRect : IEquatable<MapRect>
    {
        public SimVec2 Position { get; }
        public SimVec2 Size { get; }
        public SimVec2 End => new SimVec2(Position.X + Size.X, Position.Y + Size.Y);
        public SimVec2 Center => new SimVec2(Position.X + Size.X * 0.5f, Position.Y + Size.Y * 0.5f);

        public MapRect(SimVec2 position, SimVec2 size)
        {
            Position = position;
            Size = size;
        }

        public MapRect(double x, double y, double width, double height)
            : this(new SimVec2(x, y), new SimVec2(width, height))
        {
        }

        public static MapRect FromCorners(SimVec2 first, SimVec2 second)
        {
            float x = Math.Min(first.X, second.X);
            float y = Math.Min(first.Y, second.Y);
            return new MapRect(new SimVec2(x, y),
                new SimVec2(Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y)));
        }

        public MapRect Grow(double amount)
        {
            float delta = (float)amount;
            return new MapRect(
                new SimVec2(Position.X - delta, Position.Y - delta),
                new SimVec2(Size.X + delta * 2.0f, Size.Y + delta * 2.0f));
        }

        public bool Contains(SimVec2 point)
        {
            SimVec2 end = End;
            return point.X >= Position.X && point.Y >= Position.Y && point.X < end.X && point.Y < end.Y;
        }

        public bool Encloses(MapRect other)
        {
            SimVec2 end = End;
            SimVec2 otherEnd = other.End;
            return other.Position.X >= Position.X && other.Position.Y >= Position.Y &&
                   otherEnd.X <= end.X && otherEnd.Y <= end.Y;
        }

        public bool Equals(MapRect other)
        {
            return Position.Equals(other.Position) && Size.Equals(other.Size);
        }

        public override bool Equals(object? obj)
        {
            return obj is MapRect other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Position.GetHashCode() * 397) ^ Size.GetHashCode();
            }
        }
    }
}
