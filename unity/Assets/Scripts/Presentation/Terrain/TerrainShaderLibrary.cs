using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    public sealed class TerrainShaderLibrary : ScriptableObject
    {
        public Shader Ground;
        public Shader Road;
        public Shader Water;
        public Shader Instances;
        public Shader[] AdditionalShaders;

        public TerrainShaderSet ToSet()
        {
            return new TerrainShaderSet(Ground, Road, Water, Instances);
        }
    }
}
