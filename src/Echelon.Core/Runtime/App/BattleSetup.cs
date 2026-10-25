using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public static class BattleSetup
    {
        public static readonly SimVec2 TutorialStart = new SimVec2(750.0, 650.0);
        public static readonly SimVec2 TutorialWaypoint = new SimVec2(900.0, 1500.0);
        public static readonly SimVec2 TutorialEnemy = new SimVec2(1150.0, 2050.0);
        private static readonly SimVec2 OffMapPark = new SimVec2(3900.0, 3950.0);

        public static Battle Sandbox(DataCatalog catalog, int seedValue = -1)
        {
            Battle b = Battle.Create(catalog, "skirmish_01", seedValue);
            TaskForce blue = b.Factory.CreateTaskForce("blue", catalog.GetToe("us_armor_bn_tf")!,
                new SimVec2(2000.0, 200.0), 0.0);
            TaskForce red = b.Factory.CreateTaskForce("red", catalog.GetToe("ru_btg_bmp")!,
                new SimVec2(2000.0, 3850.0), Math.PI);
            blue.Companies.Add(b.Factory.SpawnCompany(catalog.GetUnitTemplate("us_tank_co")!, "blue",
                new SimVec2(2700.0, 500.0), 0.0, blue, 0));
            blue.Companies.Add(b.Factory.SpawnCompany(catalog.GetUnitTemplate("us_mech_co")!, "blue",
                new SimVec2(1100.0, 450.0), 0.0, blue, 1));
            blue.Organic.Add(b.Factory.SpawnPlatoon(catalog.GetUnitTemplate("us_scout_plt")!, "blue",
                new SimVec2(1900.0, 800.0), 0.0, blue));
            red.Companies.Add(b.Factory.SpawnCompany(catalog.GetUnitTemplate("ru_tank_co")!, "red",
                new SimVec2(2100.0, 3300.0), Math.PI, red, 0));
            red.Companies.Add(b.Factory.SpawnCompany(catalog.GetUnitTemplate("ru_msr_co_bmp3")!, "red",
                new SimVec2(1000.0, 3200.0), Math.PI, red, 1));
            return b;
        }

        public static Battle Tutorial(DataCatalog catalog)
        {
            Battle b = Battle.Create(catalog, "skirmish_01", 20261023);
            TaskForce blue = b.Factory.CreateTaskForce("blue", catalog.GetToe("us_armor_bn_tf")!,
                new SimVec2(500.0, 250.0), 0.0);
            blue.ArtilleryMissions = 1;
            blue.CasSorties = 1;
            blue.Companies.Add(b.Factory.SpawnCompany(catalog.GetUnitTemplate("us_tank_co")!, "blue",
                TutorialStart, 0.0, blue, 0));
            blue.Organic.Add(b.Factory.SpawnPlatoon(catalog.GetUnitTemplate("us_mortar_plt")!, "blue",
                new SimVec2(600.0, 380.0), 0.0, blue));
            TaskForce red = b.Factory.CreateTaskForce("red", catalog.GetToe("ru_btg_bmp")!,
                new SimVec2(3700.0, 3850.0), Math.PI);
            Platoon enemy = b.Factory.SpawnPlatoon(catalog.GetUnitTemplate("ru_msr_plt_bmp3")!, "red",
                OffMapPark, Math.PI, red);
            enemy.Phase = 99;
            red.Organic.Add(enemy);
            b.Factory.SetOffMap(enemy, OffMapPark, Math.PI);
            return b;
        }

        public static void RevealPlatoon(Battle b, Platoon p, SimVec2 pos, double heading)
        {
            p.OnMap = true;
            foreach (Element e in p.Elements)
            {
                e.OnMap = true;
            }

            p.Guide.Reset(pos, heading);
            b.Factory.PlaceElements(p);
            p.Posture = "idle";
        }

        public static Battle Skirmish(DataCatalog catalog, ForceSpec blue, ForceSpec red,
            string scenarioId = "skirmish_01", int seedValue = -1, IReadOnlyList<string>? aiSides = null)
        {
            Battle b = Battle.Create(catalog, scenarioId, seedValue);
            b.Factory.BuildTaskForce(blue, "blue");
            b.Factory.BuildTaskForce(red, "red");
            List<string> configuredSides = aiSides != null ? new List<string>(aiSides) : new List<string> { "red" };
            b.TfAi.Sides.Clear();
            b.TfAi.Sides.AddRange(configuredSides);
            b.Deploying = true;
            b.World.SetPaused(true);
            return b;
        }
    }
}
