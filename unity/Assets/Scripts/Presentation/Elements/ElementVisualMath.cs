using System;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements
{
    public static class ElementVisualMath
    {
        public const float TicksPerSecond = 10.0f;
        public const int ContactMemoryTicks = 100;
        public const float TurretRadiansPerSecond = 1.1f;
        public const float RecoilMeters = 0.35f;
        public const float RecoilDecayPerSecond = 3.0f;

        public static Vector2 InterpolatePosition(Vector2 previous, Vector2 current, float alpha)
        {
            return Vector2.Lerp(previous, current, Mathf.Clamp01(alpha));
        }

        public static float InterpolateHeading(float previous, float current, float alpha)
        {
            float t = Mathf.Clamp01(alpha);
            return WrapAngle(previous + WrapAngle(current - previous) * t);
        }

        public static float HeadingToYaw(float heading)
        {
            return WrapAngle(heading + Mathf.PI);
        }

        public static float WrapAngle(float angle)
        {
            return angle - (Mathf.PI * 2.0f) * Mathf.Floor((angle + Mathf.PI) / (Mathf.PI * 2.0f));
        }

        public static float MoveTowards(float current, float target, float maximumDelta)
        {
            float delta = target - current;
            if (Mathf.Abs(delta) <= maximumDelta)
                return target;
            return current + Mathf.Sign(delta) * maximumDelta;
        }

        public static float MoveTowardsAngle(float current, float target, float maximumDelta)
        {
            float difference = WrapAngle(target - current);
            float step = Mathf.Clamp(difference, -maximumDelta, maximumDelta);
            return WrapAngle(current + step);
        }

        public static float TargetTurretYaw(Vector2 shooterPosition, float shooterHeading, Vector2 targetPosition)
        {
            Vector2 delta = targetPosition - shooterPosition;
            if (delta.sqrMagnitude <= 1.0f)
                return 0.0f;
            return WrapAngle(Mathf.Atan2(delta.x, delta.y) - shooterHeading);
        }

        public static bool IsVisible(ElementVisualState state, string fogSide, int currentTick)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (!state.OnMap || state.IsMounted)
                return false;
            if (string.IsNullOrEmpty(fogSide) || !state.IsAlive
                || string.Equals(state.Side, fogSide, StringComparison.OrdinalIgnoreCase))
                return true;
            return currentTick - state.LastSeenTick <= ContactMemoryTicks;
        }

        public static Vector2 TerrainAnglesFromNormal(Vector3 normal, float heading)
        {
            if (normal.sqrMagnitude < 0.000001f)
                return Vector2.zero;
            normal.Normalize();
            if (normal.y < 0.0f)
                normal = -normal;
            if (normal.y < 0.0001f)
                return Vector2.zero;

            Vector2 forward = Forward(heading);
            Vector2 right = Right(forward);
            var forward3 = new Vector3(forward.x, 0.0f, forward.y);
            var right3 = new Vector3(right.x, 0.0f, right.y);
            float pitch = Mathf.Atan2(-Vector3.Dot(normal, forward3), normal.y);
            float roll = Mathf.Atan2(Vector3.Dot(normal, right3), normal.y);
            return new Vector2(pitch, roll);
        }

        public static Vector2 Forward(float heading)
        {
            return new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
        }

        public static Vector2 Right(Vector2 forward)
        {
            return new Vector2(forward.y, -forward.x);
        }

        public static float DestroyedTurretFactor(int elementId)
        {
            return ((long)elementId * 7919L % 100L) / 100.0f;
        }
    }
}
