using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.Presentation.Terrain
{
    public sealed class TerrainViewBuilder
    {
        private readonly TerrainShaderSet _shaderSet;

        public TerrainViewBuilder()
        {
        }

        public TerrainViewBuilder(TerrainShaderSet shaderSet)
        {
            _shaderSet = shaderSet;
        }

        public TerrainView Build(TerrainMeshData data)
        {
            TerrainViewQuality quality = Application.platform == RuntimePlatform.WebGLPlayer
                ? TerrainViewQuality.Web
                : TerrainViewQuality.Desktop;
            return Build(data, quality);
        }

        /// <summary>Creates a terrain root at the world origin; dispose the returned view to release generated resources.</summary>
        public TerrainView Build(TerrainMeshData data, TerrainViewQuality quality)
        {
            TerrainMeshValidation.EnsureValid(data);
            if (quality != TerrainViewQuality.Desktop && quality != TerrainViewQuality.Web)
                throw new ArgumentOutOfRangeException(nameof(quality));
            TerrainShaderSet shaders = _shaderSet ?? TerrainShaderSet.Find();
            shaders.EnsureValid();

            var root = new GameObject("TerrainView");
            var view = root.AddComponent<TerrainView>();
            try
            {
                BuildContents(view, data, quality, shaders);
                return view;
            }
            catch
            {
                view.Dispose();
                throw;
            }
        }

        private static void BuildContents(TerrainView view, TerrainMeshData data, TerrainViewQuality quality, TerrainShaderSet shaders)
        {
            var statistics = new TerrainViewStatistics();
            Material groundMaterial = CreateMaterial(view, shaders.Ground, "TerrainGroundMaterial");
            Material roadDashedMaterial = CreateMaterial(view, shaders.Road, "TerrainRoadDashedMaterial");
            roadDashedMaterial.SetFloat("_CenterDash", 1f);
            Material roadPlainMaterial = CreateMaterial(view, shaders.Road, "TerrainRoadPlainMaterial");
            roadPlainMaterial.SetFloat("_CenterDash", 0f);
            Material waterMaterial = CreateMaterial(view, shaders.Water, "TerrainWaterMaterial");
            Material foliageMaterial = CreateInstanceMaterial(view, shaders.Instances, "TerrainFoliageMaterial", 0.95f);
            Material wallMaterial = CreateInstanceMaterial(view, shaders.Instances, "TerrainHouseWallMaterial", 0.9f);
            Material roofMaterial = CreateInstanceMaterial(view, shaders.Instances, "TerrainHouseRoofMaterial", 0.8f);
            Material bridgeMaterial = CreateInstanceMaterial(view, shaders.Instances, "TerrainBridgeMaterial", 0.9f);

            float heightMin;
            float heightMax;
            Mesh ground = view.Own(TerrainGeometry.BuildHeightMesh(data, out heightMin, out heightMax));
            Texture2D mask = view.Own(TerrainGeometry.BuildTypeMaskTexture(data));
            groundMaterial.SetTexture("_TypeMask", mask);
            groundMaterial.SetTexture("_TypeMaskBlur", mask);
            groundMaterial.SetVector("_MapSize", new Vector4(data.MapSize.x, data.MapSize.y, 0f, 0f));
            groundMaterial.SetFloat("_HeightMin", heightMin);
            groundMaterial.SetFloat("_HeightMax", heightMax);
            groundMaterial.SetFloat("_FieldAngle", TerrainScatter.FieldAngleRadians);
            Vector2 fieldSize = TerrainScatter.FieldSize;
            groundMaterial.SetVector("_FieldSize", new Vector4(fieldSize.x, fieldSize.y, 0f, 0f));
            view.CreateMeshObject("Ground", ground, groundMaterial, ShadowCastingMode.Off);
            view.GroundMesh = ground;
            statistics.GroundVertexCount = ground.vertexCount;
            statistics.GroundTriangleCount = (int)(ground.GetIndexCount(0) / 3u);

            BuildRivers(view, data, waterMaterial, statistics);
            BuildRoads(view, data, roadDashedMaterial, roadPlainMaterial, statistics, out List<TerrainInstance> bridgeBoxes);

            TerrainScatterResult scatter = TerrainScatter.Build(data, quality);
            ShadowCastingMode objectShadows = quality == TerrainViewQuality.Web ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (scatter.BroadleafTrees.Count > 0)
            {
                AddBatch(view, TerrainPrimitives.CreateBroadleafCrown(), foliageMaterial, scatter.BroadleafTrees, objectShadows);
                if (quality == TerrainViewQuality.Desktop)
                {
                    var trunks = new List<TerrainInstance>(scatter.BroadleafTrees.Count);
                    var trunkColor = new Color(0.30f, 0.22f, 0.15f);
                    for (int i = 0; i < scatter.BroadleafTrees.Count; i++)
                    {
                        TerrainInstance tree = scatter.BroadleafTrees[i];
                        trunks.Add(new TerrainInstance(tree.Position, tree.Rotation, tree.Scale, trunkColor));
                    }
                    AddBatch(view, TerrainPrimitives.CreateTreeTrunk(), foliageMaterial, trunks, ShadowCastingMode.Off);
                }
            }
            if (scatter.ConiferTrees.Count > 0)
                AddBatch(view, TerrainPrimitives.CreateConifer(), foliageMaterial, scatter.ConiferTrees, objectShadows);
            if (scatter.Bushes.Count > 0)
                AddBatch(view, TerrainPrimitives.CreateBush(), foliageMaterial, scatter.Bushes, ShadowCastingMode.Off);

            var walls = new List<TerrainInstance>(scatter.Houses.Count);
            var roofs = new List<TerrainInstance>(scatter.Houses.Count);
            for (int i = 0; i < scatter.Houses.Count; i++)
            {
                walls.Add(scatter.Houses[i].CreateWallInstance());
                roofs.Add(scatter.Houses[i].CreateRoofInstance());
            }
            if (walls.Count > 0)
            {
                AddBatch(view, TerrainPrimitives.CreateHouseWalls(), wallMaterial, walls, objectShadows);
                AddBatch(view, TerrainPrimitives.CreateHouseRoof(), roofMaterial, roofs, objectShadows);
            }
            if (bridgeBoxes.Count > 0)
                AddBatch(view, TerrainPrimitives.CreateUnitBox(), bridgeMaterial, bridgeBoxes, objectShadows);

            statistics.BroadleafTreeCount = scatter.BroadleafTrees.Count;
            statistics.ConiferTreeCount = scatter.ConiferTrees.Count;
            statistics.BushCount = scatter.Bushes.Count;
            statistics.HouseCount = scatter.Houses.Count;
            statistics.SharedMeshCount = view.SharedMeshCount;
            statistics.InstancedDrawCallCount = view.InstancedDrawCallCount;
            view.Data = data;
            view.Quality = quality;
            view.Decorations = scatter;
            view.Statistics = statistics;
        }

        private static void BuildRivers(TerrainView view, TerrainMeshData data, Material material, TerrainViewStatistics statistics)
        {
            ReadOnlySpan<TerrainVectorFeature> rivers = data.Rivers.Span;
            for (int riverIndex = 0; riverIndex < rivers.Length; riverIndex++)
            {
                TerrainVectorFeature river = rivers[riverIndex];
                IReadOnlyList<Vector2> points = TerrainGeometry.SmoothPolyline(river.Points.ToArray());
                float[] heights = TerrainGeometry.BuildRiverSurfaceHeights(data, points);
                Mesh mesh = view.Own(TerrainGeometry.BuildRibbonMesh(
                    points,
                    river.WidthMeters * 1.15f,
                    q => data.SampleGroundHeight(data.ClampToMap(q)),
                    heights));
                mesh.name = string.IsNullOrEmpty(river.Name) ? $"River_{riverIndex}" : $"River_{riverIndex}_{river.Name}";
                view.CreateMeshObject("River", mesh, material, ShadowCastingMode.Off);
                statistics.RiverCount++;
                statistics.RibbonVertexCount += mesh.vertexCount;
                statistics.RibbonTriangleCount += (int)(mesh.GetIndexCount(0) / 3u);
            }
        }

        private static void BuildRoads(
            TerrainView view,
            TerrainMeshData data,
            Material dashedMaterial,
            Material plainMaterial,
            TerrainViewStatistics statistics,
            out List<TerrainInstance> bridgeBoxes)
        {
            bridgeBoxes = new List<TerrainInstance>();
            ReadOnlySpan<TerrainVectorFeature> roads = data.Roads.Span;
            for (int roadIndex = 0; roadIndex < roads.Length; roadIndex++)
            {
                TerrainVectorFeature road = roads[roadIndex];
                IReadOnlyList<Vector2> points = TerrainGeometry.SmoothPolyline(road.Points.ToArray());
                var heights = new float[points.Count];
                var bridge = new bool[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 p = data.ClampToMap(points[i]);
                    heights[i] = float.NaN;
                    bridge[i] = data.IsBridge(p) || data.TypeAt(p) == TerrainType.Water;
                }

                int runStart = 0;
                while (runStart < points.Count)
                {
                    if (!bridge[runStart])
                    {
                        runStart++;
                        continue;
                    }
                    int runEnd = runStart;
                    while (runEnd < points.Count && bridge[runEnd])
                        runEnd++;
                    float h0 = data.SampleGroundHeight(data.ClampToMap(points[Mathf.Max(runStart - 1, 0)]));
                    float h1 = data.SampleGroundHeight(data.ClampToMap(points[Mathf.Min(runEnd, points.Count - 1)]));
                    float deck = Mathf.Max(h0, h1) + 0.6f;
                    var runPoints = new List<Vector2>(runEnd - runStart);
                    for (int i = runStart; i < runEnd; i++)
                    {
                        heights[i] = deck;
                        runPoints.Add(points[i]);
                    }
                    IReadOnlyList<TerrainInstance> boxes = TerrainGeometry.BuildBridgeBoxes(runPoints, road.WidthMeters * 1.4f, deck);
                    bridgeBoxes.AddRange(boxes);
                    statistics.BridgeCount++;
                    statistics.BridgePieceCount += boxes.Count;
                    runStart = runEnd;
                }

                Mesh mesh = view.Own(TerrainGeometry.BuildRibbonMesh(
                    points,
                    road.WidthMeters * 1.4f,
                    q => data.SampleGroundHeight(data.ClampToMap(q)) + TerrainGeometry.RoadLiftMeters,
                    heights));
                mesh.name = string.IsNullOrEmpty(road.Name) ? $"Road_{roadIndex}" : $"Road_{roadIndex}_{road.Name}";
                Material material = road.WidthMeters >= 12f ? dashedMaterial : plainMaterial;
                view.CreateMeshObject("Road", mesh, material, ShadowCastingMode.Off);
                statistics.RoadCount++;
                statistics.RibbonVertexCount += mesh.vertexCount;
                statistics.RibbonTriangleCount += (int)(mesh.GetIndexCount(0) / 3u);
            }
        }

        private static Material CreateMaterial(TerrainView view, Shader shader, string name)
        {
            var material = new Material(shader) { name = name };
            return view.Own(material);
        }

        private static Material CreateInstanceMaterial(TerrainView view, Shader shader, string name, float roughness)
        {
            Material material = CreateMaterial(view, shader, name);
            material.enableInstancing = true;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Roughness", roughness);
            return material;
        }

        private static void AddBatch(
            TerrainView view,
            Mesh mesh,
            Material material,
            IReadOnlyList<TerrainInstance> instances,
            ShadowCastingMode shadows)
        {
            view.Own(mesh);
            view.AddBatch(new TerrainDrawBatch(mesh, material, instances, shadows));
        }
    }
}
