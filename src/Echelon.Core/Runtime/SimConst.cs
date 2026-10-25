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

        private static readonly double[] SpeedStepValues = { 0.5, 1.0, 2.0, 4.0 };

        public static IReadOnlyList<double> SpeedSteps => SpeedStepValues;
    }
}
