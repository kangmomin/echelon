using UnityEngine;

namespace Echelon.Unity.Presentation
{
    public static class ViewCoords
    {
        public static Vector3 ToWorld(Vector2 simPosition, float height = 0f)
        {
            return new Vector3(simPosition.x, height, simPosition.y);
        }

        public static Vector2 ToSim(Vector3 worldPosition)
        {
            return new Vector2(worldPosition.x, worldPosition.z);
        }
    }
}
