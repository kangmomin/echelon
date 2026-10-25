using System;
using System.Collections.Generic;

namespace Echelon.Unity.UI.Battle
{
    public enum BattleCommandType
    {
        Move = 0,
        FastMove = 1,
        AttackMove = 2,
        Seize = 3,
        Defend = 4,
        Withdraw = 5,
        Resupply = 6,
        Mount = 7,
        Dismount = 8,
        SetFormation = 9,
        SetSop = 10,
        CallFire = 11,
        Detach = 12,
        Rejoin = 13,
        Reorganize = 14,
        Cancel = 15,
        Follow = 16,
        Halt = 17,
        LayMines = 18,
        ClearMines = 19,
        Fortify = 20
    }

    public enum BattleUnitLevel
    {
        TaskForce = 0,
        Company = 1,
        Platoon = 2
    }

    public enum BattleCommsState
    {
        Good = 0,
        Poor = 1,
        Cut = 2
    }

    public enum BattleFireAsset
    {
        Mortar = 0,
        Artillery = 1,
        Cas = 2
    }

    public enum BattleFirePhase
    {
        Processing = 0,
        Adjusting = 1,
        Effect = 2,
        Done = 3,
        Cancelled = 4
    }

    public static class BattleSop
    {
        public const string Traveling = "traveling";
        public const string Overwatch = "overwatch";
        public const string Bounding = "bounding";
        public const string HoldFire = "hold_fire";
        public const string FireAtWill = "fire_at_will";
        public const string LowAltitude = "low";
        public const string HighAltitude = "high";
    }

    [Serializable]
    public sealed class BattleCommandIntent
    {
        public BattleCommandType Type { get; set; }
        public string FormationId { get; set; } = string.Empty;
        public string Technique { get; set; } = string.Empty;
        public string RulesOfEngagement { get; set; } = string.Empty;
        public string Altitude { get; set; } = string.Empty;

        public BattleCommandIntent()
        {
        }

        public BattleCommandIntent(BattleCommandType type)
        {
            Type = type;
        }

        public static BattleCommandIntent Formation(string formationId)
        {
            return new BattleCommandIntent(BattleCommandType.SetFormation)
            {
                FormationId = formationId ?? string.Empty
            };
        }

        public static BattleCommandIntent Sop(
            string technique = "",
            string rulesOfEngagement = "",
            string altitude = "")
        {
            return new BattleCommandIntent(BattleCommandType.SetSop)
            {
                Technique = technique ?? string.Empty,
                RulesOfEngagement = rulesOfEngagement ?? string.Empty,
                Altitude = altitude ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class BattleFormationOption
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public BattleFormationOption()
        {
        }

        public BattleFormationOption(string id, string displayName)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class BattleUnitView
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public BattleUnitLevel Level { get; set; } = BattleUnitLevel.Platoon;
        public float Strength { get; set; } = 1f;
        public float Morale { get; set; } = 100f;
        public BattleCommsState Comms { get; set; } = BattleCommsState.Good;
        public string FormationId { get; set; } = string.Empty;
        public string FormationName { get; set; } = string.Empty;
        public string Technique { get; set; } = BattleSop.Traveling;
        public string RulesOfEngagement { get; set; } = BattleSop.FireAtWill;
        public string Posture { get; set; } = "idle";
        public BattleCommandType? MissionType { get; set; }
        public bool Detached { get; set; }
        public bool Leaderless { get; set; }
        public bool Reforming { get; set; }
        public bool Routed { get; set; }
        public bool AwaitingFollow { get; set; }
        public bool OrderPending { get; set; }
        public bool Destroyed { get; set; }
        public bool OnMap { get; set; } = true;
        public bool HasSquads { get; set; }
        public bool Mounted { get; set; }
        public float AmmoRatio { get; set; } = 1f;
        public int MinesLeft { get; set; }
        public int PositionsBuilt { get; set; }
        public int PositionsCapacity { get; set; } = 3;
        public string Altitude { get; set; } = BattleSop.LowAltitude;

        public string TreeName => Level == BattleUnitLevel.Platoon && !string.IsNullOrEmpty(ShortName)
            ? ShortName
            : DisplayName;
    }

    [Serializable]
    public sealed class BattleHudState
    {
        public float SimulationSeconds { get; set; }
        public float Speed { get; set; } = 1f;
        public bool HasScoring { get; set; } = true;
        public float PlayerVictoryPoints { get; set; }
        public float EnemyVictoryPoints { get; set; }
        public float TimeLimitSeconds { get; set; }
        public float PlayerForceRatio { get; set; } = 1f;
        public float EnemyForceRatio { get; set; } = 1f;
        public bool Deploying { get; set; }
        public bool Paused { get; set; }
        public float? CommandCapacityUsed { get; set; }
        public float? CommandCapacityMaximum { get; set; }
        public int CommandCapacityQueue { get; set; }
        public string AdditionalStatus { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class BattleFireMissionView
    {
        public string Label { get; set; } = string.Empty;
        public BattleFireAsset Asset { get; set; }
        public bool Smoke { get; set; }
        public BattleFirePhase Phase { get; set; }
        public float SecondsRemaining { get; set; }
        public int RoundsLeft { get; set; }
    }

    [Serializable]
    public sealed class BattleFireSupportState
    {
        public int ArtilleryMissions { get; set; }
        public int CasSorties { get; set; }
        public List<BattleFireMissionView> ActiveMissions { get; set; } = new List<BattleFireMissionView>();
    }

    [Serializable]
    public sealed class OrbatNodeData
    {
        public BattleUnitView Unit { get; set; }
        public List<OrbatNodeData> Children { get; set; } = new List<OrbatNodeData>();

        public OrbatNodeData()
        {
        }

        public OrbatNodeData(BattleUnitView unit, params OrbatNodeData[] children)
        {
            Unit = unit;
            if (children != null)
                Children.AddRange(children);
        }
    }

    public sealed class OrbatSelectionIntent
    {
        public int UnitId { get; }
        public bool Additive { get; }
        public IReadOnlyList<int> SelectedUnitIds { get; }

        public OrbatSelectionIntent(int unitId, bool additive, IReadOnlyList<int> selectedUnitIds)
        {
            UnitId = unitId;
            Additive = additive;
            SelectedUnitIds = selectedUnitIds ?? Array.Empty<int>();
        }
    }
}
