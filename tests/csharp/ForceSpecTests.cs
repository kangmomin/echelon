using System;
using System.IO;
using System.Linq;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class ForceSpecTests
    {
        [Test]
        public void JsonRoundTripPreservesCrossAttachmentLoadoutDeployAndSupport()
        {
            ForceSpec spec = DataTestAssets.Us();
            Assert.That(spec.MovePlatoon("co_a", 1, "co_b"), Is.True);
            spec.Companies[0].Platoons[0].Loadout = "tusk";
            spec.Deploy["co:co_a"] = new DeploymentPose(1200, 500, 0.3);
            spec.CasSorties = 1;
            spec.EntryPoint = 1;

            string text = spec.ToJson();
            ForceSpec? back = ForceSpec.FromJson(text);
            Assert.That(back, Is.Not.Null);
            Assert.That(back!.Companies[1].Platoons, Has.Count.EqualTo(4));
            Assert.That(back.Companies[1].Platoons[3].Home, Is.EqualTo("co_a"));
            Assert.That(back.Companies[0].Platoons[0].Loadout, Is.EqualTo("tusk"));
            Assert.That(back.Deploy["co:co_a"].X, Is.EqualTo(1200.0));
            Assert.That(back.Deploy["co:co_a"].North, Is.EqualTo(500.0));
            Assert.That(back.Deploy["co:co_a"].Heading, Is.EqualTo(0.3).Within(0.001));
            Assert.That(back.CasSorties, Is.EqualTo(1));
            Assert.That(back.EntryPoint, Is.EqualTo(1));
            Assert.That(back.ToJObject()["format"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(JToken.DeepEquals(back.ToJObject(), spec.ToJObject()), Is.True);
        }

        [Test]
        public void BrokenOrNonObjectJsonReturnsNull()
        {
            Assert.That(ForceSpec.FromJson("{ 깨진"), Is.Null);
            Assert.That(ForceSpec.FromJson("[]"), Is.Null);
        }

        [Test]
        public void ValidatingReaderAccumulatesFormatAndNestedErrors()
        {
            JObject raw = DataTestAssets.Us().ToJObject();
            raw["format"] = 2;
            raw["companies"] = "invalid";
            raw["support"]!["artillery_missions"] = "many";
            raw["deploy"] = new JObject { ["cp"] = "invalid" };
            raw["entry_point"] = -4;
            DefReader reader = new DefReader { Context = "synthetic-force" };
            ForceSpec parsed = ForceSpec.FromJObject(raw, reader);
            Assert.That(parsed.EntryPoint, Is.Zero);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "format 은 1"), Is.True);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "companies"), Is.True);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "artillery_missions"), Is.True);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "deploy.cp"), Is.True);
            Assert.That(reader.Errors, Has.All.Contains("synthetic-force"));
        }

        [Test]
        public void FormatOneParserClampsWaveSupportAndEntryValues()
        {
            JObject raw = new JObject
            {
                ["format"] = 1,
                ["companies"] = new JArray(
                    new JObject
                    {
                        ["slot"] = "co_a",
                        ["template"] = "us_tank_co",
                        ["platoons"] = new JArray(new JObject { ["template"] = "us_tank_plt", ["wave"] = 99 })
                    }),
                ["support"] = new JObject { ["artillery_missions"] = -4, ["cas_sorties"] = -2 },
                ["deploy"] = new JObject { ["co:co_a"] = new JArray(10, 20, 0.5, 99) },
                ["entry_point"] = -3
            };
            ForceSpec spec = ForceSpec.FromJObject(raw);
            Assert.That(spec.Companies[0].Platoons[0].Wave, Is.EqualTo(2));
            Assert.That(spec.ArtilleryMissions, Is.Zero);
            Assert.That(spec.CasSorties, Is.Zero);
            Assert.That(spec.EntryPoint, Is.Zero);
            Assert.That(spec.Deploy["co:co_a"], Is.EqualTo(new DeploymentPose(10, 20, 0.5)));
        }

        [Test]
        public void DuplicateSpecIsDeepAndCompanyWaveUsesEarliestPlatoon()
        {
            ForceSpec spec = DataTestAssets.Us();
            ForceSpec copy = spec.DuplicateSpec();
            copy.Companies[0].Platoons[0].Template = "changed";
            copy.Organic[0].Wave = 2;
            copy.Deploy["cp"] = new DeploymentPose(1, 2, 3);
            Assert.That(spec.Companies[0].Platoons[0].Template, Is.Not.EqualTo("changed"));
            Assert.That(spec.Organic[0].Wave, Is.Zero);
            Assert.That(spec.Deploy, Is.Empty);
            Assert.That(spec.Companies[0].Wave(), Is.Zero);
            foreach (PlatoonSpec platoon in spec.Companies[0].Platoons)
            {
                platoon.Wave = 2;
            }

            spec.Companies[0].Platoons[1].Wave = 1;
            Assert.That(spec.Companies[0].Wave(), Is.EqualTo(1));
            Assert.That(new CompanySpec().Wave(), Is.Zero);
        }

        [Test]
        public void FileForceStoreRoundTripsListsAndConfinesPaths()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "EchelonForceTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                IForceStore store = new FileForceStore(basePath);
                ForceSpec spec = DataTestAssets.Us();
                store.Save(spec, "b.json");
                store.Save(spec, "a.json");
                Assert.That(store.ListSaved().Select(Path.GetFileName), Is.EqualTo(new[] { "a.json", "b.json" }));
                ForceSpec? loaded = store.LoadSpec(store.ListSaved()[0]);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(JToken.DeepEquals(loaded!.ToJObject(), spec.ToJObject()), Is.True);
                Assert.That(store.PathFor(""), Is.EqualTo(Path.Combine(basePath, "tf.json")));
                Assert.That(store.PathFor("bad/name.json"), Does.StartWith(basePath));
                Assert.That(store.PathFor("bad/name.json"), Does.Not.Contain("bad/"));

                File.WriteAllText(Path.Combine(basePath, "broken.json"), "{ broken");
                Assert.That(store.LoadSpec("broken.json"), Is.Null);
                string outside = Path.GetFullPath(Path.Combine(basePath, "..", "outside.json"));
                Assert.Throws<UnauthorizedAccessException>(() => store.LoadSpec(outside));
            }
            finally
            {
                if (Directory.Exists(basePath))
                {
                    Directory.Delete(basePath, true);
                }
            }
        }
    }
}
