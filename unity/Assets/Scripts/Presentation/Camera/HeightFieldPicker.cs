using System;
using UnityEngine;

namespace Echelon.Unity.Presentation
{
    public delegate float HeightSampler(Vector2 simPosition);

    public static class HeightFieldPicker
    {
        public const float RayMarchStepMeters = 15f;
        public const float MaxDistanceMeters = 30000f;
        public const int BinarySearchRefinements = 12;

        private const float BoundaryMarginMeters = 1f;

        public static Vector2 NoHit => new Vector2(float.PositiveInfinity, float.PositiveInfinity);

        public static Vector2 Pick(
            UnityEngine.Camera camera,
            Vector2 screenPosition,
            MapBounds bounds,
            HeightSampler sampleHeight)
        {
            return TryPick(camera, screenPosition, bounds, sampleHeight, out Vector2 simPosition)
                ? simPosition
                : NoHit;
        }

        public static bool TryPick(
            UnityEngine.Camera camera,
            Vector2 screenPosition,
            MapBounds bounds,
            HeightSampler sampleHeight,
            out Vector2 simPosition)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            if (sampleHeight == null)
                throw new ArgumentNullException(nameof(sampleHeight));

            Ray ray = camera.ScreenPointToRay(screenPosition);
            Vector3 direction = ray.direction.normalized;
            if (direction == Vector3.zero)
            {
                simPosition = NoHit;
                return false;
            }

            float previousDistance = 0f;
            float rayDistance = 0f;
            while (rayDistance < MaxDistanceMeters)
            {
                Vector3 point = ray.origin + direction * rayDistance;
                float groundHeight = SampleGround(point, bounds, sampleHeight);
                if (point.y <= groundHeight)
                {
                    float low = previousDistance;
                    float high = rayDistance;
                    for (int i = 0; i < BinarySearchRefinements; i++)
                    {
                        float middle = (low + high) * 0.5f;
                        Vector3 candidate = ray.origin + direction * middle;
                        if (candidate.y <= SampleGround(candidate, bounds, sampleHeight))
                            high = middle;
                        else
                            low = middle;
                    }

                    Vector2 hit = ViewCoords.ToSim(ray.origin + direction * high);
                    simPosition = bounds.Contains(hit) ? hit : bounds.Clamp(hit, BoundaryMarginMeters);
                    return true;
                }

                previousDistance = rayDistance;
                rayDistance += RayMarchStepMeters * Mathf.Max(1f, rayDistance / 2000f);
            }

            simPosition = NoHit;
            return false;
        }

        private static float SampleGround(
            Vector3 worldPosition,
            MapBounds bounds,
            HeightSampler sampleHeight)
        {
            Vector2 simPosition = bounds.Clamp(ViewCoords.ToSim(worldPosition), BoundaryMarginMeters);
            return sampleHeight(simPosition);
        }
    }
}
