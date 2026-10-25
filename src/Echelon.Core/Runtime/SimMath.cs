using System;

namespace Echelon.Core
{
    public static class SimMath
    {
        public const double Pi = Math.PI;
        public const double Tau = 2.0 * Math.PI;
        public const float PiFloat = (float)Math.PI;
        public const float TauFloat = (float)Tau;
        public const double CmpEpsilon = 0.00001;

        public static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }

        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        public static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }

        public static long Clamp(long value, long min, long max)
        {
            return value < min ? min : value > max ? max : value;
        }

        public static double Lerp(double from, double to, double weight)
        {
            return from + (to - from) * weight;
        }

        public static float Lerp(float from, float to, float weight)
        {
            return from + (to - from) * weight;
        }

        public static double LerpAngle(double from, double to, double weight)
        {
            return from + AngleDifference(from, to) * weight;
        }

        public static double Wrap(double value, double min, double max)
        {
            double range = max - min;
            if (IsZeroApprox(range))
            {
                return min;
            }

            double result = value - range * Math.Floor((value - min) / range);
            return IsEqualApprox(result, max) ? min : result;
        }

        public static float Wrap(float value, float min, float max)
        {
            float range = max - min;
            if (IsZeroApprox(range))
            {
                return min;
            }

            float result = value - range * MathF.Floor((value - min) / range);
            return IsEqualApprox(result, max) ? min : result;
        }

        public static int Wrap(int value, int min, int max)
        {
            long result = Wrap((long)value, min, max);
            return (int)result;
        }

        public static long Wrap(long value, long min, long max)
        {
            long range = max - min;
            if (range == 0)
            {
                return min;
            }

            return min + ((value - min) % range + range) % range;
        }

        public static double AngleDifference(double from, double to)
        {
            double difference = (to - from) % Tau;
            return 2.0 * difference % Tau - difference;
        }

        public static float AngleDifference(float from, float to)
        {
            float difference = (to - from) % TauFloat;
            return 2.0f * difference % TauFloat - difference;
        }

        public static double Round(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        public static float Round(float value)
        {
            return (float)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        public static double Snap(double value, double step)
        {
            return step == 0.0 ? value : Round(value / step) * step;
        }

        public static float Snap(float value, float step)
        {
            return step == 0.0f ? value : Round(value / step) * step;
        }

        public static bool IsZeroApprox(double value)
        {
            return Math.Abs(value) < CmpEpsilon;
        }

        public static bool IsZeroApprox(float value)
        {
            return Math.Abs(value) < (float)CmpEpsilon;
        }

        public static bool IsEqualApprox(double left, double right)
        {
            if (left == right)
            {
                return true;
            }

            double tolerance = CmpEpsilon * Math.Abs(left);
            if (tolerance < CmpEpsilon)
            {
                tolerance = CmpEpsilon;
            }

            return Math.Abs(left - right) < tolerance;
        }

        public static bool IsEqualApprox(float left, float right)
        {
            if (left == right)
            {
                return true;
            }

            float tolerance = (float)CmpEpsilon * Math.Abs(left);
            if (tolerance < (float)CmpEpsilon)
            {
                tolerance = (float)CmpEpsilon;
            }

            return Math.Abs(left - right) < tolerance;
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * (Pi / 180.0);
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Pi);
        }
    }
}
