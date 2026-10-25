using System;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    public sealed class TerrainShaderSet
    {
        public const string GroundShaderName = "Echelon/Terrain/Ground";
        public const string RoadShaderName = "Echelon/Terrain/Road";
        public const string WaterShaderName = "Echelon/Terrain/Water";
        public const string InstancesShaderName = "Echelon/Terrain/Instances";

        public Shader Ground { get; }
        public Shader Road { get; }
        public Shader Water { get; }
        public Shader Instances { get; }

        public TerrainShaderSet(Shader ground, Shader road, Shader water, Shader instances)
        {
            Ground = ground;
            Road = road;
            Water = water;
            Instances = instances;
        }

        public static TerrainShaderSet Find()
        {
            TerrainShaderLibrary library = Resources.Load<TerrainShaderLibrary>("TerrainShaders");
            if (library != null)
                return library.ToSet();
            var shaders = new TerrainShaderSet(
                Shader.Find(GroundShaderName),
                Shader.Find(RoadShaderName),
                Shader.Find(WaterShaderName),
                Shader.Find(InstancesShaderName));
            shaders.EnsureValid();
            return shaders;
        }

        internal void EnsureValid()
        {
            if (Ground == null || Road == null || Water == null || Instances == null)
            {
                throw new InvalidOperationException(
                    "Terrain shaders are missing. Include Echelon/Terrain shaders in the build or pass a TerrainShaderSet with explicit references.");
            }
        }
    }
}
