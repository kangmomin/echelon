using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.Presentation.Terrain
{
    public sealed class TerrainView : MonoBehaviour, IDisposable
    {
        private readonly List<UnityEngine.Object> _resources = new List<UnityEngine.Object>();
        private readonly List<TerrainDrawBatch> _batches = new List<TerrainDrawBatch>();

        public TerrainMeshData Data { get; internal set; }
        public TerrainViewQuality Quality { get; internal set; }
        public TerrainViewStatistics Statistics { get; internal set; }
        public TerrainScatterResult Decorations { get; internal set; }
        public Mesh GroundMesh { get; internal set; }

        internal int SharedMeshCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _resources.Count; i++)
                {
                    if (_resources[i] is Mesh)
                        count++;
                }
                return count;
            }
        }

        internal int InstancedDrawCallCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _batches.Count; i++)
                    count += _batches[i].DrawCallCount;
                return count;
            }
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (Data == null)
                errors.Add("TerrainView has no terrain data.");
            else
                errors.AddRange(Data.Validate());
            if (GroundMesh == null)
                errors.Add("TerrainView has no ground mesh.");
            else if (Data != null)
                errors.AddRange(TerrainMeshValidation.ValidateGridMesh(GroundMesh, Data));

            for (int i = 0; i < _resources.Count; i++)
            {
                if (_resources[i] is Mesh mesh && mesh != GroundMesh)
                    errors.AddRange(TerrainMeshValidation.ValidateMesh(mesh, true));
            }
            if (Statistics == null)
                errors.Add("TerrainView has no build statistics.");
            if (Decorations == null)
                errors.Add("TerrainView has no decoration scatter result.");
            return errors.AsReadOnly();
        }

        public void Dispose()
        {
            if (this == null)
                return;
            DestroyUnityObject(gameObject);
        }

        internal T Own<T>(T resource) where T : UnityEngine.Object
        {
            _resources.Add(resource);
            return resource;
        }

        internal void AddBatch(TerrainDrawBatch batch)
        {
            _batches.Add(batch);
        }

        internal void CreateMeshObject(string name, Mesh mesh, Material material, ShadowCastingMode shadows)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(transform, false);
            child.layer = gameObject.layer;
            var filter = child.GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = true;
        }

        private void LateUpdate()
        {
            int layer = gameObject.layer;
            for (int i = 0; i < _batches.Count; i++)
                _batches[i].Draw(layer);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _resources.Count; i++)
            {
                if (_resources[i] != null)
                    DestroyUnityObject(_resources[i]);
            }
            _resources.Clear();
            _batches.Clear();
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
