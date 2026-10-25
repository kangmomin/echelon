using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class EchelonModelSopTests
    {
        [Test]
        public void DefaultsAndAllowedValuesMatchGodot()
        {
            Sop sop = new Sop();
            Assert.That(sop.Roe, Is.EqualTo("fire_at_will"));
            Assert.That(sop.OnContact, Is.EqualTo("fire_and_continue"));
            Assert.That(sop.Technique, Is.EqualTo("traveling"));
            Assert.That(sop.WithdrawAt, Is.EqualTo(0.4));
            Assert.That(sop.MountPolicy, Is.EqualTo("dismount_on_contact"));
            Assert.That(sop.UnderFire, Is.EqualTo("disperse"));
            Assert.That(sop.Altitude, Is.EqualTo("low"));
            Assert.That(Sop.RoeValues, Is.EqualTo(new[] { "hold_fire", "fire_at_will", "designated" }));
            Assert.That(Sop.ContactValues, Is.EqualTo(new[] { "halt_and_fire", "fire_and_continue", "assault", "break_contact" }));
            Assert.That(Sop.TechniqueValues, Is.EqualTo(new[] { "traveling", "overwatch", "bounding" }));
            Assert.That(Sop.WithdrawValues, Is.EqualTo(new[] { 0.4, 0.25, 0.0 }));
            Assert.That(Sop.MountValues, Is.EqualTo(new[] { "dismount_on_contact", "stay_mounted", "stay_dismounted" }));
            Assert.That(Sop.UnderFireValues, Is.EqualTo(new[] { "disperse", "hold" }));
            Assert.That(Sop.AltitudeValues, Is.EqualTo(new[] { "low", "high" }));
        }

        [Test]
        public void CopyDuplicatesEveryValueWithoutSharingState()
        {
            Sop source = new Sop
            {
                Roe = "designated",
                OnContact = "assault",
                Technique = "bounding",
                WithdrawAt = 0.25,
                MountPolicy = "stay_mounted",
                UnderFire = "hold",
                Altitude = "high"
            };
            Sop copy = source.Copy();
            Assert.That(copy, Is.Not.SameAs(source));
            Assert.That(copy.ToDict(), Is.EqualTo(source.ToDict()));
            copy.Roe = "hold_fire";
            copy.WithdrawAt = 0.0;
            Assert.That(source.Roe, Is.EqualTo("designated"));
            Assert.That(source.WithdrawAt, Is.EqualTo(0.25));
        }

        [Test]
        public void ApplyChangesOnlyNamedKeysAndClampsWithdrawal()
        {
            Sop sop = new Sop();
            Dictionary<string, object?> changes = new Dictionary<string, object?>
            {
                ["roe"] = "hold_fire",
                ["on_contact"] = "break_contact",
                ["technique"] = "overwatch",
                ["withdraw_at"] = 1.5,
                ["mount_policy"] = "stay_dismounted",
                ["altitude"] = "high"
            };
            Assert.That(sop.Apply(changes), Is.True);
            Assert.That(sop.Roe, Is.EqualTo("hold_fire"));
            Assert.That(sop.OnContact, Is.EqualTo("break_contact"));
            Assert.That(sop.Technique, Is.EqualTo("overwatch"));
            Assert.That(sop.WithdrawAt, Is.EqualTo(1.0));
            Assert.That(sop.MountPolicy, Is.EqualTo("stay_dismounted"));
            Assert.That(sop.UnderFire, Is.EqualTo("disperse"));
            Assert.That(sop.Altitude, Is.EqualTo("high"));
            Assert.That(sop.Apply(new Dictionary<string, object?> { ["withdraw_at"] = -0.5 }), Is.True);
            Assert.That(sop.WithdrawAt, Is.EqualTo(0.0));
        }

        [Test]
        public void ApplyRejectsBadValuesButKeepsOtherValidChanges()
        {
            Sop sop = new Sop { Roe = "designated" };
            Dictionary<string, object?> changes = new Dictionary<string, object?>
            {
                ["roe"] = "not_a_roe",
                ["technique"] = "bounding",
                ["withdraw_at"] = "0.25",
                ["altitude"] = 12,
                ["unknown"] = true
            };
            Assert.That(sop.Apply(changes), Is.False);
            Assert.That(sop.Roe, Is.EqualTo("designated"));
            Assert.That(sop.Technique, Is.EqualTo("bounding"));
            Assert.That(sop.WithdrawAt, Is.EqualTo(0.4));
            Assert.That(sop.Altitude, Is.EqualTo("low"));
        }

        [Test]
        public void JsonPatchesAndDictionaryExportUseGodotKeys()
        {
            Sop sop = new Sop();
            JObject patch = new JObject
            {
                ["roe"] = "designated",
                ["on_contact"] = "halt_and_fire",
                ["withdraw_at"] = 0.25,
                ["under_fire"] = "hold"
            };
            Assert.That(sop.Apply(patch), Is.True);
            Dictionary<string, object> data = sop.ToDict();
            Assert.That(data.Count, Is.EqualTo(7));
            Assert.That(data["roe"], Is.EqualTo("designated"));
            Assert.That(data["on_contact"], Is.EqualTo("halt_and_fire"));
            Assert.That(data["technique"], Is.EqualTo("traveling"));
            Assert.That(data["withdraw_at"], Is.EqualTo(0.25));
            Assert.That(data["mount_policy"], Is.EqualTo("dismount_on_contact"));
            Assert.That(data["under_fire"], Is.EqualTo("hold"));
            Assert.That(data["altitude"], Is.EqualTo("low"));
            Assert.That(sop.Apply(new JObject { ["roe"] = 1, ["technique"] = "bounding" }), Is.False);
            Assert.That(sop.Roe, Is.EqualTo("designated"));
            Assert.That(sop.Technique, Is.EqualTo("bounding"));
        }
    }
}
