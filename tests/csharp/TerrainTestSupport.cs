using System;
using System.IO;
using System.Text.Json;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    internal static class TerrainTestSupport
    {
        internal static readonly string RepositoryRoot = FindRepositoryRoot();
        internal static readonly Lazy<DataCatalog> Catalog = new Lazy<DataCatalog>(() =>
            DataLoader.LoadCatalog(Path.Combine(RepositoryRoot, "data")));
        internal static readonly Lazy<Terrain> Map01 = new Lazy<Terrain>(() =>
        {
            MapDef? map = Catalog.Value.GetMap("map01");
            Assert.That(map, Is.Not.Null, "map01 definition");
            return Terrain.Build(map!);
        });
        internal static readonly Lazy<JsonDocument> GoldenMap = new Lazy<JsonDocument>(() =>
            JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "tests", "golden", "map01.json"))));

        internal static SimVec2 Vector(JsonElement array)
        {
            return new SimVec2(array[0].GetDouble(), array[1].GetDouble());
        }

        internal static string OracleBinaryPath(string name)
        {
            return Path.Combine(RepositoryRoot, "build", "unity_oracle", name + ".bin");
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tests", "golden", "map01.json")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate tests/golden/map01.json.");
        }
    }
}
