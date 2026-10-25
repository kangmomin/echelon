using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class SpatialHashTests
    {
        [Test]
        public void KeysUseFloorForNegativeCoordinates()
        {
            SpatialHash<int> hash = new SpatialHash<int>(100.0);
            Assert.That(hash.KeyOf(new SimVec2(100.0, -100.0)), Is.EqualTo(new SimVec2I(1, -1)));
            Assert.That(hash.KeyOf(new SimVec2(99.999, -0.001)), Is.EqualTo(new SimVec2I(0, -1)));
            Assert.That(hash.KeyOf(new SimVec2(-100.001, 20.0)), Is.EqualTo(new SimVec2I(-2, 0)));
        }

        [Test]
        public void QueryIsBucketRowMajorThenInsertionOrderedWithoutDistanceFiltering()
        {
            SpatialHash<string> hash = new SpatialHash<string>(100.0);
            hash.Insert("A", new SimVec2(150.0, 150.0));
            hash.Insert("B", new SimVec2(50.0, 150.0));
            hash.Insert("C", new SimVec2(50.0, 50.0));
            hash.Insert("D", new SimVec2(150.0, 50.0));
            hash.Insert("E", new SimVec2(50.0, 150.0));
            hash.Insert("F", new SimVec2(250.0, 250.0));

            List<string> result = hash.Query(new SimVec2(100.0, 100.0), 100.0);
            Assert.That(result, Is.EqualTo(new[] { "C", "D", "B", "E", "A", "F" }));
            Assert.That(hash.Query(new SimVec2(-500.0, -500.0), 10.0), Is.Empty);
        }

        [Test]
        public void ClearAllowsDeterministicRebuildAndSupportsValueTypes()
        {
            SpatialHash<int> hash = new SpatialHash<int>(10.0);
            hash.Insert(7, new SimVec2(1.0, 1.0));
            hash.Insert(9, new SimVec2(2.0, 1.0));
            Assert.That(hash.Query(new SimVec2(1.0, 1.0), 1.0), Is.EqualTo(new[] { 7, 9 }));
            hash.Clear();
            Assert.That(hash.Query(new SimVec2(1.0, 1.0), 1.0), Is.Empty);
            hash.Insert(11, new SimVec2(1.0, 1.0));
            Assert.That(hash.Query(new SimVec2(1.0, 1.0), 1.0), Is.EqualTo(new[] { 11 }));
        }
    }
}
