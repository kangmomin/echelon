using System.Linq;
using Echelon.Core;
using Echelon.Unity.Platform;
using NUnit.Framework;

namespace Echelon.Unity.Tests.Platform
{
    public sealed class UnityCatalogSourceTests
    {
        [TestCase(CatalogCategories.Weapons, 55)]
        [TestCase(CatalogCategories.Elements, 57)]
        [TestCase(CatalogCategories.Units, 63)]
        [TestCase(CatalogCategories.Maps, 1)]
        [TestCase(CatalogCategories.Toe, 2)]
        [TestCase(CatalogCategories.Scenarios, 1)]
        [TestCase(CatalogCategories.Forces, 2)]
        [TestCase(CatalogCategories.Formations, 1)]
        public void CatalogContainsExpectedFileCounts(string category, int expected)
        {
            var source = new UnityCatalogSource();
            Assert.That(source.EnumerateJson(category).Count(), Is.EqualTo(expected));
        }

        [Test]
        public void ReadTextReturnsOriginalJson()
        {
            var source = new UnityCatalogSource();
            CatalogJsonFile element = source.EnumerateJson(CatalogCategories.Elements)
                .Single(file => file.Name == "us_m1a2_sepv3.json");
            StringAssert.Contains("\"id\": \"us_m1a2_sepv3\"", source.ReadText(element));
            CatalogJsonFile formations = source.EnumerateJson(CatalogCategories.Formations).Single();
            StringAssert.Contains("\"id\": \"wedge\"", source.ReadText(formations));
        }
    }
}
