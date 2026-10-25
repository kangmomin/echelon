using UnityEngine;

namespace Echelon.Unity.Presentation.Fx
{
    public static class FxEventType
    {
        public const string Shot = "shot";
        public const string ElementDestroyed = "element_destroyed";
        public const string Shell = "shell";
        public const string IndirectFire = "indirect_fire";
        public const string SamLaunch = "sam_launch";
        public const string MineStrike = "mine_strike";
        public const string CasShotDown = "cas_shot_down";
    }

    public static class FxWeapon
    {
        public const string MG = "MG";
        public const string KE = "KE";
        public const string HEAT = "HEAT";
        public const string HE = "HE";
        public const string ATGM = "ATGM";
        public const string AC = "AC";
        public const string SAM = "SAM";
    }

    public sealed class FxEvent
    {
        public string Type = string.Empty;
        public int Tick = -1;
        public int SourceId = -1;
        public int TargetId = -1;

        // Position is the ground position for pos payloads. Shot uses SourcePosition/TargetPosition for fp/tp.
        public Vector3 Position;
        public Vector3 SourcePosition;
        public Vector3 TargetPosition;

        public string Weapon = FxWeapon.MG;
        public bool Hit;
        public int Asset = 1;
        public bool Smoke;

        public bool SourceVisible = true;
        public bool TargetVisible;
        public bool SourceIsInfantry;
        public bool TargetIsInfantry;
        public bool SourceIsMortar;
        public float SourceAltitude;
        public float TargetAltitude;

        public bool DestroyedIsHelicopter;
        public bool DestroyedHasPlatoon;
        public float DestroyedAltitude;

        public string SourceSide = string.Empty;
        public Color TracerColor = Color.clear;

        internal FxEvent Snapshot()
        {
            return (FxEvent)MemberwiseClone();
        }
    }

    public struct FxSmoke
    {
        public int Id;
        public Vector3 Position;
        public float Radius;
        public int UntilTick;
    }
}
