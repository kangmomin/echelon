using System;
using System.Linq;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class ForceRulesTests
    {
        [Test]
        public void DefaultUsForceFitsBudgetAndHasSustain()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ForceSpec spec = DataTestAssets.Us();
            ForceRules.Report report = ForceRules.Evaluate(catalog, spec, 2000);
            Assert.That(report.IsValid(), Is.True, DataTestAssets.Join(report.Errors));
            Assert.That(report.Total, Is.InRange(1900, 2000));
            Assert.That(report.SustainMin, Is.GreaterThan(5.0));
            Assert.That(report.ElementCount, Is.GreaterThan(0));
            Assert.That(report.Lines.Sum(line => line.Cost), Is.EqualTo(report.Total));
            Assert.That(spec.Companies, Has.Count.EqualTo(2));
            Assert.That(spec.Organic.Count + spec.Attachments.Count, Is.EqualTo(5));

            int reserveElements = ForceRules.AllPlatoons(spec).Where(platoon => platoon.Wave > 0)
                .Sum(platoon => catalog.GetUnitTemplate(platoon.Template)!.ElementsFor(platoon.Loadout).Count +
                                catalog.GetUnitTemplate(platoon.Template)!.PassengersFor(platoon.Loadout).Count);
            Assert.That(reserveElements, Is.EqualTo(8));
        }

        [Test]
        public void BudgetAndCompanyConstraintErrorsMatchM4()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ForceSpec overBudget = DataTestAssets.Us();
            overBudget.ArtilleryMissions = 6;
            overBudget.Attachments.Add(new PlatoonSpec { Template = "us_ada_section" });
            ForceRules.Report budgetReport = ForceRules.Evaluate(catalog, overBudget, 2000);
            Assert.That(budgetReport.IsValid(), Is.False);
            Assert.That(DataTestAssets.Join(budgetReport.Errors), Does.Contain("예산 초과"));

            ForceSpec noTank = DataTestAssets.Us();
            noTank.Companies[0] = ForceSpec.MakeCompany(catalog, "co_a", "us_mech_co");
            ForceRules.Report tankReport = ForceRules.Evaluate(catalog, noTank, 5000);
            Assert.That(tankReport.IsValid(), Is.False);
            Assert.That(DataTestAssets.Join(tankReport.Errors), Does.Contain("전차 중대가 최소 1"));

            ForceSpec wrongFaction = DataTestAssets.Us();
            wrongFaction.Companies[0].Platoons[0].Template = "ru_tank_plt";
            Assert.That(ForceRules.Evaluate(catalog, wrongFaction, 5000).IsValid(), Is.False);

            ForceSpec missingRequired = DataTestAssets.Us();
            missingRequired.Companies.RemoveAt(0);
            Assert.That(DataTestAssets.Join(ForceRules.Evaluate(catalog, missingRequired, 5000).Errors),
                Does.Contain("co_a"));
        }

        [Test]
        public void AttachmentLimitsAndExpandedSlotsAreEnforced()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ForceSpec tooMany = DataTestAssets.Us();
            for (int i = 0; i < 4; ++i)
            {
                tooMany.Attachments.Add(new PlatoonSpec { Template = "us_ada_section" });
            }

            Assert.That(DataTestAssets.Join(ForceRules.Evaluate(catalog, tooMany, 5000).Errors),
                Does.Contain("배속 자산은 최대 5"));

            ForceSpec tooManyHelicopters = DataTestAssets.Us();
            tooManyHelicopters.Attachments.Add(new PlatoonSpec { Template = "us_aviation_sec" });
            tooManyHelicopters.Attachments.Add(new PlatoonSpec { Template = "us_aviation_sec" });
            Assert.That(DataTestAssets.Join(ForceRules.Evaluate(catalog, tooManyHelicopters, 50000).Errors),
                Does.Contain("최대 1개"));

            ForceSpec engineers = DataTestAssets.Us();
            int oldElementCount = ForceRules.Evaluate(catalog, engineers, 50000).ElementCount;
            engineers.Attachments.Add(new PlatoonSpec { Template = "us_engineer_plt" });
            ForceRules.Report engineerReport = ForceRules.Evaluate(catalog, engineers, 50000);
            Assert.That(engineerReport.IsValid(), Is.True, DataTestAssets.Join(engineerReport.Errors));
            Assert.That(engineerReport.ElementCount - oldElementCount, Is.EqualTo(5));
        }

        [Test]
        public void WaveDiscountAndLoadoutCostMatchGodotRounding()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            PlatoonSpec platoon = new PlatoonSpec { Template = "us_tank_plt" };
            int baseCost = ForceRules.PlatoonCost(catalog, platoon);
            Assert.That(baseCost, Is.EqualTo(288));
            platoon.Wave = 1;
            Assert.That(ForceRules.PlatoonCost(catalog, platoon), Is.EqualTo(Round(baseCost * 0.9)));
            platoon.Wave = 2;
            Assert.That(ForceRules.PlatoonCost(catalog, platoon), Is.EqualTo(Round(baseCost * 0.8)));
            platoon.Wave = 0;
            platoon.Loadout = "tusk";
            Assert.That(ForceRules.PlatoonCost(catalog, platoon), Is.GreaterThan(baseCost));
            Assert.That(ForceRules.PlatoonCost(catalog, platoon), Is.EqualTo(328));
        }

        [Test]
        public void CrossAttachmentAndAdHocAreWarningsNotHardErrors()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ForceSpec spec = DataTestAssets.Us();
            spec.Companies.Add(ForceSpec.MakeCompany(catalog, "co_c", "us_mech_co"));
            Assert.That(spec.MovePlatoon("co_a", 0, "co_c"), Is.True);
            Assert.That(spec.MovePlatoon("co_c", 0, "co_a"), Is.True);
            ForceRules.Report teamed = ForceRules.Evaluate(catalog, spec, 5000);
            Assert.That(teamed.IsValid(), Is.True, DataTestAssets.Join(teamed.Errors));
            Assert.That(teamed.AdHoc, Is.Empty);

            Assert.That(spec.MovePlatoon("co_a", 0, "co_c"), Is.True);
            Assert.That(spec.MovePlatoon("co_a", 0, "co_c"), Is.True);
            ForceRules.Report adHoc = ForceRules.Evaluate(catalog, spec, 5000);
            Assert.That(adHoc.IsValid(), Is.True, DataTestAssets.Join(adHoc.Errors));
            Assert.That(adHoc.AdHoc, Contains.Item("co_a"));
            Assert.That(adHoc.AdHoc, Contains.Item("co_c"));
            Assert.That(adHoc.AdHoc, Does.Not.Contain("co_b"));
            Assert.That(adHoc.Warnings, Is.Not.Empty);
        }

        [Test]
        public void CrossAttachmentTankShortageMarksAllParticipantsAdHoc()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ForceSpec spec = DataTestAssets.Us();
            spec.Companies.Add(ForceSpec.MakeCompany(catalog, "co_c", "us_mech_co"));
            Assert.That(spec.MovePlatoon("co_a", 0, "co_b"), Is.True);
            Assert.That(spec.MovePlatoon("co_b", 0, "co_a"), Is.True);
            Assert.That(spec.MovePlatoon("co_a", 0, "co_c"), Is.True);
            Assert.That(spec.MovePlatoon("co_c", 0, "co_a"), Is.True);

            ForceRules.Report report = ForceRules.Evaluate(catalog, spec, 5000);
            Assert.That(report.IsValid(), Is.True, DataTestAssets.Join(report.Errors));
            Assert.That(report.AdHoc, Is.EquivalentTo(new[] { "co_a", "co_b", "co_c" }));
            Assert.That(DataTestAssets.Join(report.Warnings), Does.Contain("전차 위주 중대가 0개"));
        }

        [Test]
        public void SupportLoadoutWaveAndOrganicRulesRejectInvalidSpecs()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ForceSpec support = DataTestAssets.Us();
            support.ArtilleryMissions = 7;
            support.CasSorties = 3;
            string supportErrors = DataTestAssets.Join(ForceRules.Evaluate(catalog, support, 50000).Errors);
            Assert.That(supportErrors, Does.Contain("포병 사격 임무는 최대 6"));
            Assert.That(supportErrors, Does.Contain("근접항공지원은 최대 2"));

            ForceSpec invalidPlatoon = DataTestAssets.Us();
            invalidPlatoon.Companies[0].Platoons[0].Wave = 3;
            invalidPlatoon.Companies[0].Platoons[1].Loadout = "missing";
            string platoonErrors = DataTestAssets.Join(ForceRules.Evaluate(catalog, invalidPlatoon, 50000).Errors);
            Assert.That(platoonErrors, Does.Contain("증원 단계 3"));
            Assert.That(platoonErrors, Does.Contain("장비 템플릿 'missing'"));

            ForceSpec duplicateOrganic = DataTestAssets.Us();
            duplicateOrganic.Organic.Add(new PlatoonSpec { Template = "us_scout_plt" });
            Assert.That(DataTestAssets.Join(ForceRules.Evaluate(catalog, duplicateOrganic, 50000).Errors),
                Does.Contain("중복"));
        }

        [Test]
        public void RedDefaultAndGeneratedToeForceAreValid()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            ForceRules.Report report = ForceRules.Evaluate(catalog, DataTestAssets.Ru(), 2000);
            Assert.That(report.IsValid(), Is.True, DataTestAssets.Join(report.Errors));
            ForceSpec generated = ForceSpec.FromToe(catalog, "ru_btg_bmp");
            Assert.That(generated.Companies, Has.Count.EqualTo(3));
            ForceRules.Report generatedReport = ForceRules.Evaluate(catalog, generated, 2000);
            Assert.That(generatedReport.IsValid(), Is.True, DataTestAssets.Join(generatedReport.Errors));
        }

        [Test]
        public void EntryPointSelectionClampsAndHandlesNoEntries()
        {
            ScenarioDef.SideSetup red = DataTestAssets.Catalog.GetScenario("skirmish_01")!.Sides["red"];
            ForceSpec spec = DataTestAssets.Ru();
            Assert.That(spec.EntryPoint, Is.EqualTo(1));
            Assert.That(ForceRules.ResolveEntryPoint(red, spec), Is.EqualTo(new SimVec2(3400, 4000)));
            spec.EntryPoint = 99;
            Assert.That(ForceRules.EntryPointIndex(red, spec), Is.EqualTo(1));
            spec.EntryPoint = -5;
            Assert.That(ForceRules.ResolveEntryPoint(red, spec), Is.EqualTo(new SimVec2(600, 4000)));
            Assert.That(ForceRules.ResolveEntryPoint(new ScenarioDef.SideSetup(), spec), Is.Null);
        }

        private static int Round(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
