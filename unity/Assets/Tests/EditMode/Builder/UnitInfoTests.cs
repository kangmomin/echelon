using System;
using System.Collections.Generic;
using System.Globalization;
using Echelon.Core;
using NUnit.Framework;
using UnitInformation = Echelon.Unity.UI.UnitInfo.UnitInfo;

namespace Echelon.Unity.Tests.Builder
{
    public sealed class UnitInfoTests
    {
        private DataCatalog Catalog => BuilderTestData.Catalog;

        [Test]
        public void PlatoonCardMapsRoleProsConsRatingsAndEquipment()
        {
            UnitTemplate template = Catalog.GetUnitTemplate("us_tank_plt");
            UnitTemplate.Loadout tusk = template.GetLoadout("tusk");
            string card = UnitInformation.PlatoonCard(Catalog, template.Id, tusk.Id);
            StringAssert.Contains(template.DisplayName, card);
            StringAssert.Contains(template.Role, card);
            StringAssert.Contains("장비 템플릿: " + tusk.DisplayName, card);
            StringAssert.Contains("328점", card);
            foreach (string pro in template.Pros)
                StringAssert.Contains(pro, card);
            foreach (string con in template.Cons)
                StringAssert.Contains(con, card);
            foreach (string rating in UnitInformation.RatingNames)
                StringAssert.Contains(rating, card);

            ElementDef element = Catalog.GetElement("us_m1a2_sepv3_tusk");
            StringAssert.Contains(element.DisplayName + " × 4", card);
            StringAssert.Contains("장갑 정면 " + BuilderTestData.Round(element.GetArmor("front")), card);
            StringAssert.Contains("측면 " + BuilderTestData.Round(element.GetArmor("side")), card);
            StringAssert.Contains("후면 " + BuilderTestData.Round(element.GetArmor("rear")), card);
            StringAssert.Contains("상면 " + BuilderTestData.Round(element.GetArmor("top")), card);
            StringAssert.Contains("속도 도로 " + BuilderTestData.Round(element.SpeedRoadKmh), card);
            StringAssert.Contains("야지 " + BuilderTestData.Round(element.SpeedOffroadKmh), card);
            StringAssert.Contains("관측 " + (element.OpticsM / 1000.0).ToString("0.0", CultureInfo.InvariantCulture), card);
            StringAssert.Contains("은폐 " + element.Stealth.ToString("0.00", CultureInfo.InvariantCulture), card);
            foreach (string weaponId in element.Weapons)
            {
                WeaponDef weapon = Catalog.GetWeapon(weaponId);
                StringAssert.Contains(weapon.DisplayName, card);
                if (element.Ammo[weaponId] > 0)
                    StringAssert.Contains(element.Ammo[weaponId] + "발", card);
            }
        }

        [Test]
        public void PassengerLoadoutChangesCompositionAndUsesSixBoundedRatings()
        {
            UnitTemplate template = Catalog.GetUnitTemplate("us_mech_plt");
            string card = UnitInformation.PlatoonCard(Catalog, template.Id, "jav2");
            ElementDef passengers = Catalog.GetElement("us_inf_squad_jav2");
            StringAssert.Contains("탑승 " + passengers.DisplayName, card);
            foreach (string weaponId in passengers.Weapons)
                StringAssert.Contains(Catalog.GetWeapon(weaponId).DisplayName, card);

            Dictionary<string, int> ratings = UnitInformation.PlatoonRatings(Catalog, template.Id, "jav2");
            Assert.That(UnitInformation.RatingKeys.Length, Is.EqualTo(6));
            Assert.That(UnitInformation.RatingNames.Length, Is.EqualTo(6));
            Assert.That(ratings.Count, Is.EqualTo(6));
            foreach (string key in UnitInformation.RatingKeys)
                Assert.That(ratings[key], Is.InRange(0, 5), key);
        }

