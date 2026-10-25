using System.Collections.Generic;

namespace Echelon.Core
{
    public static class SimConst
    {
        public const int TickRate = 10;
        public const double TickDt = 1.0 / TickRate;
        public const double TickEpsilon = 1e-6;
        public const int DefaultSpeedIndex = 1;
        public const int MaxTicksPerAdvance = 8;
        public const double CellSizeM = 10.0;

        /// <summary>
        /// 이동 시간 압축 배율. 장비 데이터의 속도(km/h)는 실제 제원 그대로 두고, 전장 이동만 이만큼 빠르게 한다(D-048).
        /// </summary>
        public const double MovementSpeedScale = 1.5;

        private static readonly double[] SpeedStepValues = { 0.5, 1.0, 2.0, 4.0 };

        public static IReadOnlyList<double> SpeedSteps => SpeedStepValues;
    }
}
