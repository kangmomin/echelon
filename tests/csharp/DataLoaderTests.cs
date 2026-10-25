using System.Linq;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class DataLoaderTests
    {
        [Test]
        public void ProjectDataLoadsWithoutErrorsAndPreservesCounts()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            Assert.That(DataTestAssets.Join(catalog.Errors), Is.Empty);
            Assert.That(catalog.IsValid(), Is.True);
            Assert.That(catalog.Weapons, Has.Count.EqualTo(55));
            Assert.That(catalog.Elements, Has.Count.EqualTo(57));
            Assert.That(catalog.UnitTemplates, Has.Count.EqualTo(63));
            Assert.That(catalog.UnitTemplates.Values.Count(template => template.Echelon == "platoon"), Is.EqualTo(43));
            Assert.That(catalog.UnitTemplates.Values.Count(template => template.Echelon == "company"), Is.EqualTo(20));
            Assert.That(catalog.Formations, Has.Count.EqualTo(14));
            Assert.That(catalog.ToeTemplates, Has.Count.EqualTo(2));
            Assert.That(catalog.Maps, Has.Count.EqualTo(1));
            Assert.That(catalog.Scenarios, Has.Count.EqualTo(1));
            Assert.That(catalog.Forces, Has.Count.EqualTo(2));
        }

        [Test]
        public void LoadedElementFieldsAreTyped()
        {
            ElementDef? tank = DataTestAssets.Catalog.GetElement("us_m1a2_sepv3");
            Assert.That(tank, Is.Not.Null);
            Assert.That(tank!.ElementClass, Is.EqualTo("tank"));
            Assert.That(tank.GetArmor("front"), Is.EqualTo(900.0));
            Assert.That(tank.Ammo["m256_apfsds"], Is.EqualTo(22));
            Assert.That(tank.Weapons, Contains.Item("m240_coax"));
        }

        [Test]
        public void LoadedWeaponFieldsAndCalculationsAreTyped()
        {
            WeaponDef weapon = DataTestAssets.Catalog.GetWeapon("m256_apfsds")!;
            Assert.That(weapon.Targets, Contains.Item("vehicle"));
            Assert.That(weapon.CanTarget("vehicle"), Is.True);
            Assert.That(weapon.CanTarget("infantry"), Is.False);
            Assert.That(weapon.IntervalSec(), Is.EqualTo(10.0).Within(0.0001));
            Assert.That(weapon.PenetrationAt(1000.0), Is.EqualTo(807.5).Within(0.0001));
            Assert.That(weapon.AccuracyAt(weapon.RangeMinM), Is.EqualTo(0.75).Within(0.0001));
            Assert.That(weapon.AccuracyAt(weapon.RangeMaxM), Is.EqualTo(0.35).Within(0.0001));
        }

        [Test]
        public void ScenarioAndMapPreserveD043CenteredFoxData()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ScenarioDef? scenario = catalog.GetScenario("skirmish_01");
            Assert.That(scenario, Is.Not.Null);
            Assert.That(scenario!.Sides, Has.Count.EqualTo(2));
            Assert.That(scenario.Sides["blue"].Toe, Is.EqualTo("us_armor_bn_tf"));
            Assert.That(scenario.ControlPoints, Has.Count.EqualTo(4));
            Assert.That(scenario.ControlPoints[0].Id, Is.EqualTo("cp_fox"));
            Assert.That(scenario.ControlPoints[0].Pos, Is.EqualTo(new SimVec2(2000, 2000)));
            Assert.That(scenario.CollapseThreshold, Is.EqualTo(0.25).Within(0.0001));
            Assert.That(scenario.Objectives[0].Point, Is.EqualTo("cp_fox"));

            MapDef map = catalog.GetMap(scenario.MapId)!;
            MapDef.Feature fox = map.Towns.Single(town => town.Name == "FOX");
            Assert.That(MapRect.FromCorners(fox.Points[0], fox.Points[2]).Contains(new SimVec2(2000, 2000)), Is.True);
            Assert.That(fox.Points.Average(point => point.X), Is.EqualTo(2005.0).Within(0.001));
            Assert.That(fox.Points.Average(point => point.Y), Is.EqualTo(2000.0).Within(0.001));
            Assert.That(map.Roads.Any(road => road.Points.Contains(new SimVec2(2000, 2000))), Is.True);
        }

        [Test]
        public void ExpandedEngineerAndHelicopterContentIsComposed()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            UnitTemplate usEngineers = catalog.GetUnitTemplate("us_engineer_plt")!;
            Assert.That(usEngineers.Kind, Is.EqualTo("engineer"));
            Assert.That(usEngineers.Elements, Has.Count.EqualTo(3));
            Assert.That(usEngineers.Passengers, Has.Count.EqualTo(2));
            Assert.That(catalog.GetElement("us_m1150_abv")!.ElementClass, Is.EqualTo("eng"));

            UnitTemplate usAviation = catalog.GetUnitTemplate("us_aviation_sec")!;
            Assert.That(usAviation.Kind, Is.EqualTo("aviation"));
            Assert.That(usAviation.Elements, Is.EqualTo(new[] { "us_ah64e", "us_ah64e" }));
            Assert.That(catalog.GetElement("us_ah64e")!.ElementClass, Is.EqualTo("heli"));

            TOETemplate usToe = catalog.GetToe("us_armor_bn_tf")!;
            Assert.That(usToe.AttachmentSlots[0].Allowed, Contains.Item("us_engineer_plt"));
            Assert.That(usToe.AttachmentSlots[1].Allowed, Contains.Item("us_aviation_sec"));
            Assert.That(usToe.AttachmentSlots[1].MaxCount, Is.EqualTo(1));
            Assert.That(catalog.GetToe("ru_btg_bmp")!.AttachmentSlots[0].Allowed, Contains.Item("ru_engineer_plt"));
            Assert.That(catalog.GetUnitTemplate("ru_aviation_sec")!.Elements, Has.Count.EqualTo(2));
        }

        [Test]
        public void MissingRequiredFieldsAreReported()
        {
            DefReader reader = new DefReader();
            ElementDef.FromJObject(new JObject(), reader);
            Assert.That(reader.HasErrors(), Is.True);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "'id'"), Is.True);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "'armor'"), Is.True);
        }

        [Test]
        public void FormationSlotCountMismatchIsReported()
        {
            JObject raw = new JObject
            {
                ["id"] = "bad",
                ["echelon"] = "platoon",
                ["slots"] = new JObject { ["4"] = new JArray(new JArray(0, 0), new JArray(1, 1)) },
                ["mods"] = new JObject
                {
                    ["speed"] = 1, ["front_fire"] = 1, ["flank_security"] = 1,
                    ["arty_vuln"] = 1, ["control"] = 1, ["transition_sec"] = 10
                }
            };
            DefReader reader = new DefReader();
            FormationDef.FromJObject(raw, reader);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "slots[4]"), Is.True);
        }

        [Test]
        public void UnknownWeaponAndLoadoutTargetFailReferenceChecks()
        {
            DataCatalog manual = new DataCatalog();
            ElementDef element = new ElementDef { Id = "ghost" };
            element.Weapons.Add("no_such_gun");
            manual.Elements[element.Id] = element;
            DefReader reader = new DefReader();
            DataLoader.ValidateReferences(manual, reader);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "no_such_gun"), Is.True);

            DataCatalog catalog = DataTestAssets.FreshCatalog();
            catalog.GetUnitTemplate("us_tank_plt")!.GetLoadout("tusk")!.Replace["us_m1a2_sepv3"] = "no_such_element";
            DefReader loadoutReader = new DefReader();
            DataLoader.ValidateReferences(catalog, loadoutReader);
            Assert.That(DataTestAssets.HasMessage(loadoutReader.Errors, "no_such_element"), Is.True);
        }

        [Test]
        public void UnknownScenarioToeFailsReferenceCheck()
        {
            DataCatalog catalog = DataTestAssets.FreshCatalog();
            catalog.GetScenario("skirmish_01")!.Sides["red"].Toe = "no_such_toe";
            DefReader reader = new DefReader();
            DataLoader.ValidateReferences(catalog, reader);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "no_such_toe"), Is.True);
        }

        [Test]
        public void MalformedAndNonObjectJsonAreReportedWithoutCrashing()
        {
            InMemoryCatalogSource malformed = new InMemoryCatalogSource();
            malformed.Add(CatalogCategories.Weapons, "broken.json", "{ \"id\": \"x\", ");
            malformed.Add(CatalogCategories.Formations, "formations.json", "{ \"formations\": [] }");
            DataCatalog first = DataLoader.LoadCatalog(malformed);
            Assert.That(first.IsValid(), Is.False);
            Assert.That(DataTestAssets.HasMessage(first.Errors, "JSON 파싱 오류"), Is.True);

            InMemoryCatalogSource nonObject = new InMemoryCatalogSource();
            nonObject.Add(CatalogCategories.Weapons, "array.json", "[]");
            nonObject.Add(CatalogCategories.Formations, "formations.json", "{ \"formations\": [] }");
            DataCatalog second = DataLoader.LoadCatalog(nonObject);
            Assert.That(second.IsValid(), Is.False);
            Assert.That(DataTestAssets.HasMessage(second.Errors, "최상위가 객체"), Is.True);
        }

        [Test]
        public void DuplicateIdsAreRejectedWhileOtherFilesStillLoad()
        {
            InMemoryCatalogSource source = new InMemoryCatalogSource();
            string weapon = DataTestAssets.ReadData(CatalogCategories.Weapons, "m2_hmg.json");
            source.Add(CatalogCategories.Weapons, "a.json", weapon);
            source.Add(CatalogCategories.Weapons, "b.json", weapon);
            source.Add(CatalogCategories.Formations, "formations.json", "{ \"formations\": [] }");
            DataCatalog catalog = DataLoader.LoadCatalog(source);
            Assert.That(catalog.Weapons, Has.Count.EqualTo(1));
            Assert.That(DataTestAssets.HasMessage(catalog.Errors, "중복"), Is.True);
        }

        [Test]
        public void DefaultForceIsClonedAndToeFallbackRemainsValid()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ForceSpec first = catalog.DefaultForce("US")!;
            first.Companies[0].Platoons[0].Loadout = "tusk";
            ForceSpec second = catalog.DefaultForce("US")!;
            Assert.That(second.Companies[0].Platoons[0].Loadout, Is.Empty);
            Assert.That(catalog.FormationsFor("platoon").First().Id, Is.EqualTo("column"));

            DataCatalog fallback = DataTestAssets.FreshCatalog();
            fallback.Forces.Clear();
            ForceSpec generated = fallback.DefaultForce("RU")!;
            Assert.That(generated.Companies, Has.Count.EqualTo(3));
            Assert.That(ForceRules.Evaluate(fallback, generated, 2000).IsValid(), Is.True);
        }
    }
}
