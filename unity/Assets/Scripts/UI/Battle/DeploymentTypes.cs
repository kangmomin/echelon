using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.UI.Battle
{
    [Serializable]
    public sealed class DeploymentZoneData
    {
        public Rect Zone { get; set; }
        public Vector2 MapSize { get; set; }
        public List<Vector2> EntryPoints { get; set; } = new List<Vector2>();

        public IReadOnlyList<Vector2> Corners => Array.AsReadOnly(new[]
        {
            new Vector2(Zone.xMin, Zone.yMin),
            new Vector2(Zone.xMax, Zone.yMin),
            new Vector2(Zone.xMax, Zone.yMax),
            new Vector2(Zone.xMin, Zone.yMax)
        });

        public Vector2 LabelPosition => new Vector2(
            Zone.center.x,
            Zone.center.y < MapSize.y * 0.5f ? Zone.yMax : Zone.yMin);

        public bool Contains(Vector2 point)
        {
            return Zone.Contains(point);
        }
    }

    [Serializable]
    public sealed class DeploymentUnitState
    {
        public int UnitId { get; set; }
        public float HeadingRadians { get; set; }

        public DeploymentUnitState()
        {
        }

        public DeploymentUnitState(int unitId, float headingRadians)
        {
            UnitId = unitId;
            HeadingRadians = headingRadians;
        }
    }

    public sealed class DeploymentPlacementIntent
    {
        public int UnitId { get; }
        public Vector2 Position { get; }
        public float HeadingRadians { get; }

        public DeploymentPlacementIntent(int unitId, Vector2 position, float headingRadians)
        {
            UnitId = unitId;
            Position = position;
            HeadingRadians = headingRadians;
        }
    }

    public static class DeploymentPlacement
    {
        public const float SpacingMeters = 350f;

        public static IReadOnlyList<DeploymentPlacementIntent> CreateIntents(
            IReadOnlyList<DeploymentUnitState> units,
            Vector2 target,
            float? facingRadians)
        {
            var result = new List<DeploymentPlacementIntent>();
            if (units == null)
                return result;
            bool sharedFacing = facingRadians.HasValue && !float.IsNaN(facingRadians.Value);
            for (int i = 0; i < units.Count; i++)
            {
                DeploymentUnitState unit = units[i];
                if (unit == null)
                    continue;
                float heading = sharedFacing ? facingRadians.Value : unit.HeadingRadians;
                float offset = (i - (units.Count - 1) * 0.5f) * SpacingMeters;
                var right = new Vector2(Mathf.Cos(heading), -Mathf.Sin(heading));
                result.Add(new DeploymentPlacementIntent(unit.UnitId, target + right * offset, heading));
            }
            return result;
        }
    }
}
