using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class InsertionOrderedDictionaryTests
    {
        [Test]
        public void OverwriteRemovalAndReinsertionPreserveIterationOrder()
        {
            var dictionary = new InsertionOrderedDictionary<int, string>
            {
                [76] = "original",
                [84] = "middle",
                [98] = "last"
            };
            dictionary[76] = "updated";
            Assert.That(dictionary.Keys, Is.EqualTo(new[] { 76, 84, 98 }));
            Assert.That(dictionary.Values, Is.EqualTo(new[] { "updated", "middle", "last" }));

            Assert.That(dictionary.Remove(76), Is.True);
            Assert.That(dictionary.Remove(76), Is.False);
            dictionary[76] = "reinserted";
            var expected = new[]
            {
                new KeyValuePair<int, string>(84, "middle"),
                new KeyValuePair<int, string>(98, "last"),
                new KeyValuePair<int, string>(76, "reinserted")
            };
            Assert.That(dictionary, Is.EqualTo(expected));
            IReadOnlyDictionary<int, string> readOnly = dictionary;
            Assert.That(readOnly.Keys, Is.EqualTo(new[] { 84, 98, 76 }));
            Assert.That(readOnly.Values, Is.EqualTo(new[] { "middle", "last", "reinserted" }));

            var copy = new KeyValuePair<int, string>[5];
            dictionary.CopyTo(copy, 1);
            Assert.That(copy[1], Is.EqualTo(expected[0]));
            Assert.That(copy[2], Is.EqualTo(expected[1]));
            Assert.That(copy[3], Is.EqualTo(expected[2]));
        }

        [Test]
        public void KeyValuePairCollectionRequiresBothKeyAndValueForRemoval()
        {
            var dictionary = new InsertionOrderedDictionary<string, int>
            {
                ["one"] = 1,
                ["two"] = 2
            };
            ICollection<KeyValuePair<string, int>> collection = dictionary;
            var wrongValue = new KeyValuePair<string, int>("one", 99);
            Assert.That(collection.IsReadOnly, Is.False);
            Assert.That(collection.Contains(wrongValue), Is.False);
            Assert.That(collection.Remove(wrongValue), Is.False);
            Assert.That(dictionary["one"], Is.EqualTo(1));

            Assert.That(collection.Remove(new KeyValuePair<string, int>("one", 1)), Is.True);
            collection.Add(new KeyValuePair<string, int>("three", 3));
            Assert.That(dictionary.Keys, Is.EqualTo(new[] { "two", "three" }));
            Assert.That(() => collection.Add(new KeyValuePair<string, int>("two", 20)), Throws.ArgumentException);

            collection.Clear();
            Assert.That(dictionary, Is.Empty);
            Assert.That(dictionary.Keys, Is.Empty);
            Assert.That(dictionary.Values, Is.Empty);
        }

        [Test]
        public void ComparerAndLiveReadOnlyViewsFollowDictionarySemantics()
        {
            var dictionary = new InsertionOrderedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            ICollection<string> keys = dictionary.Keys;
            ICollection<int> values = dictionary.Values;
            dictionary.Add("One", 1);
            dictionary["ONE"] = 10;
            dictionary.Add("Two", 2);

            Assert.That(dictionary.Comparer, Is.SameAs(StringComparer.OrdinalIgnoreCase));
            Assert.That(dictionary.Count, Is.EqualTo(2));
            Assert.That(keys, Is.EqualTo(new[] { "One", "Two" }));
            Assert.That(values, Is.EqualTo(new[] { 10, 2 }));
            Assert.That(keys.Contains("one"), Is.True);
            Assert.That(keys.IsReadOnly, Is.True);
            Assert.That(values.IsReadOnly, Is.True);
            Assert.That(() => keys.Add("three"), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => values.Remove(10), Throws.TypeOf<NotSupportedException>());

            Assert.That(dictionary.Remove("ONE"), Is.True);
            dictionary.Add("ONE", 11);
            Assert.That(keys, Is.EqualTo(new[] { "Two", "ONE" }));
            Assert.That(values, Is.EqualTo(new[] { 2, 11 }));
        }
    }
}
