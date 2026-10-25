using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Echelon.Core;

namespace Echelon.Headless
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (HasOption(args, "--help") || HasOption(args, "-h"))
            {
                PrintUsage();
                return 0;
            }
            try
            {
                string root = FindRepositoryRoot(OptionValue(args, "--data="));
                DataCatalog catalog = DataLoader.LoadCatalog(Path.Combine(root, "data"));
                if (HasOption(args, "--snapshots"))
                    return CanonicalSnapshot.Run(catalog, args);
                if (OptionValue(args, "--seed=") != null)
                    return BattleRunner.Run(catalog, args);

                bool catalogOnly = HasOption(args, "--catalog-only");
                bool terrainOnly = HasOption(args, "--terrain-only");
                foreach (string arg in args)
                {
                    if (!arg.StartsWith("--data=", StringComparison.Ordinal) &&
                        !arg.StartsWith("--golden=", StringComparison.Ordinal) &&
                        !catalogOnly && !terrainOnly)
                    {
                        Console.Error.WriteLine("Unknown option: " + arg);
                        PrintUsage();
                        return 2;
                    }
                }
                string golden = OptionValue(args, "--golden=") ??
                    Path.Combine(root, "tests", "golden", "map01.json");
                bool ok = ValidateCatalog(catalog);
                if (!catalogOnly)
                    ok = ValidateTerrain(catalog, golden) && ok;
                Console.WriteLine(ok ? "[headless] OK" : "[headless] FAIL");
                return ok ? 0 : 1;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("[headless] " + error.Message);
                return 2;
            }
        }

        private static bool ValidateCatalog(DataCatalog catalog)
        {
            Console.WriteLine("[data] " + catalog.Summary());
            foreach (string error in catalog.Errors)
                Console.Error.WriteLine("[data:error] " + error);
            foreach (string warning in catalog.Warnings)
                Console.WriteLine("[data:warning] " + warning);
            return catalog.IsValid();
        }

        private static bool ValidateTerrain(DataCatalog catalog, string goldenPath)
        {
            MapDef? map = catalog.GetMap("map01");
            if (map == null)
            {
                Console.Error.WriteLine("[terrain] map01 is missing");
                return false;
            }
            Terrain terrain = Terrain.Build(map);
            using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(goldenPath));
            JsonElement arrays = golden.RootElement.GetProperty("arrays");
            var actual = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["types"] = terrain.Types,
                ["heights"] = FloatBytes(terrain.Heights),
                ["slopes"] = FloatBytes(terrain.Slopes),
                ["passable_vehicle"] = terrain.PassableVehicle,
                ["passable_foot"] = terrain.PassableFoot,
                ["clearance_vehicle"] = terrain.ClearanceVehicle,
                ["bridge_mask"] = terrain.BridgeMask,
            };
            int failures = 0;
            foreach (KeyValuePair<string, byte[]> pair in actual)
            {
                string expected = arrays.GetProperty(pair.Key).GetProperty("sha256").GetString() ?? "";
                string hash = Convert.ToHexString(SHA256.HashData(pair.Value)).ToLowerInvariant();
                int expectedCount = arrays.GetProperty(pair.Key).GetProperty("count").GetInt32();
                bool countMatches = pair.Key == "heights" || pair.Key == "slopes"
                    ? expectedCount == pair.Value.Length / 4
                    : expectedCount == pair.Value.Length;
                if (hash != expected || !countMatches)
                {
                    failures++;
                    Console.Error.WriteLine($"[terrain] {pair.Key} mismatch expected={expected} actual={hash}");
                }
            }
            Console.WriteLine($"[terrain] arrays={actual.Count} cells={terrain.Types.Length} mismatches={failures}");
            return failures == 0;
        }

        private static byte[] FloatBytes(float[] values)
        {
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < bytes.Length; i += sizeof(float))
                    Array.Reverse(bytes, i, sizeof(float));
            }
            return bytes;
        }

        private static string FindRepositoryRoot(string? explicitRoot)
        {
            if (!string.IsNullOrWhiteSpace(explicitRoot))
                return Path.GetFullPath(explicitRoot);
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "data")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the repository data directory; use --data=ROOT.");
        }

        private static string? OptionValue(string[] args, string prefix)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.Ordinal))
                    return arg.Substring(prefix.Length);
            }
            return null;
        }

        private static bool HasOption(string[] args, string option)
        {
            foreach (string arg in args)
            {
                if (arg == option)
                    return true;
            }
            return false;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Echelon.Headless [--data=ROOT] [--golden=FILE] [--catalog-only|--terrain-only]");
            Console.WriteLine("Echelon.Headless --seed=N [--out=FILE] [--quiet]");
            Console.WriteLine("Echelon.Headless --snapshots [--seeds=1,8] [--detail=1,8] [--out=DIR] [--baselines=DIR]");
        }
    }
}
