using Echelon.Unity.Presentation.Symbols;
using NUnit.Framework;

namespace Echelon.Unity.Tests.Symbols
{
    public sealed class SymbolAggregationTests
    {
        [TestCase(0f, SymbolAggregationLevel.Element)]
        [TestCase(449.99f, SymbolAggregationLevel.Element)]
        [TestCase(450f, SymbolAggregationLevel.Platoon)]
        [TestCase(2199.99f, SymbolAggregationLevel.Platoon)]
        [TestCase(2200f, SymbolAggregationLevel.Company)]
        public void CameraHeight_SelectsGodotAggregationThresholds(float height, SymbolAggregationLevel expected)
        {
            Assert.That(SymbolAggregation.LevelForCameraHeight(height), Is.EqualTo(expected));
        }

        [Test]
        public void Aggregation_DrawsElementsThenPlatoonsThenCompanies()
        {
            SymbolUnitSnapshot platoon = Unit(SymbolUnitLevel.Platoon);
            platoon.Elements.Add(new SymbolElementSnapshot());
            SymbolUnitSnapshot company = Unit(SymbolUnitLevel.Company);

            Assert.That(SymbolAggregation.ShouldDrawElements(platoon, SymbolAggregationLevel.Element, "blue", true), Is.True);
            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(platoon, SymbolAggregationLevel.Element, "blue", true), Is.False);
            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(company, SymbolAggregationLevel.Element, "blue", true), Is.False);

            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(platoon, SymbolAggregationLevel.Platoon, "blue", true), Is.True);
            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(company, SymbolAggregationLevel.Platoon, "blue", true), Is.False);
            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(company, SymbolAggregationLevel.Company, "blue", true), Is.True);
        }

        [Test]
        public void CompanyAggregation_OmitsAttachedButKeepsDetachedPlatoons()
        {
            SymbolUnitSnapshot attached = Unit(SymbolUnitLevel.Platoon);
            attached.FollowsParent = true;
            SymbolUnitSnapshot detached = Unit(SymbolUnitLevel.Platoon);
            detached.Detached = true;
            detached.FollowsParent = false;

            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(attached, SymbolAggregationLevel.Company, "blue", true), Is.False);
            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(detached, SymbolAggregationLevel.Company, "blue", true), Is.True);
        }

        [Test]
        public void FogFiltering_NeverDrawsHiddenHostileContacts()
        {
            SymbolUnitSnapshot hiddenEnemy = Unit(SymbolUnitLevel.Platoon);
            hiddenEnemy.Contact = SymbolContactState.Hidden;
            SymbolUnitSnapshot friendly = Unit(SymbolUnitLevel.Platoon);
            friendly.Side = "blue";
            friendly.Contact = SymbolContactState.Hidden;

            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(hiddenEnemy, SymbolAggregationLevel.Platoon, "blue", true), Is.False);
            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(hiddenEnemy, SymbolAggregationLevel.Platoon, "blue", false), Is.True);
            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(friendly, SymbolAggregationLevel.Platoon, "blue", true), Is.True);

            hiddenEnemy.Contact = SymbolContactState.Reported;
            Assert.That(SymbolAggregation.ShouldDrawUnitFrame(hiddenEnemy, SymbolAggregationLevel.Platoon, "blue", true), Is.True);
        }

        [Test]
        public void ElementFiltering_RejectsHiddenMountedAndOffMapElements()
        {
            SymbolUnitSnapshot unit = Unit(SymbolUnitLevel.Platoon);
            var element = new SymbolElementSnapshot { Contact = SymbolContactState.Hidden };
            Assert.That(SymbolAggregation.ShouldDrawElement(unit, element, "blue", true), Is.False);
            element.Contact = SymbolContactState.Stale;
            Assert.That(SymbolAggregation.ShouldDrawElement(unit, element, "blue", true), Is.True);
            element.IsMounted = true;
            Assert.That(SymbolAggregation.ShouldDrawElement(unit, element, "blue", true), Is.False);
            element.IsMounted = false;
            element.OnMap = false;
            Assert.That(SymbolAggregation.ShouldDrawElement(unit, element, "blue", true), Is.False);
        }

        [Test]
        public void Selection_OutlinesParentCompanyAndDirectSelection()
        {
            SymbolUnitSnapshot platoon = Unit(SymbolUnitLevel.Platoon);
            platoon.ParentId = "company";
            var selected = new[] { "company" };
            Assert.That(SymbolAggregation.IsSelected(platoon, selected), Is.True);
            Assert.That(SymbolAggregation.IsDirectlySelected(platoon, selected), Is.False);
            selected[0] = platoon.Id;
            Assert.That(SymbolAggregation.IsDirectlySelected(platoon, selected), Is.True);
        }

        private static SymbolUnitSnapshot Unit(SymbolUnitLevel level)
        {
            return new SymbolUnitSnapshot
            {
                Id = level + "_unit",
                Side = "red",
                Level = level
            };
        }
    }
}