        [Test]
        public void RatingValuesMatchGodotReferenceCalculations()
        {
            AssertRatings("us_tank_plt", "tusk", 5, 5, 5, 3, 2, 0);
            AssertRatings("us_mech_plt", "jav2", 5, 5, 2, 3, 2, 2);
            AssertRatings("ru_msr_plt_bmp3", "bmp2m", 3, 5, 1, 4, 2, 2);
            AssertRatings("us_aviation_sec", "", 4, 4, 0, 5, 5, 0);
            AssertRatings("ru_aviation_sec", "", 4, 4, 0, 5, 5, 2);
            AssertRatings("us_ada_section", "mshorad", 0, 4, 1, 5, 4, 5);
        }

        [Test]
        public void CompanyCardsMapNestedCompositionAndRepresentativeEquipment()
        {
            foreach (UnitTemplate company in Catalog.UnitTemplatesFor("US", "company"))
            {
                AssertCompanyCard(company);
            }
            foreach (UnitTemplate company in Catalog.UnitTemplatesFor("RU", "company"))
            {
                AssertCompanyCard(company);
            }
        }

        [Test]
        public void EveryTemplateLoadoutAndEquipmentOptionHasCardAndTooltip()
        {
            foreach (UnitTemplate template in Catalog.UnitTemplates.Values)
            {
                if (template.Echelon == "company")
                {
                    AssertCompanyCard(template);
                    continue;
                }

                List<string> loadouts = new List<string>();
                foreach (UnitTemplate.Loadout loadout in template.Loadouts)
                    loadouts.Add(loadout.Id);
                if (loadouts.Count == 0)
                    loadouts.Add(string.Empty);
                foreach (string loadout in loadouts)
                {
                    string card = UnitInformation.PlatoonCard(Catalog, template.Id, loadout);
                    Assert.That(card, Is.Not.Empty, template.Id);
                    StringAssert.Contains(template.Role, card, template.Id);
                    foreach (string pro in template.Pros)
                        StringAssert.Contains(pro, card, template.Id);
                    foreach (string con in template.Cons)
                        StringAssert.Contains(con, card, template.Id);
                    foreach (string rating in UnitInformation.RatingNames)
                        StringAssert.Contains(rating, card, template.Id);
                    string selected = UnitInformation.LoadoutOrDefault(template, loadout);
                    AssertEquipment(card, template.ElementsFor(selected), template.Id);
                    AssertEquipment(card, template.PassengersFor(selected), template.Id);

                    string tooltip = UnitInformation.Tooltip(Catalog, template.Id, loadout);
                    StringAssert.Contains(template.DisplayName, tooltip);
                    StringAssert.Contains(template.Role, tooltip);
                    Assert.That(tooltip.Contains("<color"), Is.False);
                    foreach (string pro in template.Pros)
                        StringAssert.Contains(pro, tooltip);
                    foreach (string con in template.Cons)
                        StringAssert.Contains(con, tooltip);
                }
            }
        }

