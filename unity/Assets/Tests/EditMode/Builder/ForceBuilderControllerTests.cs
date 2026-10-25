using System;
using System.Collections.Generic;
using System.Linq;
using Echelon.Core;
using Echelon.Unity.UI.Builder;
using NUnit.Framework;

namespace Echelon.Unity.Tests.Builder
{
    public sealed class ForceBuilderControllerTests
    {
        private MemoryForceStore _store;
        private ForceBuilderController _controller;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryForceStore();
            _controller = BuilderTestData.Us(_store);
        }

        [Test]
        public void InitialStateUsesUsDefaultAndLiveCoreReport()
        {
            ForceRules.Report expected = ForceRules.Evaluate(
                BuilderTestData.Catalog, _controller.Current, _controller.Budget);
            Assert.That(_controller.Current.Faction, Is.EqualTo("US"));
            Assert.That(_controller.Current.Toe, Is.EqualTo("us_armor_bn_tf"));
            Assert.That(_controller.Budget, Is.EqualTo(2000));
            Assert.That(_controller.Report.Total, Is.EqualTo(expected.Total));
            Assert.That(_controller.Report.SustainMin, Is.EqualTo(expected.SustainMin));
            Assert.That(_controller.Report.ElementCount, Is.EqualTo(expected.ElementCount));
            Assert.That(_controller.CanStart, Is.True, BuilderTestData.Errors(_controller.Report));
        }

        [Test]
        public void InitialSpecIsDuplicated()
        {
            ForceSpec initial = BuilderTestData.Catalog.DefaultForce("US");
            ForceBuilderController controller = BuilderTestData.Us(initial: initial);
            Assert.That(controller.Current, Is.Not.SameAs(initial));
            Assert.That(controller.Current.ToJson(), Is.EqualTo(initial.ToJson()));
            controller.Current.Name = "changed";
            Assert.That(controller.Current.Name, Is.Not.EqualTo(initial.Name));
        }

        [Test]
        public void FactionSelectionLoadsMatchingDefault()
        {
            Assert.That(_controller.SetFaction("RU"), Is.True);
            Assert.That(_controller.Current.Faction, Is.EqualTo("RU"));
            Assert.That(_controller.Current.Toe, Is.EqualTo("ru_btg_bmp"));
            Assert.That(_controller.Current.EntryPoint, Is.EqualTo(1));
            Assert.That(_controller.Report.IsValid(), Is.True, BuilderTestData.Errors(_controller.Report));
            Assert.That(_controller.SetFaction("RU"), Is.False);
            Assert.That(_controller.SetFaction("unknown"), Is.False);
            Assert.That(_controller.SetFaction("US"), Is.True);
            Assert.That(_controller.Current.Faction, Is.EqualTo("US"));
        }

        [Test]
        public void CompanySlotsEnforceRequiredOptionalAndAllowedChoices()
        {
            Assert.That(_controller.Current.GetCompany("co_c"), Is.Null);
            Assert.That(_controller.SetCompany("co_c", "us_stryker_co"), Is.True);
            Assert.That(_controller.Current.Companies[2].Slot, Is.EqualTo("co_c"));
            Assert.That(_controller.Current.GetCompany("co_c").Template, Is.EqualTo("us_stryker_co"));
            Assert.That(_controller.SetCompany("co_c", ""), Is.True);
            Assert.That(_controller.Current.GetCompany("co_c"), Is.Null);
            Assert.That(_controller.SetCompany("co_a", ""), Is.False);
            Assert.That(_controller.SetCompany("co_a", "ru_tank_co"), Is.False);
            Assert.That(_controller.SetCompany("missing", "us_tank_co"), Is.False);
            Assert.That(_controller.SetCompany("co_b", "us_light_inf_co"), Is.True);
            Assert.That(_controller.Current.GetCompany("co_b").Platoons.Select(p => p.Template),
                Is.All.EqualTo("us_light_inf_plt"));
        }

