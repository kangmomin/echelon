using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class TerrainTests
    {
        [Test]
        public void GridMetadataMatchesMapAndOracle()
        {
            Terrain terrain = TerrainTestSupport.Map01.Value;
            JsonElement golden = TerrainTestSupport.GoldenMap.Value.RootElement;
            Assert.That(terrain.Width, Is.EqualTo(400));
            Assert.That(terrain.HeightCells, Is.EqualTo(400));
            Assert.That(terrain.Width, Is.EqualTo(golden.GetProperty("width").GetInt32()));
            Assert.That(terrain.HeightCells, Is.EqualTo(golden.GetProperty("height").GetInt32()));
            Assert.That(terrain.CellM, Is.EqualTo(golden.GetProperty("cell_m").GetDouble()));
            Assert.That(terrain.SizeM.X, Is.EqualTo(4000.0f));
            Assert.That(terrain.SizeM.Y, Is.EqualTo(4000.0f));
        }

        [TestCase("types")]
        [TestCase("heights")]
        [TestCase("slopes")]
        [TestCase("passable_vehicle")]
        [TestCase("passable_foot")]
        [TestCase("clearance_vehicle")]
        [TestCase("bridge_mask")]
        public void EveryOracleArrayMatchesSha256(string name)
        {
            Terrain terrain = TerrainTestSupport.Map01.Value;
            JsonElement expected = TerrainTestSupport.GoldenMap.Value.RootElement.GetProperty("arrays").GetProperty(name);
            byte[] actualBytes = ArrayBytes(terrain, name, out int count);
            string actualHash = Hash(actualBytes);
            Assert.That(count, Is.EqualTo(expected.GetProperty("count").GetInt32()), name + " count");
            Assert.That(actualHash, Is.EqualTo(expected.GetProperty("sha256").GetString()), name + " full-array SHA-256");
        }

        [Test]
        public void EveryOracleSampleMatchesQueries()
        {
            Terrain terrain = TerrainTestSupport.Map01.Value;
            foreach (JsonElement sample in TerrainTestSupport.GoldenMap.Value.RootElement.GetProperty("samples").EnumerateArray())
            {
                SimVec2 point = TerrainTestSupport.Vector(sample.GetProperty("pos"));
                SimVec2I cell = terrain.CellOf(point);
                string at = point.ToString();
                Assert.That(terrain.TypeAt(point), Is.EqualTo(sample.GetProperty("type").GetInt32()), at);
                Assert.That(terrain.GroundHeight(point), Is.EqualTo(sample.GetProperty("height").GetDouble()).Within(1e-10), at);
                Assert.That(terrain.SlopeAtCell(cell), Is.EqualTo(sample.GetProperty("slope").GetDouble()).Within(1e-10), at);
                Assert.That(terrain.IsPassable(point, Terrain.MobilityVehicle), Is.EqualTo(sample.GetProperty("vehicle").GetBoolean()), at);
                Assert.That(terrain.IsPassable(point, Terrain.MobilityFoot), Is.EqualTo(sample.GetProperty("foot").GetBoolean()), at);
                Assert.That(terrain.IsBridge(point), Is.EqualTo(sample.GetProperty("bridge").GetBoolean()), at);
                Assert.That(terrain.ClearanceVehicle[terrain.IndexOf(cell)], Is.EqualTo(sample.GetProperty("clearance").GetInt32()), at);
            }
        }

        [Test]
        public void RasterizationNearestPassableAndLineQueriesMatchGodotBehavior()
        {
            Terrain terrain = TerrainTestSupport.Map01.Value;
            Assert.That(terrain.TypeAt(new SimVec2(1100.0, 1140.0)), Is.EqualTo((int)Terrain.Type.WATER));
            Assert.That(terrain.TypeAt(new SimVec2(2400.0, 1200.0)), Is.EqualTo((int)Terrain.Type.ROAD));
            Assert.That(terrain.TypeAt(new SimVec2(1300.0, 2550.0)), Is.EqualTo((int)Terrain.Type.FOREST));
            Assert.That(terrain.TypeAt(new SimVec2(3100.0, 1650.0)), Is.EqualTo((int)Terrain.Type.URBAN));
            Assert.That(terrain.TypeAt(new SimVec2(2000.0, 2000.0)), Is.EqualTo((int)Terrain.Type.ROAD));
            Assert.That(terrain.TypeAt(new SimVec2(3000.0, 3500.0)), Is.EqualTo((int)Terrain.Type.OPEN));
            Assert.That(terrain.TypeAt(new SimVec2(-10.0, 100.0)), Is.EqualTo((int)Terrain.Type.WATER));

            SimVec2 nearest = terrain.NearestPassable(new SimVec2(1100.0, 1140.0), Terrain.MobilityVehicle);
            Assert.That(terrain.IsPassable(nearest, Terrain.MobilityVehicle), Is.True);
            Assert.That(nearest.DistanceTo(new SimVec2(1100.0, 1140.0)), Is.LessThan(60.0));
            Assert.That(terrain.LinePassable(new SimVec2(1100.0, 1000.0), new SimVec2(1100.0, 1300.0), Terrain.MobilityVehicle), Is.False);
            Assert.That(terrain.LinePassable(new SimVec2(2600.0, 3500.0), new SimVec2(3400.0, 3500.0), Terrain.MobilityVehicle), Is.True);
            Assert.That(terrain.LinePassable(new SimVec2(-10.0, 100.0), new SimVec2(100.0, 100.0), Terrain.MobilityAir), Is.False);
            Assert.That(terrain.HasTypeNear(new SimVec2(2400.0, 1200.0), (int)Terrain.Type.WATER, 30.0), Is.True);
            Assert.That(terrain.SpeedFactor((int)Terrain.Type.FOREST, Terrain.MobilityVehicle), Is.EqualTo(0.5));
            Assert.That(terrain.SpeedFactor((int)Terrain.Type.FOREST, Terrain.MobilityFoot), Is.EqualTo(0.8));
            Assert.That(terrain.SpeedFactor((int)Terrain.Type.WATER, Terrain.MobilityAir), Is.EqualTo(1.0));
        }

        [Test]
        public void RidgeAndFlatGroundLineOfSightMatchGodot()
        {
            Terrain terrain = TerrainTestSupport.Map01.Value;
            Los.Smoke[] noSmoke = Array.Empty<Los.Smoke>();
            Assert.That(Los.Check(terrain, noSmoke, 0, new SimVec2(900.0, 1450.0), 2.5, new SimVec2(900.0, 2450.0), 2.0), Is.False);
            Assert.That(Los.Check(terrain, noSmoke, 0, new SimVec2(2600.0, 3500.0), 2.5, new SimVec2(3400.0, 3500.0), 2.0), Is.True);
            Assert.That(terrain.GroundHeight(new SimVec2(900.0, 1950.0)) - terrain.GroundHeight(new SimVec2(900.0, 1350.0)), Is.GreaterThan(20.0));
        }

        [Test]
        public void ForestEdgeDeepForestAndSmokeLineOfSightMatchGodot()
        {
            Terrain terrain = TerrainTestSupport.Map01.Value;
            Los.Smoke[] noSmoke = Array.Empty<Los.Smoke>();
            SimVec2 observer = new SimVec2(900.0, 700.0);
            SimVec2 edge = FirstForestPoint(terrain, new SimVec2(1000.0, 700.0), new SimVec2(1.0, 0.0)) + new SimVec2(15.0, 0.0);
            SimVec2 deep = edge + new SimVec2(200.0, 0.0);
            Assert.That(terrain.TypeAt(edge), Is.EqualTo((int)Terrain.Type.FOREST));
            Assert.That(Los.Check(terrain, noSmoke, 0, observer, 2.5, edge, 2.0), Is.True);
            Assert.That(Los.Check(terrain, noSmoke, 0, observer, 2.5, deep, 2.0), Is.False);

            Los.Smoke[] smokes = { new Los.Smoke(new SimVec2(3000.0, 3500.0), 80.0, 100) };
            Assert.That(Los.Check(terrain, smokes, 50, new SimVec2(2600.0, 3500.0), 2.5, new SimVec2(3400.0, 3500.0), 2.0), Is.False);
            Assert.That(Los.Check(terrain, smokes, 150, new SimVec2(2600.0, 3500.0), 2.5, new SimVec2(3400.0, 3500.0), 2.0), Is.True);
            Assert.That(Los.Check(terrain, smokes, 50, new SimVec2(2999.5, 3500.0), 2.5, new SimVec2(3000.0, 3500.0), 2.0), Is.True, "Sub-meter checks return before smoke");
        }

        [Test]
        public void ElementHeightsAltitudeAndBetweenMatchGodot()
        {
            Terrain terrain = TerrainTestSupport.Map01.Value;
            Element infantry = new Element { Def = new ElementDef { ElementClass = "inf_squad" } };
            Element vehicle = new Element
            {
                Def = new ElementDef { ElementClass = "tank" },
                Pos = new SimVec2(2600.0, 3500.0)
            };
            Element target = new Element
            {
                Def = new ElementDef { ElementClass = "tank" },
                Pos = new SimVec2(3400.0, 3500.0)
            };
            Assert.That(Los.EyeHeight(infantry), Is.EqualTo(1.5));
            Assert.That(Los.TargetHeight(infantry), Is.EqualTo(1.0));
            Assert.That(Los.EyeHeight(vehicle), Is.EqualTo(2.5));
            Assert.That(Los.TargetHeight(vehicle), Is.EqualTo(2.0));
            Assert.That(Los.Between(terrain, Array.Empty<Los.Smoke>(), 0, vehicle, target), Is.True);

            Element helicopter = new Element { Def = new ElementDef { ElementClass = "heli" } };
            Assert.That(Los.EyeHeight(helicopter), Is.EqualTo(27.5));
            Assert.That(Los.TargetHeight(helicopter), Is.EqualTo(27.0));
            helicopter.State = ElementState.DESTROYED;
            Assert.That(Los.EyeHeight(helicopter), Is.EqualTo(2.5));
        }

        private static SimVec2 FirstForestPoint(Terrain terrain, SimVec2 from, SimVec2 direction)
        {
            SimVec2 point = from;
            for (int i = 0; i < 200; i++)
            {
                if (terrain.TypeAt(point) == (int)Terrain.Type.FOREST)
                {
                    return point;
                }

                point += direction * 5.0f;
            }

            return point;
        }

        private static byte[] ArrayBytes(Terrain terrain, string name, out int count)
        {
            byte[] bytes;
            switch (name)
            {
                case "types": bytes = terrain.Types; break;
                case "passable_vehicle": bytes = terrain.PassableVehicle; break;
                case "passable_foot": bytes = terrain.PassableFoot; break;
                case "clearance_vehicle": bytes = terrain.ClearanceVehicle; break;
                case "bridge_mask": bytes = terrain.BridgeMask; break;
                case "heights": count = terrain.Heights.Length; return FloatBytes(terrain.Heights);
                case "slopes": count = terrain.Slopes.Length; return FloatBytes(terrain.Slopes);
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }

            count = bytes.Length;
            return bytes;
        }

        private static byte[] FloatBytes(float[] values)
        {
            byte[] bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), BitConverter.SingleToInt32Bits(values[i]));
            }

            return bytes;
        }

        private static string Hash(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }
}
