using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class SupplySystemTests
    {
        [Test]
        public void NeedAveragesMissingFractionPerWeapon()
        {
            Element element = ElementWithAmmo(100, ("a", 10, 10), ("b", 20, 0));
            Assert.That(SupplySystem.ElementNeed(element), Is.EqualTo(50.0).Within(0.0001));
            double used = SupplySystem.Give(element, 10.0);
            Assert.That(used, Is.EqualTo(10.0).Within(0.0001));
            Assert.That(element.Ammo["a"], Is.EqualTo(10));
            Assert.That(element.Ammo["b"], Is.EqualTo(2));
            Assert.That(SupplySystem.ElementNeed(element), Is.EqualTo(45.0).Within(0.0001));
        }

        [Test]
        public void GiveUsesCeilingAndAtLeastOneRoundPerMissingWeapon()
        {
            Element element = ElementWithAmmo(100, ("a", 10, 0), ("b", 20, 0));
            double used = SupplySystem.Give(element, 10.0);
            Assert.That(used, Is.EqualTo(10.0));
            Assert.That(element.Ammo["a"], Is.EqualTo(1));
            Assert.That(element.Ammo["b"], Is.EqualTo(2));
            Assert.That(SupplySystem.Give(element, 0.0), Is.EqualTo(0.0));
        }

        [Test]
        public void AmmoRatioAndSectionTotalsIgnoreDeadElements()
        {
            Platoon platoon = new Platoon();
            Element alive = ElementWithAmmo(100, ("a", 10, 5));
            alive.Def.SupplyCapacity = 100;
            alive.SupplyLeft = 80.0;
            Element dead = ElementWithAmmo(100, ("a", 10, 0));
            dead.Def.SupplyCapacity = 100;
            dead.SupplyLeft = 60.0;
            dead.State = ElementState.DESTROYED;
            platoon.Elements.Add(alive);
            platoon.Elements.Add(dead);
            Assert.That(SupplySystem.AmmoRatio(platoon), Is.EqualTo(0.5).Within(0.0001));
            Assert.That(SupplySystem.SectionSupply(platoon), Is.EqualTo(80.0));
            Assert.That(SupplySystem.SectionCapacity(platoon), Is.EqualTo(100.0));
        }

        [Test]
        public void TasksAndReturningKeepLiveOrderAfterRemovalAndReinsertion()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.Create(createTaskForces: false);
            SupplySystem supply = rig.Supply;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;

            Invoke("AddTask", 76, 760);
            Invoke("AddTask", 84, 840);
            Invoke("RemoveTask", 76);
            Invoke("AddTask", 76, 761);
            Assert.That(supply.Tasks, Is.EqualTo(new[]
            {
                new KeyValuePair<int, int>(84, 840),
                new KeyValuePair<int, int>(76, 761)
            }));

            Invoke("AddReturning", 76);
            Invoke("AddReturning", 84);
            Invoke("RemoveReturning", 76);
            Invoke("AddReturning", 76);
            Assert.That(supply.Returning.Keys, Is.EqualTo(new[] { 84, 76 }));

            void Invoke(string method, params object[] arguments)
            {
                typeof(SupplySystem).GetMethod(method, flags)!.Invoke(supply, arguments);
            }
        }

        private static Element ElementWithAmmo(int supplyFull, params (string Id, int Full, int Current)[] ammunition)
        {
            Element element = new Element
            {
                Def = new ElementDef { SupplyFull = supplyFull }
            };
            foreach ((string id, int full, int current) in ammunition)
            {
                element.Def.Ammo[id] = full;
                element.Ammo[id] = current;
            }

            return element;
        }
    }
}
