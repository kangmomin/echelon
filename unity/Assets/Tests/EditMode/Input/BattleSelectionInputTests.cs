using System.Collections.Generic;
using Echelon.Core;
using Echelon.Unity.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Echelon.Unity.Tests.Input
{
    [TestFixture]
    public sealed class BattleSelectionInputTests : BattleInputTestFixture
    {
        [Test]
        public void OrderedSelection_ControlToggleAndCompanyDoubleClick()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            Platoon first = company.Platoons[0];
            Platoon second = company.Platoons[1];
            var selections = new List<IReadOnlyList<Unit>>();
            Controller.Commands.SelectionChanged += units => selections.Add(new List<Unit>(units));

            Controller.ClickUnit(first.Id);
            Now += 0.1;
            Controller.ClickUnit(second.Id, true);
            CollectionAssert.AreEqual(new[] { first.Id, second.Id }, Controller.Commands.SelectedUnitIds);

            Controller.ClickUnit(first.Id, true);
            CollectionAssert.AreEqual(new[] { second.Id }, Controller.Commands.SelectedUnitIds);
            Now += 0.1;
            Controller.ClickUnit(second.Id);
            Now += 0.1;
            Controller.ClickUnit(second.Id);

            CollectionAssert.AreEqual(new[] { company.Id }, Controller.Commands.SelectedUnitIds);
            Assert.That(selections.Count, Is.EqualTo(4));
        }

        [Test]
        public void TabNavigationAndCompanyFunctionKeysPreserveEchelonOrder()
        {
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            Company first = taskForce.Companies[0];
            Company second = taskForce.Companies[1];
            Controller.Commands.SelectUnit(first.Id);

            Controller.HandleKey(Key.Tab);
            List<Unit> children = first.ChildrenUnits();
            Assert.That(Controller.Commands.Selection.Count, Is.EqualTo(children.Count));
            for (int i = 0; i < children.Count; i++)
                Assert.That(Controller.Commands.Selection[i], Is.SameAs(children[i]));

            Controller.HandleKey(Key.Tab, shift: true);
            CollectionAssert.AreEqual(new[] { first.Id }, Controller.Commands.SelectedUnitIds);

            Vector2? focus = null;
            Controller.Commands.FocusRequested += value => focus = value;
            Controller.HandleKey(Key.F2);
            CollectionAssert.AreEqual(new[] { second.Id }, Controller.Commands.SelectedUnitIds);
            Assert.That(focus.HasValue, Is.True);
            Assert.That(focus.Value.x, Is.EqualTo(second.Centroid().X).Within(0.001f));
            Assert.That(focus.Value.y, Is.EqualTo(second.Centroid().Y).Within(0.001f));

            Controller.HandleKey(Key.F4);
            CollectionAssert.AreEqual(new[] { second.Id }, Controller.Commands.SelectedUnitIds);
            Controller.HandleKey(Key.F1);
            CollectionAssert.AreEqual(new[] { first.Id }, Controller.Commands.SelectedUnitIds);
        }

        [Test]
        public void ControlGroupsRecallInOrderAdditivelyAndFocusOnDoubleTap()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            Platoon first = company.Platoons[0];
            Platoon second = company.Platoons[1];
            Platoon other = company.Hq;
            Assert.That(other, Is.Not.Null);
            var notifications = new List<string>();
            Controller.Commands.Notification += notifications.Add;

            Controller.Commands.SelectUnits(new Unit[] { first, second });
            Controller.HandleKey(Key.Digit4, control: true);
            Assert.That(notifications, Has.Count.EqualTo(1));
            Controller.Commands.ClearSelection();
            Controller.Commands.SelectUnit(other.Id);

            Controller.HandleKey(Key.Digit4, shift: true);
            CollectionAssert.AreEqual(new[] { other.Id, first.Id, second.Id }, Controller.Commands.SelectedUnitIds);
            Controller.HandleKey(Key.Digit4);
            CollectionAssert.AreEqual(new[] { first.Id, second.Id }, Controller.Commands.SelectedUnitIds);

            Vector2? focus = null;
            Controller.Commands.FocusRequested += value => focus = value;
            Now += 0.1;
            Controller.HandleKey(Key.Digit4);
            Assert.That(focus.HasValue, Is.True);
            Assert.That(focus.Value.x, Is.EqualTo(first.Centroid().X).Within(0.001f));
            CollectionAssert.AreEqual(new Unit[] { first, second }, Controller.Commands.GetGroupUnits(4));

            Controller.HandleKey(Key.Escape);
            Assert.That(Controller.Commands.Selection, Is.Empty);
        }

        [Test]
        public void OrbatStyleAdditiveSelectionDoesNotToggleOrDuplicate()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            Platoon first = company.Platoons[0];
            Platoon second = company.Platoons[1];

            Controller.Commands.SelectUnit(first.Id);
            Controller.Commands.SelectUnit(second.Id, true);
            Controller.Commands.SelectUnit(second.Id, true);
            CollectionAssert.AreEqual(new[] { first.Id, second.Id }, Controller.Commands.SelectedUnitIds);

            Controller.Commands.SelectUnitsById(new[] { second.Id, first.Id });
            CollectionAssert.AreEqual(new[] { second.Id, first.Id }, Controller.Commands.SelectedUnitIds);
        }

        [Test]
        public void GuideEscapeAndMissingCommandNotificationsFollowBattleState()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            Controller.Commands.SelectUnit(company.Id);
            int guideRequests = 0;
            var notifications = new List<string>();
            Controller.GuideRequested += () => guideRequests++;
            Controller.Commands.Notification += notifications.Add;

            Controller.HandleKey(Key.Slash);
            Assert.That(Controller.GuideOpen, Is.True);
            Assert.That(guideRequests, Is.EqualTo(1));
            Controller.HandleKey(Key.Escape);
            Assert.That(Controller.GuideOpen, Is.False);
            Assert.That(guideRequests, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { company.Id }, Controller.Commands.SelectedUnitIds);

            Controller.HandleKey(Key.R);
            Controller.HandleKey(Key.Z);
            Controller.HandleKey(Key.X);
            Controller.HandleKey(Key.C);
            Assert.That(notifications, Has.Count.EqualTo(4));
            foreach (string notification in notifications)
                StringAssert.Contains("아직 없습니다", notification);
        }
    }
}
