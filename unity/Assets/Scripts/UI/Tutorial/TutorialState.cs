using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.UI.Tutorial
{
    public readonly struct TutorialSelectedUnit
    {
        public TutorialSelectedUnit(string unitId, string parentUnitId)
        {
            UnitId = unitId;
            ParentUnitId = parentUnitId;
        }

        public string UnitId { get; }
        public string ParentUnitId { get; }
    }

    public readonly struct TutorialElementState
    {
        public TutorialElementState(bool isAlive, Vector2 velocity)
        {
            IsAlive = isAlive;
            Velocity = velocity;
        }

        public bool IsAlive { get; }
        public Vector2 Velocity { get; }
    }

    public sealed class TutorialState
    {
        public string PlayerSide { get; set; } = "blue";
        public string CompanyUnitId { get; set; } = string.Empty;
        public string EnemyUnitId { get; set; } = string.Empty;
        public Vector2 CompanyPosition { get; set; }

        public Vector2 CameraFocus { get; set; }
        public float CameraDistance { get; set; } = 1400f;

        public IList<TutorialSelectedUnit> Selection { get; } = new List<TutorialSelectedUnit>();
        public IList<string> ControlGroup1 { get; } = new List<string>();

        public string CompanyFormationId { get; set; } = string.Empty;
        public string CompanyRoe { get; set; } = string.Empty;
        public IList<TutorialElementState> CompanyElements { get; } = new List<TutorialElementState>();

        public bool IsPaused { get; set; }
        public float TimeSpeed { get; set; } = 1f;

        public bool EnemyOnMap { get; set; }
        public bool EnemyIsDestroyed { get; set; }
        public IList<TutorialElementState> EnemyElements { get; } = new List<TutorialElementState>();
        public bool HasBattleResult { get; set; }
    }
}
