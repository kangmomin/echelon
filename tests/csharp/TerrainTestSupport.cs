using System;
using System.IO;
using System.Text.Json;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    internal static class TerrainTestSupport
    {
        internal static readonly Lazy<Terrain> Map01 = new Lazy<Terrain>(() =>
        {
            MapDef? map = DataTestAssets.Catalog.GetMap("map01");
            Assert.That(map, Is.Not.Null, "map01 definition");
            return Terrain.Build(map!);
        });
        internal static readonly Lazy<JsonDocument> GoldenMap = new Lazy<JsonDocument>(() =>
            JsonDocument.Parse(File.ReadAllText(Path.Combine(DataTestAssets.GoldenRoot, "map01.json"))));

        internal static SimVec2 Vector(JsonElement array)
        {
            return new SimVec2(array[0].GetDouble(), array[1].GetDouble());
        }
    }
}
