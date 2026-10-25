using System;
using System.Collections.Generic;
using Echelon.Core;
using Echelon.Unity.App;
using Echelon.Unity.UI.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Echelon.Unity.Tests.Input
{
    [TestFixture]
    public sealed class BattleOrderInputTests : BattleInputTestFixture
    {
        [Test]
        public void MovementUsesRealOrderNetworkTargetFacingQueueAndReset()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            Controller.SelectUnit(company.Id);
            Controller.SetOrderMode(BattleCommandType.Seize);
            Order observed = null;
            Controller.CommandIssued += order => observed = order;
            var target = new SimVec2(1500f, 1600f);

            IReadOnlyList<Order> orders = Controller.IssueMovementOrders(target, 0.4, true);

            Assert.That(orders, Has.Count.EqualTo(1));
            Order order = orders[0];
            Assert.That(observed, Is.SameAs(order));
            Assert.That(Battle.GetOrder(order.Id), Is.SameAs(order));
            Assert.That(order.Type, Is.EqualTo(OrderType.SEIZE));
            Assert.That(order.TargetUnitId, Is.EqualTo(company.Id));
            Assert.That(order.Issuer, Is.EqualTo("player"));
            Assert.That(order.GetBool("append"), Is.True);
            Assert.That(order.GetFacing(), Is.EqualTo(0.4).Within(0.000001));
            Assert.That(order.GetPos(), Is.EqualTo(target));
            Assert.That(order.State, Is.EqualTo(OrderState.PROPAGATING));
            Assert.That(order.Hops, Is.Not.Empty);
            Assert.That(order.ReceivedTick, Is.EqualTo(-1));
            Assert.That(company.Mission, Is.Not.SameAs(order));
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.Move));
        }

        [Test]
        public void MultiUnitMovementUsesSelectionOrderForEventsAndFormationOffsets()
        {
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            Company first = taskForce.Companies[0];
            Company second = taskForce.Companies[1];
            Controller.SelectUnits(new Unit[] { second, first });
            Controller.SetOrderMode(BattleCommandType.FastMove);
            var observed = new List<Order>();
            Controller.CommandIssued += observed.Add;

            IReadOnlyList<Order> orders = Controller.IssueMovementOrders(
                new SimVec2(2000f, 2000f), 0.0);

            Assert.That(orders, Has.Count.EqualTo(2));
            Assert.That(observed[0], Is.SameAs(orders[0]));
            Assert.That(observed[1], Is.SameAs(orders[1]));
            Assert.That(orders[0].TargetUnitId, Is.EqualTo(second.Id));
            Assert.That(orders[1].TargetUnitId, Is.EqualTo(first.Id));
            Assert.That(orders[0].Type, Is.EqualTo(OrderType.FAST_MOVE));
            Assert.That(orders[0].GetPos().X, Is.EqualTo(1875f).Within(0.001f));
            Assert.That(orders[1].GetPos().X, Is.EqualTo(2125f).Within(0.001f));
            Assert.That(orders[0].GetPos().Y, Is.EqualTo(2000f).Within(0.001f));
            Assert.That(orders[1].GetPos().Y, Is.EqualTo(2000f).Within(0.001f));
        }

        [Test]
        public void DirectPlatoonMovementSetsBypassWithoutDirectlyDetaching()
        {
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            Company company = taskForce.Companies[0];
            Platoon platoon = company.Platoons[0];
            Assert.That(platoon.FollowsParent(), Is.True);
            Controller.SelectUnit(platoon.Id);

            IReadOnlyList<Order> orders = Controller.IssueMovementOrders(
                new SimVec2(1700f, 1400f), null, true);

            Assert.That(orders, Has.Count.EqualTo(1));
            Order order = orders[0];
            Assert.That(order.Bypass, Is.True);
            Assert.That(order.Type, Is.EqualTo(OrderType.MOVE));
            Assert.That(order.GetBool("append"), Is.True);
            Assert.That(order.Hops, Has.Count.EqualTo(2));
            Assert.That(order.Hops[0].FromUnitId, Is.EqualTo(taskForce.Id));
            Assert.That(order.Hops[0].ToUnitId, Is.EqualTo(platoon.Id));
            Assert.That(platoon.Detached, Is.False);
            Assert.That(Battle.OrdersById.Values, Has.None.Matches<Order>(value => value.Type == OrderType.DETACH));
        }

        [Test]
        public void RightDragIssuesFacingAtInitialTargetAndShiftQueues()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            Controller.SelectUnit(company.Id);
            Controller.SetOrderMode(BattleCommandType.Defend);
            Controller.SetHeightFieldPickDelegate((Vector2 screen, out Vector2 target) =>
            {
                target = screen * 10f;
                return true;
            });

            Assert.That(Controller.PointerDown(new Vector2(10f, 10f), BattlePointerButton.Right), Is.True);
            Controller.PointerMove(new Vector2(40f, 10f));
            Assert.That(Controller.HasDirectionPreview, Is.True);
            Assert.That(Controller.PointerUp(new Vector2(40f, 10f), BattlePointerButton.Right, shift: true), Is.True);

            Assert.That(Battle.OrdersById, Has.Count.EqualTo(1));
            foreach (Order order in Battle.OrdersById.Values)
            {
                Assert.That(order.Type, Is.EqualTo(OrderType.DEFEND));
                Assert.That(order.GetPos().X, Is.EqualTo(100f).Within(0.001f));
                Assert.That(order.GetPos().Y, Is.EqualTo(100f).Within(0.001f));
                Assert.That(order.GetFacing(), Is.EqualTo(Math.PI * 0.5).Within(0.000001));
                Assert.That(order.GetBool("append"), Is.True);
            }
            Assert.That(Controller.IsRightDragging, Is.False);
            Assert.That(Controller.HasDirectionPreview, Is.False);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.Move));
        }

        [Test]
        public void FormationHotkeysUseCompanyAndPlatoonCatalogs()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            var issued = new List<Order>();
            Controller.CommandIssued += issued.Add;
            Controller.SelectUnit(company.Id);
            Controller.HandleKey(Key.Digit7, alt: true);
            Assert.That(issued, Has.Count.EqualTo(1));
            Assert.That(issued[0].Type, Is.EqualTo(OrderType.SET_FORMATION));
            Assert.That(issued[0].GetString("formation"), Is.EqualTo("co_box"));

            Controller.SelectUnit(company.Platoons[0].Id);
            Controller.HandleKey(Key.Digit7, alt: true);
            Assert.That(issued, Has.Count.EqualTo(2));
            Assert.That(issued[1].GetString("formation"), Is.EqualTo("coil"));
            Controller.HandleKey(Key.Digit8, alt: true);
            Assert.That(issued, Has.Count.EqualTo(2));
        }

        [Test]
        public void ImmediateCommandsCarrySopMountResupplyDetachAndRejoinParameters()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[1];
            Controller.SelectUnit(company.Id);
            var issued = new List<Order>();
            Controller.CommandIssued += issued.Add;
            string originalRoe = company.Sop.Roe;

            Controller.ToggleHoldFire();
            Assert.That(issued[0].Type, Is.EqualTo(OrderType.SET_SOP));
            string sop = issued[0].GetString("sop");
            StringAssert.Contains("roe", sop);
            StringAssert.Contains(BattleSop.HoldFire, sop);
            Assert.That(company.Sop.Roe, Is.EqualTo(originalRoe));

            string[] techniques = { BattleSop.Traveling, BattleSop.Overwatch, BattleSop.Bounding };
            int techniqueIndex = Array.IndexOf(techniques, company.Sop.Technique);
            string expectedTechnique = techniques[(techniqueIndex + 1) % techniques.Length];
            Controller.CycleTechnique();
            StringAssert.Contains(expectedTechnique, issued[1].GetString("sop"));

            Controller.ToggleMount();
            Assert.That(issued[2].Type, Is.EqualTo(OrderType.DISMOUNT));
            Controller.IssueImmediateOrder(new BattleCommandIntent(BattleCommandType.Resupply));
            Controller.IssueImmediateOrder(new BattleCommandIntent(BattleCommandType.Detach));
            Controller.IssueImmediateOrder(new BattleCommandIntent(BattleCommandType.Rejoin));
            Assert.That(issued[3].Type, Is.EqualTo(OrderType.RESUPPLY));
            Assert.That(issued[4].Type, Is.EqualTo(OrderType.DETACH));
            Assert.That(issued[5].Type, Is.EqualTo(OrderType.REJOIN));
            Assert.That(issued[5].TargetUnitId, Is.EqualTo(company.Id));
        }

        [Test]
        public void HelicopterAltitudeTargetsOnlyAviationAndRejectsOtherSelections()
        {
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            Company company = taskForce.Companies[0];
            var issued = new List<Order>();
            var notifications = new List<string>();
            Controller.CommandIssued += issued.Add;
            Controller.NotificationRequested += notifications.Add;
            Controller.SelectUnit(company.Id);
            Controller.ToggleAltitude();
            Assert.That(issued, Is.Empty);
            Assert.That(notifications, Has.Count.EqualTo(1));

            Platoon helicopter = Battle.Factory.SpawnPlatoon(
                Catalog.GetUnitTemplate("us_aviation_sec"), Battle.PlayerSide,
                new SimVec2(1800f, 800f), 0.0, taskForce);
            taskForce.Organic.Add(helicopter);
            Controller.SelectUnits(new Unit[] { company, helicopter });
            Controller.ToggleAltitude();

            Assert.That(issued, Has.Count.EqualTo(1));
            Assert.That(issued[0].TargetUnitId, Is.EqualTo(helicopter.Id));
            Assert.That(issued[0].Type, Is.EqualTo(OrderType.SET_SOP));
            StringAssert.Contains(BattleSop.HighAltitude, issued[0].GetString("sop"));
            Assert.That(helicopter.Sop.Altitude, Is.EqualTo(BattleSop.LowAltitude));
        }

        [Test]
        public void TargetModeHotkeysCoverMovementEngineeringAndHaltReset()
        {
            Controller.HandleKey(Key.F);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.FastMove));
            Controller.HandleKey(Key.Q);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.AttackMove));
            Controller.HandleKey(Key.Y);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.Seize));
            Controller.HandleKey(Key.J);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.Defend));
            Controller.HandleKey(Key.B);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.Withdraw));
            Controller.HandleKey(Key.N);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.LayMines));
            Controller.HandleKey(Key.I);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.ClearMines));
            Controller.HandleKey(Key.O);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.Fortify));
            Controller.HandleKey(Key.M);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.Move));
            Controller.SetOrderMode(BattleCommandType.AttackMove);
            Controller.HandleKey(Key.E);
            Assert.That(Controller.OrderMode, Is.EqualTo(BattleCommandType.Move));
        }
    }
}
