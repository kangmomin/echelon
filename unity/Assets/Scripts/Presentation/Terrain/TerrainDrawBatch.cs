using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.Presentation.Terrain
{
    internal sealed class TerrainDrawBatch
    {
        private const int MaximumInstancesPerDraw = 1023;
        private static readonly int InstanceColorProperty = Shader.PropertyToID("_InstanceColor");

        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly ShadowCastingMode _shadows;
        private readonly List<Chunk> _chunks = new List<Chunk>();
        private readonly bool _useInstancing;

        internal int DrawCallCount => _chunks.Count;

        internal TerrainDrawBatch(
            Mesh mesh,
            Material material,
            IReadOnlyList<TerrainInstance> instances,
            ShadowCastingMode shadows)
        {
            _mesh = mesh;
            _material = material;
            _shadows = shadows;
            _material.enableInstancing = true;
            _useInstancing = SystemInfo.supportsInstancing;
            for (int start = 0; start < instances.Count; start += MaximumInstancesPerDraw)
            {
                int count = Mathf.Min(MaximumInstancesPerDraw, instances.Count - start);
                var matrices = new Matrix4x4[count];
                var colors = new Vector4[count];
                for (int i = 0; i < count; i++)
                {
                    TerrainInstance instance = instances[start + i];
                    matrices[i] = instance.ToMatrix();
                    Color linear = instance.Color.linear;
                    colors[i] = new Vector4(linear.r, linear.g, linear.b, linear.a);
                }
                _chunks.Add(new Chunk(matrices, colors));
            }
        }

        internal void Draw(int layer)
        {
            for (int i = 0; i < _chunks.Count; i++)
                _chunks[i].Draw(_mesh, _material, _shadows, layer, _useInstancing);
        }

        private sealed class Chunk
        {
            private readonly Matrix4x4[] _matrices;
            private readonly Vector4[] _colors;
            private readonly MaterialPropertyBlock _properties;

            internal Chunk(Matrix4x4[] matrices, Vector4[] colors)
            {
                _matrices = matrices;
                _colors = colors;
                _properties = new MaterialPropertyBlock();
                _properties.SetVectorArray(InstanceColorProperty, colors);
            }

            internal void Draw(Mesh mesh, Material material, ShadowCastingMode shadows, int layer, bool useInstancing)
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    return;
                if (useInstancing)
                {
                    Graphics.DrawMeshInstanced(mesh, 0, material, _matrices, _matrices.Length, _properties, shadows, true, layer);
                    return;
                }

                for (int i = 0; i < _matrices.Length; i++)
                {
                    _properties.SetVector(InstanceColorProperty, _colors[i]);
                    Graphics.DrawMesh(mesh, _matrices[i], material, layer, null, 0, _properties, shadows, true);
                }
            }
        }
    }
}
