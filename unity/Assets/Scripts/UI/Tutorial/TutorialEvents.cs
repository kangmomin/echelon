using System;
using UnityEngine;

namespace Echelon.Unity.UI.Tutorial
{
    public enum TutorialOrderType
    {
        Other = 0,
        Move = 1,
        FastMove = 2,
        AttackMove = 3,
        Seize = 4
    }

    public readonly struct TutorialPlayerOrderEvent
    {
        public TutorialPlayerOrderEvent(
            TutorialOrderType type,
            string targetUnitId,
            string targetParentUnitId,
            bool hasPosition,
            Vector2 position)
        {
            Type = type;
            TargetUnitId = targetUnitId;
            TargetParentUnitId = targetParentUnitId;
            HasPosition = hasPosition;
            Position = position;
        }

        public TutorialOrderType Type { get; }
        public string TargetUnitId { get; }
        public string TargetParentUnitId { get; }
        public bool HasPosition { get; }
        public Vector2 Position { get; }

        public static TutorialPlayerOrderEvent At(
            TutorialOrderType type,
            string targetUnitId,
            string targetParentUnitId,
            Vector2 position)
        {
            return new TutorialPlayerOrderEvent(type, targetUnitId, targetParentUnitId, true, position);
        }

        public static TutorialPlayerOrderEvent WithoutPosition(
            TutorialOrderType type,
            string targetUnitId,
            string targetParentUnitId)
        {
            return new TutorialPlayerOrderEvent(type, targetUnitId, targetParentUnitId, false, Vector2.zero);
        }
    }

    public enum TutorialSimulationEventType
    {
        Other = 0,
        FireRequested = 1,
        OrderReceived = 2,
        BattleEnded = 3
    }

    public readonly struct TutorialSimulationEvent
    {
        public TutorialSimulationEvent(
            TutorialSimulationEventType type,
            string side,
            string unitId,
            string parentUnitId)
        {
            Type = type;
            Side = side;
            UnitId = unitId;
            ParentUnitId = parentUnitId;
        }

        public TutorialSimulationEventType Type { get; }
        public string Side { get; }
        public string UnitId { get; }
        public string ParentUnitId { get; }

        public static TutorialSimulationEvent FireRequested(string side)
        {
            return new TutorialSimulationEvent(TutorialSimulationEventType.FireRequested, side, string.Empty, string.Empty);
        }

        public static TutorialSimulationEvent OrderReceived(string unitId, string parentUnitId)
        {
            return new TutorialSimulationEvent(
                TutorialSimulationEventType.OrderReceived,
                string.Empty,
                unitId,
                parentUnitId);
        }

        public static TutorialSimulationEvent BattleEnded()
        {
            return new TutorialSimulationEvent(
                TutorialSimulationEventType.BattleEnded,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }

    public interface ITutorialEventSource
    {
        event Action<TutorialPlayerOrderEvent> PlayerOrderIssued;
        event Action<TutorialSimulationEvent> SimulationEventEmitted;
    }
}
