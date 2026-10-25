using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class DataDefReaderTests
    {
        [Test]
        public void WrongTypeIsReported()
        {
            DefReader reader = new DefReader();
            Assert.That(reader.ReqNumber(new JObject { ["hp"] = "abc" }, "hp"), Is.Zero);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "'hp'는 숫자여야 함"), Is.True);
        }

        [Test]
        public void NonIntegerIsReportedAndTruncatedForIntField()
        {
            DefReader reader = new DefReader();
            Assert.That(reader.ReqInt(new JObject { ["crew"] = 3.5 }, "crew"), Is.EqualTo(3));
            Assert.That(reader.HasErrors(), Is.True);
        }

        [Test]
        public void RequiredFieldsAndContextAccumulate()
        {
            DefReader reader = new DefReader { Context = "synthetic.json" };
            reader.ReqString(new JObject(), "id");
            reader.ReqObject(new JObject(), "armor");
            Assert.That(reader.Errors, Has.Count.EqualTo(2));
            Assert.That(reader.Errors, Has.All.Contains("synthetic.json"));
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "'id'"), Is.True);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "'armor'"), Is.True);
        }

        [Test]
        public void VectorValidationRejectsWrongShapeAndReadsCoordinates()
        {
            DefReader reader = new DefReader();
            Assert.That(reader.AsVec2(new JArray(1, 2, 3), "pos"), Is.EqualTo(SimVec2.Zero));
            Assert.That(reader.AsVec2(new JArray(12, -4), "pos"), Is.EqualTo(new SimVec2(12, -4)));
            Assert.That(reader.AsVec2("bad", "other"), Is.EqualTo(SimVec2.Zero));
            Assert.That(reader.Errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void NonFiniteAndOversizedNumbersAreRejectedWithoutThrowing()
        {
            DefReader reader = new DefReader();
            JObject oversized = JObject.Parse("{\"crew\":999999999999999999999999999999999999}");
            Assert.That(reader.ReqInt(oversized, "crew"), Is.Zero);
            Assert.That(DefReader.IsNumber(new JValue(double.NaN)), Is.False);
            Assert.That(DefReader.IsNumber(new JValue(double.PositiveInfinity)), Is.False);
            Assert.That(reader.HasErrors(), Is.True);
        }

        [Test]
        public void OneOfAcceptsAnyEnumerableWithoutLosingValidation()
        {
            DefReader reader = new DefReader();
            reader.CheckOneOf("air", Values(), "target");
            reader.CheckOneOf("water", Values(), "target");
            Assert.That(reader.Errors, Has.Count.EqualTo(1));
            Assert.That(reader.Errors[0], Does.Contain("water"));
        }

        [Test]
        public void WeaponRangeAndAccuracyChecksAccumulate()
        {
            JObject raw = new JObject
            {
                ["id"] = "bad",
                ["type"] = "KE",
                ["range_m"] = new JObject { ["min"] = 100, ["max"] = 50 },
                ["penetration"] = 10,
                ["damage"] = 1,
                ["accuracy"] = new JObject { ["base"] = 1.5, ["at_max_range"] = 0.1 },
                ["rof_rpm"] = 1,
                ["suppression"] = 1,
                ["fire_on_move"] = true,
                ["targets"] = new JArray("vehicle")
            };
            DefReader reader = new DefReader();
            WeaponDef.FromJObject(raw, reader);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "range_m.max"), Is.True);
            Assert.That(DataTestAssets.HasMessage(reader.Errors, "accuracy.base"), Is.True);
        }

        private static IEnumerable<string> Values()
        {
            yield return "vehicle";
            yield return "air";
        }
    }
}
