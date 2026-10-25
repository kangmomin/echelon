#if NUNIT_LITE
using NUnitLite;

namespace Echelon.Core.Tests
{
    public static class AStarTestRunner
    {
        public static int Main(string[] args)
        {
            return new AutoRun().Execute(args);
        }
    }
}
#endif
