using System;
using System.Collections.Generic;
using Echelon.Unity.Presentation.Symbols;
using UnityEngine;

namespace Echelon.Unity.UI.Minimap
{
    public enum MinimapTerrainType
    {
        Open = 0,
        Road = 1,
        Forest = 2,
        Urban = 3,
        Water = 4
    }

    public readonly struct MinimapTerrainSample
    {
        public readonly MinimapTerrainType Type;
        public readonly float Height;

        public MinimapTerrainSample(MinimapTerrainType type, float height)
        {
            Type = type;
            Height = height;
        }
    }

    [Serializable]
    public sealed class MinimapContactSnapshot
    {
        public string Side;
        public Vector2 Position;
        public bool OnMap = true;
        public bool IsMounted;
        public bool IsAlive = true;
        public SymbolContactState Contact = SymbolContactState.Current;
    }

    [Serializable]
    public sealed class MinimapCameraSnapshot
    {
        public bool Visible = true;
        public Vector2 Focus;
        public float Distance = 1400f;
    }

    [Serializable]
    public sealed class MinimapSnapshot
    {
        public string PlayerSide = "blue";
        public bool FogEnabled = true;
        public Vector2 MapSize = new Vector2(4000f, 4000f);
        public List<SymbolControlPointSnapshot> ControlPoints = new List<SymbolControlPointSnapshot>();
        public List<MinimapContactSnapshot> Contacts = new List<MinimapContactSnapshot>();
        public MinimapCameraSnapshot Camera = new MinimapCameraSnapshot();
    }
}
