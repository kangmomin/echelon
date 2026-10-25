using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    public static class TerrainCoordinates
    {
        public const float DefaultFieldSizeMeters = 4000f;

        public static Vector2 DefaultMapSize => new Vector2(DefaultFieldSizeMeters, DefaultFieldSizeMeters);

        public static Vector3 ToUnity(Vector2 simulationPosition, float height = 0f)
        {
            return new Vector3(simulationPosition.x, height, simulationPosition.y);
        }

        public static Vector2 ToSimulation(Vector3 unityPosition)
        {
            return new Vector2(unityPosition.x, unityPosition.z);
        }

        public static float DirectionToYawRadians(Vector2 simulationDirection)
        {
            if (simulationDirection.sqrMagnitude < 0.000001f)
                return 0f;
            return Mathf.Atan2(simulationDirection.x, simulationDirection.y);
        }

        public static Quaternion DirectionToRotation(Vector2 simulationDirection)
        {
            return Quaternion.AngleAxis(DirectionToYawRadians(simulationDirection) * Mathf.Rad2Deg, Vector3.up);
        }
    }
}
