using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Echelon.Core;
using Newtonsoft.Json;

namespace Echelon.Headless
{
    internal static class BattleRunner
    {
        public static int Run(DataCatalog catalog, string[] args)
        {
            int seed = int.Parse(Option(args, "--seed=") ?? "1");
            string? output = Option(args, "--out=");
            bool quiet = Flag(args, "--quiet");
            string? bluePath = Option(args, "--blue=");
            string? redPath = Option(args, "--red=");
            ForceSpec blue = bluePath == null ? catalog.DefaultForce("US")! : LoadForce(bluePath);
            ForceSpec red = redPath == null ? catalog.DefaultForce("RU")! : LoadForce(redPath);
            if (!catalog.IsValid())
            {
                Console.Error.WriteLine("[battle] Catalog is invalid");
                return 1;
            }
            var battle = BattleSetup.Skirmish(catalog, blue, red, "skirmish_01", seed,
                new[] { "blue", "red" });
            try
            {
                battle.BeginBattle();
                var watch = Stopwatch.StartNew();
                int limit = (int)Math.Round(battle.Scenario.TimeLimitMin * 60.0 * SimConst.TickRate,
                    MidpointRounding.AwayFromZero) + 100;
                while (!battle.HasResult && battle.Tick() < limit)
                {
                    battle.StepTick();
                    if (battle.Tick() % 1200 == 0)
                    {
                        battle.World.Events.Drain();
                        if (!quiet)
                            PrintProgress(battle, watch.Elapsed.TotalSeconds);
                    }
                }
                if (!battle.HasResult)
                {
                    Console.Error.WriteLine("[battle] Did not finish before the tick limit");
                    return 1;
                }
                Dictionary<string, object> aar = battle.Stats.Summary();
                aar["real_sec"] = watch.Elapsed.TotalSeconds;
                aar["seed"] = battle.World.Rng.SeedValue;
                string json = JsonConvert.SerializeObject(aar, Formatting.Indented);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    string fullPath = Path.GetFullPath(output);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    File.WriteAllText(fullPath, json, new UTF8Encoding(false));
                    Console.WriteLine("AAR -> " + fullPath);
                }
                else
                {
                    Console.WriteLine(json);
                }
                Console.WriteLine($"[result] winner={battle.Result["winner"]} reason={battle.Result["reason"]} " +
                    $"t={battle.World.GetSimTimeSec():F0}s real={watch.Elapsed.TotalSeconds:F1}s");
                return 0;
            }
            finally
            {
                battle.Dispose();
            }
        }

        private static ForceSpec LoadForce(string path)
        {
            string fullPath = Path.GetFullPath(path);
            var store = new FileForceStore(Path.GetDirectoryName(fullPath)!);
            return store.LoadSpec(fullPath) ?? throw new InvalidDataException("Cannot load force: " + fullPath);
        }

        private static void PrintProgress(Battle battle, double realSeconds)
        {
            var points = new List<string>();
            foreach (ScoringSystem.PointState point in battle.Scoring.Points)
                points.Add(point.Def.Id.TrimStart('c', 'p', '_') + ":" + point.Owner);
            Console.WriteLine($"[{battle.Tick() / 600:00}:00] VP blue {battle.Scoring.Vp["blue"]:F1} red {battle.Scoring.Vp["red"]:F1} " +
                $"force blue {battle.Scoring.ValueRatio("blue") * 100.0:F0}% red {battle.Scoring.ValueRatio("red") * 100.0:F0}% " +
                $"{string.Join(" ", points)} | {realSeconds:F1}s");
        }

        private static string? Option(string[] args, string prefix)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.Ordinal))
                    return arg.Substring(prefix.Length);
            }
            return null;
        }

        private static bool Flag(string[] args, string flag)
        {
            foreach (string arg in args)
            {
                if (arg == flag)
                    return true;
            }
            return false;
        }
    }
}
