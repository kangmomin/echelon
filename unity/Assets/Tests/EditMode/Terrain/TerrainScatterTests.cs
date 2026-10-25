using System.Collections.Generic;
using Echelon.Unity.Presentation.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain.Tests
{
    public sealed class TerrainScatterTests
    {
        [Test]
        public void ScatterIsRepeatableAndDoesNotChangeUnityRandomState()
        {
            TerrainMeshData data = TerrainTestData.Create(26, 26, TerrainType.Forest);
            UnityEngine.Random.State state = UnityEngine.Random.state;
            TerrainScatterResult first = TerrainScatter.Build(data, TerrainViewQuality.Desktop);
            TerrainScatterResult second = TerrainScatter.Build(data, TerrainViewQuality.Desktop);

            Assert.That(first.BroadleafTrees.Count + first.ConiferTrees.Count, Is.GreaterThan(0));
            AssertInstancesEqual(first.BroadleafTrees, second.BroadleafTrees);
            AssertInstancesEqual(first.ConiferTrees, second.ConiferTrees);
            AssertInstancesEqual(first.Bushes, second.Bushes);
            Assert.That(first.Houses.Count, Is.EqualTo(second.Houses.Count));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(state));
        }

        [Test]
        public void WebQualityReducesForestDensity()
        {
            TerrainMeshData data = TerrainTestData.Create(26, 26, TerrainType.Forest);
            TerrainScatterResult desktop = TerrainScatter.Build(data, TerrainViewQuality.Desktop);
            TerrainScatterResult web = TerrainScatter.Build(data, TerrainViewQuality.Web);
            int desktopCount = desktop.BroadleafTrees.Count + desktop.ConiferTrees.Count;
            int webCount = web.BroadleafTrees.Count + web.ConiferTrees.Count;
            Assert.That(webCount, Is.LessThan(desktopCount));
        }

        [Test]
        public void OpenFieldsProduceHedgeBushesAndOccasionalTrees()
        {
            TerrainMeshData data = TerrainTestData.Create(24, 24);
            TerrainScatterResult scatter = TerrainScatter.Build(data, TerrainViewQuality.Desktop);
            Assert.That(scatter.Bushes.Count, Is.GreaterThan(0));
            Assert.That(scatter.BroadleafTrees.Count, Is.GreaterThan(0));
        }

        [Test]
        public void UrbanScatterProducesFullSizedColoredHouses()
        {
            TerrainMeshData data = TerrainTestData.Create(12, 12, TerrainType.Urban);
            TerrainScatterResult scatter = TerrainScatter.Build(data, TerrainViewQuality.Desktop);
            Assert.That(scatter.Houses.Count, Is.GreaterThan(0));
            for (int i = 0; i < scatter.Houses.Count; i++)
            {
                TerrainHouse house = scatter.Houses[i];
                Assert.That(house.Size.x, Is.GreaterThanOrEqualTo(7.1999f));
                Assert.That(house.Size.y, Is.GreaterThanOrEqualTo(4.3999f));
                Assert.That(house.Size.z, Is.GreaterThanOrEqualTo(6.3999f));
                Assert.That(house.WallColor.a, Is.EqualTo(1f));
                Assert.That(house.RoofColor.a, Is.EqualTo(1f));
                Assert.That(house.CreateWallInstance().Color, Is.EqualTo(house.WallColor));
                Assert.That(house.CreateRoofInstance().Color, Is.EqualTo(house.RoofColor));
            }
        }

        [Test]
        public void ChangingDecorationSeedChangesOnlyTheDecorationSequence()
        {
            TerrainMeshData firstData = TerrainTestData.Create(26, 26, TerrainType.Forest, seed: 11);
            TerrainMeshData secondData = TerrainTestData.Create(26, 26, TerrainType.Forest, seed: 12);
            TerrainScatterResult first = TerrainScatter.Build(firstData, TerrainViewQuality.Desktop);
            TerrainScatterResult second = TerrainScatter.Build(secondData, TerrainViewQuality.Desktop);
            Assert.That(first.BroadleafTrees.Count + first.ConiferTrees.Count, Is.GreaterThan(0));
            CollectionAssert.AreNotEqual(ToArray(first.BroadleafTrees), ToArray(second.BroadleafTrees));
        }

        private static void AssertInstancesEqual(IReadOnlyList<TerrainInstance> first, IReadOnlyList<TerrainInstance> second)
        {
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
                Assert.That(second[i], Is.EqualTo(first[i]));
        }

        private static TerrainInstance[] ToArray(IReadOnlyList<TerrainInstance> items)
        {
            var copy = new TerrainInstance[items.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = items[i];
            return copy;
        }
    }
}