        [Test]
        public void EveryWeaponLineMapsTypeRangeTargetsFlagsAndAmmo()
        {
            foreach (WeaponDef weapon in Catalog.Weapons.Values)
            {
                string line = UnitInformation.WeaponLine(weapon, 17);
                StringAssert.Contains(weapon.DisplayName, line, weapon.Id);
                StringAssert.Contains(UnitInformation.WeaponTypeName(weapon.WeaponType), line);
                StringAssert.Contains((weapon.RangeMaxM / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " km", line);
                StringAssert.Contains("17발", line);
                foreach (string target in weapon.Targets)
                    StringAssert.Contains(UnitInformation.TargetName(target), line);
                if (weapon.Penetration >= 20.0)
                    StringAssert.Contains("관통 " + BuilderTestData.Round(weapon.Penetration), line);
                if (weapon.TopAttack)
                    StringAssert.Contains("상부 공격", line);
                if (weapon.FireOnMove)
                    StringAssert.Contains("이동 사격", line);
                if (weapon.HeRadiusM > 0.0)
                    StringAssert.Contains("폭발 " + BuilderTestData.Round(weapon.HeRadiusM) + " m", line);
                if (weapon.Indirect)
                    StringAssert.Contains("간접", line);
            }
        }

        [Test]
        public void InfantryAndHelicopterDetailsAreMapped()
        {
            string infantry = UnitInformation.PlatoonCard(Catalog, "us_light_inf_plt", "jav2");
            ElementDef squad = Catalog.GetElement("us_inf_squad_jav2");
            StringAssert.Contains("인원 " + BuilderTestData.Round(squad.Hp), infantry);
            StringAssert.Contains("도보 " + BuilderTestData.Round(squad.SpeedOffroadKmh), infantry);
            StringAssert.Contains("은폐 " + squad.Stealth.ToString("0.0", CultureInfo.InvariantCulture), infantry);

            string helicopter = UnitInformation.PlatoonCard(Catalog, "us_aviation_sec");
            StringAssert.Contains("지형 무시 비행", helicopter);
            StringAssert.Contains("저공 25 m / 고공 90 m(V)", helicopter);
            StringAssert.Contains("대공 무기만", helicopter);
        }

        [Test]
        public void MissingTemplatesAndEmptyCompositionAreSafe()
        {
            Assert.That(UnitInformation.PlatoonCard(Catalog, "missing"), Is.Empty);
            Assert.That(UnitInformation.CompanyCard(Catalog, "missing"), Is.Empty);
            Assert.That(UnitInformation.Tooltip(Catalog, "missing"), Is.Empty);
            Assert.That(UnitInformation.CalculateRatings(Catalog,
                new[] { "missing" }, Array.Empty<string>()), Is.Empty);
        }

        private void AssertRatings(
            string templateId,
            string loadout,
            int at,
            int infantry,
            int armor,
            int mobility,
            int recon,
            int aa)
        {
            Dictionary<string, int> ratings = UnitInformation.PlatoonRatings(Catalog, templateId, loadout);
            Assert.That(ratings["at"], Is.EqualTo(at), templateId + " AT");
            Assert.That(ratings["inf"], Is.EqualTo(infantry), templateId + " infantry");
            Assert.That(ratings["armor"], Is.EqualTo(armor), templateId + " armor");
            Assert.That(ratings["mobility"], Is.EqualTo(mobility), templateId + " mobility");
            Assert.That(ratings["recon"], Is.EqualTo(recon), templateId + " recon");
            Assert.That(ratings["aa"], Is.EqualTo(aa), templateId + " AA");
        }

        private void AssertCompanyCard(UnitTemplate company)
        {
            string card = UnitInformation.CompanyCard(Catalog, company.Id);
            Assert.That(card, Is.Not.Empty, company.Id);
            StringAssert.Contains(company.Role, card, company.Id);
            foreach (string pro in company.Pros)
                StringAssert.Contains(pro, card, company.Id);
            foreach (string con in company.Cons)
                StringAssert.Contains(con, card, company.Id);
            foreach (string rating in UnitInformation.RatingNames)
                StringAssert.Contains(rating, card, company.Id);
            foreach (string elementId in company.HqElements)
                StringAssert.Contains(Catalog.GetElement(elementId).DisplayName, card);
            foreach (string platoonId in company.Platoons)
            {
                UnitTemplate platoon = Catalog.GetUnitTemplate(platoonId);
                StringAssert.Contains(platoon.DisplayName, card);
                AssertEquipment(card, platoon.Elements, company.Id);
                AssertEquipment(card, platoon.Passengers, company.Id);
            }
            string tooltip = UnitInformation.Tooltip(Catalog, company.Id);
            StringAssert.Contains(company.DisplayName, tooltip);
            StringAssert.Contains(company.Role, tooltip);
        }

        private void AssertEquipment(string card, IEnumerable<string> elementIds, string context)
        {
            foreach (string elementId in elementIds)
            {
                ElementDef element = Catalog.GetElement(elementId);
                StringAssert.Contains(element.DisplayName, card, context + " / " + elementId);
                foreach (string weaponId in element.Weapons)
                {
                    WeaponDef weapon = Catalog.GetWeapon(weaponId);
                    StringAssert.Contains(weapon.DisplayName, card, context + " / " + weaponId);
                    int ammo = element.Ammo.TryGetValue(weaponId, out int rounds) ? rounds : 0;
                    if (ammo > 0)
                        StringAssert.Contains(ammo + "발", card, context + " / " + weaponId);
                }
            }
        }
    }
}
