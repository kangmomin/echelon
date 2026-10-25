namespace Echelon.Unity.Presentation.Terrain
{
    public sealed class TerrainViewStatistics
    {
        public int GroundVertexCount { get; internal set; }
        public int GroundTriangleCount { get; internal set; }
        public int RoadCount { get; internal set; }
        public int RiverCount { get; internal set; }
        public int RibbonVertexCount { get; internal set; }
        public int RibbonTriangleCount { get; internal set; }
        public int BridgeCount { get; internal set; }
        public int BridgePieceCount { get; internal set; }
        public int BroadleafTreeCount { get; internal set; }
        public int ConiferTreeCount { get; internal set; }
        public int BushCount { get; internal set; }
        public int HouseCount { get; internal set; }
        public int SharedMeshCount { get; internal set; }
        public int InstancedDrawCallCount { get; internal set; }

        public int DecorationInstanceCount => BroadleafTreeCount + ConiferTreeCount + BushCount + HouseCount;

        internal TerrainViewStatistics()
        {
        }
    }
}