        [Test]
        public void EveryCompanyTemplateAndNestedLoadoutCanBeSelected()
        {
            foreach (string faction in ForceBuilderController.Factions)
            {
                _controller.SetFaction(faction);
                TOETemplate toe = BuilderTestData.Catalog.GetToe(_controller.Current.Toe);
                foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
                {
                    foreach (string templateId in slot.Allowed)
                    {
                        _controller.UseMinimalForce();
                        bool changed = _controller.SetCompany(slot.Id, templateId);
                        CompanySpec company = _controller.Current.GetCompany(slot.Id);
                        Assert.That(company, Is.Not.Null, slot.Id + " / " + templateId);
                        Assert.That(changed || company.Template == templateId, Is.True);
                        Assert.That(company.Template, Is.EqualTo(templateId));
                        UnitTemplate template = BuilderTestData.Catalog.GetUnitTemplate(templateId);
                        Assert.That(company.Platoons.Count, Is.EqualTo(template.Platoons.Count));
                        for (int i = 0; i < company.Platoons.Count; ++i)
                        {
                            PlatoonSpec platoon = company.Platoons[i];
                            Assert.That(platoon.Template, Is.EqualTo(template.Platoons[i]));
                            UnitTemplate platoonTemplate = BuilderTestData.Catalog.GetUnitTemplate(platoon.Template);
                            foreach (UnitTemplate.Loadout loadout in platoonTemplate.Loadouts)
                            {
                                Assert.That(_controller.SetLoadout(platoon, loadout.Id), Is.True,
                                    platoon.Template + " / " + loadout.Id);
                                AssertLoadoutCost(platoon);
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void CompanyReplacementReturnsForeignPlatoons()
        {
            PlatoonSpec moved = _controller.Current.GetCompany("co_a").Platoons[0];
            Assert.That(_controller.MovePlatoon("co_a", 0, "co_b"), Is.True);
            Assert.That(_controller.Current.GetCompany("co_b").Platoons, Contains.Item(moved));
            Assert.That(_controller.SetCompany("co_b", "us_stryker_co"), Is.True);
            Assert.That(_controller.Current.GetCompany("co_a").Platoons, Contains.Item(moved));
            Assert.That(_controller.Current.GetCompany("co_b").Platoons, Is.Not.SupersetOf(new[] { moved }));
            Assert.That(moved.Home, Is.EqualTo("co_a"));
        }

        [Test]
        public void CrossAttachmentMovesOriginalPlatoonAndKeepsHome()
        {
            PlatoonSpec moved = _controller.Current.GetCompany("co_a").Platoons[0];
            Assert.That(_controller.MovePlatoon("co_a", 0, "co_b"), Is.True);
            Assert.That(_controller.Current.GetCompany("co_a").Platoons, Is.Not.SupersetOf(new[] { moved }));
            Assert.That(_controller.Current.GetCompany("co_b").Platoons.Last(), Is.SameAs(moved));
            Assert.That(moved.Home, Is.EqualTo("co_a"));
            Assert.That(_controller.MovePlatoon("co_a", 99, "co_b"), Is.False);
            Assert.That(_controller.MovePlatoon("co_a", 0, "co_a"), Is.False);
        }

        [Test]
        public void LoadoutsAndWavesUseCoreCostsAndDiscounts()
        {
            PlatoonSpec platoon = _controller.Current.GetCompany("co_a").Platoons[0];
            int baseCost = ForceRules.PlatoonBaseCost(BuilderTestData.Catalog, platoon);
            Assert.That(_controller.SetWave(platoon, 1), Is.True);
            Assert.That(ForceRules.PlatoonCost(BuilderTestData.Catalog, platoon),
                Is.EqualTo(BuilderTestData.Round(baseCost * 0.9)));
            Assert.That(_controller.SetWave(platoon, 2), Is.True);
            Assert.That(ForceRules.PlatoonCost(BuilderTestData.Catalog, platoon),
                Is.EqualTo(BuilderTestData.Round(baseCost * 0.8)));
            Assert.That(_controller.SetWave(platoon, 3), Is.False);
            Assert.That(_controller.SetWave(platoon, 0), Is.True);
            Assert.That(_controller.SetLoadout(platoon, "tusk"), Is.True);
            AssertLoadoutCost(platoon);
            Assert.That(ForceRules.PlatoonCost(BuilderTestData.Catalog, platoon), Is.EqualTo(328));
            Assert.That(_controller.SetLoadout(platoon, "missing"), Is.False);
            Assert.That(platoon.Loadout, Is.EqualTo("tusk"));
        }

        [Test]
        public void OrganicToggleAddsAndRemovesExactlyOne()
        {
            _controller.UseMinimalForce();
            Assert.That(_controller.SetOrganic("us_scout_plt", false), Is.True);
            Assert.That(_controller.Current.Organic.Any(p => p.Template == "us_scout_plt"), Is.False);
            Assert.That(_controller.SetOrganic("us_scout_plt", false), Is.False);
            Assert.That(_controller.SetOrganic("us_scout_plt", true), Is.True);
            Assert.That(_controller.Current.Organic.Count(p => p.Template == "us_scout_plt"), Is.EqualTo(1));
            Assert.That(_controller.SetOrganic("us_scout_plt", true), Is.False);
            Assert.That(_controller.SetOrganic("ru_recon_plt", true), Is.False);
        }

        [Test]
        public void EveryOrganicAndAttachmentLoadoutCanBeEdited()
        {
            foreach (string faction in ForceBuilderController.Factions)
            {
                _controller.SetFaction(faction);
                _controller.UseMinimalForce();
                foreach (PlatoonSpec platoon in _controller.Current.Organic)
                {
                    UnitTemplate template = BuilderTestData.Catalog.GetUnitTemplate(platoon.Template);
                    foreach (UnitTemplate.Loadout loadout in template.Loadouts)
                    {
                        Assert.That(_controller.SetLoadout(platoon, loadout.Id), Is.True);
                        AssertLoadoutCost(platoon);
                    }
                }

                foreach (string templateId in _controller.AllowedAttachments())
                {
                    _controller.UseMinimalForce();
                    Assert.That(_controller.AddAttachment(templateId), Is.True, templateId);
                    PlatoonSpec attachment = _controller.Current.Attachments.Single();
                    UnitTemplate template = BuilderTestData.Catalog.GetUnitTemplate(templateId);
                    foreach (UnitTemplate.Loadout loadout in template.Loadouts)
                    {
                        Assert.That(_controller.SetLoadout(attachment, loadout.Id), Is.True);
                        AssertLoadoutCost(attachment);
                    }
                    Assert.That(_controller.SetWave(attachment, 2), Is.True);
                    Assert.That(attachment.Wave, Is.EqualTo(2));
                }
            }
        }

        [Test]
        public void AttachmentLimitsKeepHelicopterSlotSeparate()
        {
            foreach (string faction in ForceBuilderController.Factions)
            {
                _controller.SetFaction(faction);
                _controller.UseMinimalForce();
                string ground = faction == "US" ? "us_engineer_plt" : "ru_engineer_plt";
                string aviation = faction == "US" ? "us_aviation_sec" : "ru_aviation_sec";
                string otherFaction = faction == "US" ? "ru_aviation_sec" : "us_aviation_sec";
                for (int i = 0; i < 4; ++i)
                    Assert.That(_controller.AddAttachment(ground), Is.True);
                Assert.That(_controller.CanAddAttachment(ground), Is.False);
                Assert.That(_controller.CanAddAttachment(aviation), Is.True);
                Assert.That(_controller.AddAttachment(aviation), Is.True);
                Assert.That(_controller.Current.Attachments.Count, Is.EqualTo(5));
                Assert.That(_controller.CanAddAttachment(aviation), Is.False);
                Assert.That(_controller.AddAttachment(aviation), Is.False);
                Assert.That(_controller.AddAttachment(otherFaction), Is.False);
                Assert.That(_controller.RemoveAttachment(0), Is.True);
                Assert.That(_controller.CanAddAttachment(ground), Is.True);
                Assert.That(_controller.CanAddAttachment(aviation), Is.False);
                Assert.That(_controller.RemoveAttachment(-1), Is.False);
            }
        }

        [Test]
        public void SupportPurchasesClampAndChangeCoreTotal()
        {
            foreach (string faction in ForceBuilderController.Factions)
            {
                _controller.SetFaction(faction);
                _controller.UseMinimalForce();
                TOETemplate toe = BuilderTestData.Catalog.GetToe(_controller.Current.Toe);
                int initial = _controller.Report.Total;
                Assert.That(_controller.SetArtilleryMissions(toe.MaxArtillery + 99), Is.True);
                Assert.That(_controller.Current.ArtilleryMissions, Is.EqualTo(toe.MaxArtillery));
                Assert.That(_controller.Report.Total,
                    Is.EqualTo(initial + toe.MaxArtillery * toe.ArtilleryMissionCost));
                Assert.That(_controller.SetCasSorties(-10), Is.False);
                Assert.That(_controller.SetCasSorties(toe.MaxCas + 1), Is.True);
                Assert.That(_controller.Current.CasSorties, Is.EqualTo(toe.MaxCas));
                Assert.That(_controller.Report.Total,
                    Is.EqualTo(initial + toe.MaxArtillery * toe.ArtilleryMissionCost + toe.MaxCas * toe.CasSortieCost));
                Assert.That(_controller.SetArtilleryMissions(0), Is.True);
                Assert.That(_controller.SetCasSorties(0), Is.True);
            }
        }

        [Test]
        public void EntrySelectionUsesScenarioSideAndClamps()
        {
            ScenarioDef.SideSetup blue = BuilderTestData.Scenario.Sides["blue"];
            Assert.That(_controller.SetEntryPoint(1), Is.True);
            Assert.That(ForceRules.ResolveEntryPoint(blue, _controller.Current), Is.EqualTo(new SimVec2(3400, 0)));
            Assert.That(_controller.SetEntryPoint(99), Is.False);
            Assert.That(_controller.SetEntryPoint(-5), Is.True);
            Assert.That(_controller.Current.EntryPoint, Is.EqualTo(0));
            _controller.SetFaction("RU");
            ScenarioDef.SideSetup red = BuilderTestData.Scenario.Sides["red"];
            Assert.That(_controller.SetEntryPoint(0), Is.True);
            Assert.That(ForceRules.ResolveEntryPoint(red, _controller.Current), Is.EqualTo(new SimVec2(600, 4000)));
        }

        [Test]
        public void DefaultAndMinimalPresetsDiffer()
        {
            Assert.That(_controller.Current.Attachments, Is.Not.Empty);
            Assert.That(_controller.Current.ArtilleryMissions, Is.GreaterThan(0));
            Assert.That(_controller.UseMinimalForce(), Is.True);
            TOETemplate toe = BuilderTestData.Catalog.GetToe(_controller.Current.Toe);
            Assert.That(_controller.Current.Companies.Count, Is.EqualTo(toe.CompanySlots.Count(slot => slot.Required)));
            Assert.That(_controller.Current.Attachments, Is.Empty);
            Assert.That(_controller.Current.ArtilleryMissions, Is.Zero);
            Assert.That(_controller.Current.CasSorties, Is.Zero);
            Assert.That(_controller.Current.Organic.Count, Is.EqualTo(toe.Organic.Count));
            ForceRules.Report expected = ForceRules.Evaluate(BuilderTestData.Catalog, _controller.Current,
                _controller.Budget);
            Assert.That(_controller.Report.Total, Is.EqualTo(expected.Total));
            Assert.That(_controller.Report.SustainMin, Is.EqualTo(expected.SustainMin));
            Assert.That(_controller.UseDefaultForce(), Is.True);
            Assert.That(_controller.Current.Attachments, Is.Not.Empty);
            Assert.That(_controller.Current.ArtilleryMissions, Is.GreaterThan(0));
        }

        [Test]
        public void LiveReportIncludesBudgetErrorsAdHocWarningsAndSustain()
        {
            _controller.SetArtilleryMissions(6);
            Assert.That(_controller.Report.Total, Is.GreaterThan(_controller.Budget));
            Assert.That(_controller.Report.Errors.Any(error => error.Contains("예산 초과")), Is.True);
            Assert.That(_controller.CanStart, Is.False);
            Assert.That(_controller.Report.SustainMin,
                Is.EqualTo(ForceRules.SustainMinutes(BuilderTestData.Catalog, _controller.Current)));

            _controller.UseMinimalForce();
            _controller.MovePlatoon("co_a", 0, "co_b");
            _controller.MovePlatoon("co_a", 0, "co_b");
            Assert.That(_controller.Current.GetCompany("co_a").Platoons.Count, Is.EqualTo(1));
            Assert.That(_controller.Current.GetCompany("co_b").Platoons.Count, Is.EqualTo(5));
            Assert.That(_controller.Report.Warnings, Is.Not.Empty);
            Assert.That(_controller.Report.AdHoc, Contains.Item("co_a"));
            Assert.That(_controller.Report.AdHoc, Contains.Item("co_b"));
        }

        [Test]
        public void NamedSaveLoadRoundTripsWithoutAliasing()
        {
            PlatoonSpec platoon = _controller.Current.GetCompany("co_a").Platoons[0];
            _controller.SetLoadout(platoon, "tusk");
            _controller.SetWave(platoon, 2);
            Assert.That(_controller.Save("내 전투단"), Is.True, _controller.LastNotice);
            string path = _store.ListSaved().Single();
            Assert.That(ForceBuilderController.DisplayNameForSaved(path), Is.EqualTo("내 전투단"));
            string savedJson = _controller.Current.ToJson();
            _controller.SetFaction("RU");
            Assert.That(_controller.LoadSaved(path), Is.True, _controller.LastNotice);
            Assert.That(_controller.Current.Faction, Is.EqualTo("US"));
            Assert.That(_controller.Current.ToJson(), Is.EqualTo(savedJson));
            Assert.That(_controller.Current, Is.Not.SameAs(_store.LoadSpec(path)));
            Assert.That(_controller.LoadSaved("missing"), Is.False);
            Assert.That(_controller.LastNoticeSucceeded, Is.False);
        }

        [Test]
        public void StartAndBackCallbacksValidateAndDuplicate()
        {
            ForceSpec started = null;
            int starts = 0;
            int backs = 0;
            _controller.StartRequested += spec => { started = spec; ++starts; };
            _controller.BackRequested += () => ++backs;
            Assert.That(_controller.RequestStart(), Is.True, BuilderTestData.Errors(_controller.Report));
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(started, Is.Not.SameAs(_controller.Current));
            Assert.That(started.ToJson(), Is.EqualTo(_controller.Current.ToJson()));
            started.Name = "callback mutation";
            Assert.That(_controller.Current.Name, Is.Not.EqualTo(started.Name));
            _controller.RequestBack();
            Assert.That(backs, Is.EqualTo(1));

            _controller.SetArtilleryMissions(6);
            Assert.That(_controller.RequestStart(), Is.False);
            Assert.That(starts, Is.EqualTo(1));
        }

        [Test]
        public void ChangedEventFiresOnlyForEffectiveEdits()
        {
            int changes = 0;
            _controller.Changed += () => ++changes;
            PlatoonSpec platoon = _controller.Current.GetCompany("co_a").Platoons[0];
            _controller.SetWave(platoon, 0);
            Assert.That(changes, Is.Zero);
            _controller.SetWave(platoon, 1);
            Assert.That(changes, Is.EqualTo(1));
            _controller.SetName("name only");
            Assert.That(changes, Is.EqualTo(1));
            _controller.SetWave(new PlatoonSpec { Template = "us_tank_plt" }, 2);
            Assert.That(changes, Is.EqualTo(1));
        }

        private static void AssertLoadoutCost(PlatoonSpec platoon)
        {
            DataCatalog catalog = BuilderTestData.Catalog;
            UnitTemplate template = catalog.GetUnitTemplate(platoon.Template);
            int expected = 0;
            foreach (string elementId in template.ElementsFor(platoon.Loadout))
                expected += catalog.GetElement(elementId).Cost;
            foreach (string elementId in template.PassengersFor(platoon.Loadout))
                expected += catalog.GetElement(elementId).Cost;
            Assert.That(ForceRules.PlatoonBaseCost(catalog, platoon), Is.EqualTo(expected), platoon.Template);
        }
    }
}
