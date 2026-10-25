using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.Presentation.Fx
{
    internal sealed class FxBatchRenderer
    {
        private const int MaxBatchSize = 511;

        private readonly Matrix4x4[] _matrices = new Matrix4x4[MaxBatchSize];
        private readonly Vector4[] _colors = new Vector4[MaxBatchSize];
        private readonly Vector4[] _parameters = new Vector4[MaxBatchSize];
        private readonly List<Vector3> _lineVertices = new List<Vector3>();
        private readonly List<Color> _lineColors = new List<Color>();
        private readonly List<int> _lineIndices = new List<int>();
        private MaterialPropertyBlock _properties;
        private Mesh _particleMesh;
        private Mesh _scorchMesh;
        private Mesh _lineMesh;
        private Material _smokeMaterial;
        private Material _fireMaterial;
        private Material _scorchMaterial;
        private Material _lineMaterial;

        internal void Initialize()
        {
            if (_particleMesh != null)
                return;
            _properties = new MaterialPropertyBlock();
            _particleMesh = CreateQuad(false, "Fx Billboard Quad");
            _scorchMesh = CreateQuad(true, "Fx Ground Quad");
            _lineMesh = new Mesh { name = "Fx Tracer Lines", hideFlags = HideFlags.HideAndDontSave };
            _lineMesh.MarkDynamic();
            _smokeMaterial = CreateMaterial("FxSmoke", "Echelon/Fx/Smoke");
            _fireMaterial = CreateMaterial("FxFire", "Echelon/Fx/Fire");
            _scorchMaterial = CreateMaterial("FxScorch", "Echelon/Fx/Scorch");
            _lineMaterial = CreateMaterial("FxLine", "Echelon/Fx/Line");
        }

        internal void Render(FxEffectSystem effects)
        {
            if (_particleMesh == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            DrawParticleLayer(effects, FxParticleLayer.Smoke, _smokeMaterial);
            DrawParticleLayer(effects, FxParticleLayer.Fire, _fireMaterial);
            DrawScorches(effects);
            DrawTracers(effects);
        }

        internal void Dispose()
        {
            DestroyObject(_particleMesh);
            DestroyObject(_scorchMesh);
            DestroyObject(_lineMesh);
            DestroyObject(_smokeMaterial);
            DestroyObject(_fireMaterial);
            DestroyObject(_scorchMaterial);
            DestroyObject(_lineMaterial);
            _particleMesh = null;
            _scorchMesh = null;
            _lineMesh = null;
            _smokeMaterial = null;
            _fireMaterial = null;
            _scorchMaterial = null;
            _lineMaterial = null;
        }

        private void DrawParticleLayer(FxEffectSystem effects, FxParticleLayer layer, Material material)
        {
            if (material == null)
                return;
            int total = layer == FxParticleLayer.Smoke ? effects.SmokeParticleCount : effects.FireParticleCount;
            for (int start = 0; start < total; start += MaxBatchSize)
            {
                int count = Mathf.Min(MaxBatchSize, total - start);
                for (int i = 0; i < count; i++)
                {
                    FxParticleState particle = effects.GetParticle(layer, start + i);
                    _matrices[i] = Matrix4x4.TRS(particle.Position, Quaternion.identity,
                        new Vector3(particle.Size, particle.Size, particle.Size));
                    _colors[i] = particle.Color;
                    _parameters[i] = new Vector4(particle.Rotation, particle.Seed, particle.NormalizedAge, 0.0f);
                }
                DrawBatch(_particleMesh, material, count);
            }
        }

        private void DrawScorches(FxEffectSystem effects)
        {
            if (_scorchMaterial == null)
                return;
            int count = 0;
            for (int slot = 0; slot < effects.ScorchSlotCount; slot++)
            {
                FxScorchState scorch = effects.GetScorchSlot(slot);
                if (scorch.Age >= FxEffectSystem.ScorchSeconds || scorch.Alpha <= 0.0f)
                    continue;
                _matrices[count] = Matrix4x4.TRS(scorch.Position,
                    Quaternion.AngleAxis(scorch.Rotation * Mathf.Rad2Deg, Vector3.up),
                    new Vector3(scorch.Radius, 1.0f, scorch.Radius));
                _colors[count] = new Vector4(0.0f, 0.0f, 0.0f, scorch.Alpha);
                _parameters[count] = new Vector4(0.0f, scorch.Seed, 0.0f, 0.0f);
                count++;
                if (count == MaxBatchSize)
                {
                    DrawBatch(_scorchMesh, _scorchMaterial, count);
                    count = 0;
                }
            }
            if (count > 0)
                DrawBatch(_scorchMesh, _scorchMaterial, count);
        }

        private void DrawBatch(Mesh mesh, Material material, int count)
        {
            if (SystemInfo.supportsInstancing)
            {
                _properties.Clear();
                _properties.SetVectorArray("_FxColor", _colors);
                _properties.SetVectorArray("_FxParams", _parameters);
                RenderParams renderParams = CreateRenderParams(material, _properties);
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, _matrices, count);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                _properties.Clear();
                _properties.SetVector("_FxColor", _colors[i]);
                _properties.SetVector("_FxParams", _parameters[i]);
                RenderParams renderParams = CreateRenderParams(material, _properties);
                Graphics.RenderMesh(renderParams, mesh, 0, _matrices[i]);
            }
        }

        private static RenderParams CreateRenderParams(Material material, MaterialPropertyBlock properties)
        {
            return new RenderParams(material)
            {
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                matProps = properties
            };
        }

        private void DrawTracers(FxEffectSystem effects)
        {
            _lineVertices.Clear();
            _lineColors.Clear();
            _lineIndices.Clear();
            if (_lineMaterial != null)
            {
                for (int i = 0; i < effects.ProjectileCount; i++)
                {
                    FxProjectileState projectile = effects.GetProjectile(i);
                    if (projectile.IsMissile)
                        continue;
                    int index = _lineVertices.Count;
                    _lineVertices.Add(projectile.Tail);
                    _lineVertices.Add(projectile.Head);
                    Color color = projectile.Color;
                    _lineColors.Add(new Color(color.r, color.g, color.b, 0.0f));
                    _lineColors.Add(color);
                    _lineIndices.Add(index);
                    _lineIndices.Add(index + 1);
                }
            }

            _lineMesh.Clear();
            if (_lineVertices.Count == 0)
                return;
            _lineMesh.SetVertices(_lineVertices);
            _lineMesh.SetColors(_lineColors);
            _lineMesh.SetIndices(_lineIndices, MeshTopology.Lines, 0);
            _lineMesh.RecalculateBounds();
            RenderParams renderParams = CreateRenderParams(_lineMaterial, null);
            Graphics.RenderMesh(renderParams, _lineMesh, 0, Matrix4x4.identity);
        }

        private static Material CreateMaterial(string resourceName, string shaderName)
        {
            Shader shader = Resources.Load<Shader>(resourceName);
            if (shader == null)
                shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Missing combat FX shader: " + shaderName);
                return null;
            }
            Material material = new Material(shader)
            {
                name = "Fx " + resourceName,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            return material;
        }

        private static Mesh CreateQuad(bool ground, string name)
        {
            Mesh mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = ground
                ? new[]
                {
                    new Vector3(-1.0f, 0.0f, -1.0f), new Vector3(1.0f, 0.0f, -1.0f),
                    new Vector3(1.0f, 0.0f, 1.0f), new Vector3(-1.0f, 0.0f, 1.0f)
                }
                : new[]
                {
                    new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f, 1.0f, 0.0f), new Vector3(-1.0f, 1.0f, 0.0f)
                };
            mesh.uv = new[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyObject(Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }
    }
}
