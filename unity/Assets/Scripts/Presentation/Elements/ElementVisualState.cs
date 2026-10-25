using System;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements
{
    public enum ElementVisualCondition
    {
        Ok = 0,
        Damaged = 1,
        Destroyed = 2,
        Abandoned = 3
    }

    [Serializable]
    public sealed class ElementVisualState
    {
        public const int NoTick = -1000000;
        public const int NoTarget = -1;

        public int Id;
        public string ElementId = string.Empty;
        public string Side = string.Empty;
        public string ElementClass = string.Empty;
        public string Loadout = string.Empty;

        public Vector2 PreviousPosition;
        public Vector2 Position;
        public float PreviousHeading;
        public float Heading;
        public Vector2 Velocity;

        public float Hp = 100.0f;
        public ElementVisualCondition State = ElementVisualCondition.Ok;
        public bool IsMounted;
        public bool OnMap = true;

        public int TargetId = NoTarget;
        public float Altitude;
        public float TerrainHeight;
        public Vector3 TerrainNormal = Vector3.up;

        public int LastFiredTick = NoTick;
        public int LastSeenTick = NoTick;

        public bool IsAlive
        {
            get { return State == ElementVisualCondition.Ok || State == ElementVisualCondition.Damaged; }
        }

        public bool IsInfantry
        {
            get { return string.Equals(ElementClass, "inf_squad", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsHelicopter
        {
            get { return string.Equals(ElementClass, "heli", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsPlane
        {
            get { return string.Equals(ElementClass, "plane", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsAircraft
        {
            get { return IsHelicopter || IsPlane; }
        }

        public bool IsVehicle
        {
            get { return !IsInfantry; }
        }

        public ElementVisualState Snapshot()
        {
            var snapshot = new ElementVisualState();
            snapshot.CopyFrom(this);
            return snapshot;
        }

        public void CopyFrom(ElementVisualState other)
        {
            if (other == null)
                throw new ArgumentNullException("other");

            Id = other.Id;
            ElementId = other.ElementId;
            Side = other.Side;
            ElementClass = other.ElementClass;
            Loadout = other.Loadout;
            PreviousPosition = other.PreviousPosition;
            Position = other.Position;
            PreviousHeading = other.PreviousHeading;
            Heading = other.Heading;
            Velocity = other.Velocity;
            Hp = other.Hp;
            State = other.State;
            IsMounted = other.IsMounted;
            OnMap = other.OnMap;
            TargetId = other.TargetId;
            Altitude = other.Altitude;
            TerrainHeight = other.TerrainHeight;
            TerrainNormal = other.TerrainNormal;
            LastFiredTick = other.LastFiredTick;
            LastSeenTick = other.LastSeenTick;
        }
    }
}
