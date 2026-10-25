using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Symbols
{
    public enum SymbolAggregationLevel
    {
        Element = 0,
        Platoon = 1,
        Company = 2
    }

    public enum SymbolUnitLevel
    {
        TaskForce = 0,
        Company = 1,
        Platoon = 2
    }

    public enum SymbolCommsState
    {
        Good = 0,
        Poor = 1,
        Cut = 2
    }

    public enum SymbolContactState
    {
        Hidden = 0,
        Current = 1,
        Reported = 2,
        Stale = 3
    }

    [Serializable]
    public sealed class SymbolElementSnapshot
    {
        public string Id;
        public bool OnMap = true;
        public bool IsMounted;
        public bool IsAlive = true;
        public bool IsInfantry;
        public bool IsLeader;
        public Vector2 Position;
        public Vector2 Forward = Vector2.up;
        public float Altitude;
        public SymbolContactState Contact = SymbolContactState.Current;
    }

    [Serializable]
    public sealed class SymbolMovementSnapshot
    {
        public List<Vector2> Path = new List<Vector2>();
        public List<Vector2> FormationGhosts = new List<Vector2>();
        public bool IsDefending;
        public Vector2 DefendDirection;
    }

    [Serializable]
    public sealed class SymbolPendingOrderSnapshot
    {
        public Vector2 TargetPosition;
        public string TypeName = string.Empty;
        public float RemainingSeconds;
        public bool WaitingForComms;
    }

    [Serializable]
    public sealed class SymbolUnitSnapshot
    {
        public string Id;
        public string ParentId;
        public string Side;
        public string Branch = string.Empty;
        public string Label = string.Empty;
        public SymbolUnitLevel Level = SymbolUnitLevel.Platoon;
        public bool OnMap = true;
        public bool IsDestroyed;
        public bool Detached;
        public bool FollowsParent;
        public Vector2 Position;
        public Vector2 GuidePosition;
        public float Altitude;
        public float Strength = 1f;
        public SymbolCommsState Comms = SymbolCommsState.Good;
        public SymbolContactState Contact = SymbolContactState.Current;
        public List<SymbolElementSnapshot> Elements = new List<SymbolElementSnapshot>();
        public SymbolMovementSnapshot Movement = new SymbolMovementSnapshot();
        public List<SymbolPendingOrderSnapshot> PendingOrders = new List<SymbolPendingOrderSnapshot>();
    }

    [Serializable]
    public sealed class SymbolControlPointSnapshot
    {
        public string Id;
        public string DisplayName = string.Empty;
        public Vector2 Position;
        public float RadiusMeters;
        public string OwnerSide = string.Empty;
        public bool Contested;
    }

    [Serializable]
    public sealed class SymbolMinefieldSnapshot
    {
        public string Side;
        public Vector2 Position;
        public float RadiusMeters;
        public int Charges;
    }

    [Serializable]
    public sealed class SymbolFortificationSnapshot
    {
        public string Side;
        public Vector2 Position;
        public float RadiusMeters;
    }

    [Serializable]
    public sealed class SymbolDeploymentSnapshot
    {
        public Rect Zone;
        public List<Vector2> EntryPoints = new List<Vector2>();
    }

    [Serializable]
    public sealed class BattlefieldSymbolSnapshot
    {
        public string PlayerSide = "blue";
        public bool FogEnabled = true;
        public bool IsDeploying;
        public Vector2 MapSize = new Vector2(4000f, 4000f);
        public List<SymbolUnitSnapshot> Units = new List<SymbolUnitSnapshot>();
        public List<string> SelectedUnitIds = new List<string>();
        public List<SymbolControlPointSnapshot> ControlPoints = new List<SymbolControlPointSnapshot>();
        public List<SymbolMinefieldSnapshot> KnownMinefields = new List<SymbolMinefieldSnapshot>();
        public List<SymbolFortificationSnapshot> Fortifications = new List<SymbolFortificationSnapshot>();
        public SymbolDeploymentSnapshot Deployment = new SymbolDeploymentSnapshot();
    }

    public readonly struct SymbolPickResult
    {
        public readonly string UnitId;
        public readonly string ParentId;
        public readonly string Side;
        public readonly Vector2 ScreenPosition;
        public readonly float DistancePixels;

        public SymbolPickResult(
            string unitId,
            string parentId,
            string side,
            Vector2 screenPosition,
            float distancePixels)
        {
            UnitId = unitId;
            ParentId = parentId;
            Side = side;
            ScreenPosition = screenPosition;
            DistancePixels = distancePixels;
        }
    }
}
