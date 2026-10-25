using System.Collections.Generic;
using Echelon.Core;
using Echelon.Unity.App;
using Echelon.Unity.Presentation;
using Echelon.Unity.UI.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Echelon.Unity.Tests.Input
{
    [TestFixture]
    public sealed class BattleSupportInputTests : BattleInputTestFixture
    {
        [Test]
        public void FireModesCycleTargetAndCancelWithoutConsumingSelection()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            Controller.Commands.SelectUnit(company.Id);
            Controller.HandleKey(Key.T);
            Assert.That(Controller.Commands.FireAsset, Is.EqualTo(BattleFireAsset.Mortar));
            Assert.That(Controller.Commands.FireSmoke, Is.False);
            Controller.HandleKey(Key.T);
            Assert.That(Controller.Commands.FireAsset, Is.EqualTo(BattleFireAsset.Artillery));
            Controller.HandleKey(Key.T);
            Assert.That(Controller.Commands.FireAsset, Is.Null);

            Controller.HandleKey(Key.G);
            Assert.That(Controller.Commands.FireAsset, Is.EqualTo(BattleFireAsset.Mortar));
            Assert.That(Controller.Commands.FireSmoke, Is.True);
            Controller.HandleKey(Key.G);
            Assert.That(Controller.Commands.FireAsset, Is.EqualTo(BattleFireAsset.Artillery));
            Assert.That(Controller.Commands.FireSmoke, Is.True);
            Controller.HandleKey(Key.Escape);
            Assert.That(Controller.Commands.FireAsset, Is.Null);
            CollectionAssert.AreEqual(new[] { company.Id }, Controller.Commands.SelectedUnitIds);

            Controller.Commands.SetFireMode(BattleFireAsset.Cas, true);
            Assert.That(Controller.Commands.FireSmoke, Is.False);
            Controller.PointerDown(Vector2.zero, BattlePointerButton.Right);
            Assert.That(Controller.Commands.FireAsset, Is.Null);
        }

        [Test]
        public void FireRequestUsesRealSupportSystemAndClearsTargeting()
        {
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            taskForce.ArtilleryMissions = 1;
            Company company = taskForce.Companies[0];
            Battle.Factory.Redeploy(company, taskForce.Guide.Pos + new SimVec2(0f, 100f), 0.0);
            Battle.Comms.Rebuild();
            FireSupportSystem.Mission observed = null;
            var notifications = new List<string>();
            Controller.Commands.FireMissionRequested += mission => observed = mission;
            Controller.Commands.Notification += notifications.Add;
            Controller.Commands.SetFireMode(BattleFireAsset.Artillery);

            FireSupportSystem.Mission mission = Controller.Commands.RequestFireSupport(company.Centroid());

            Assert.That(mission, Is.Not.Null);
            Assert.That(observed, Is.SameAs(mission));
            Assert.That(Battle.Fires.Missions, Has.Count.EqualTo(1));
            Assert.That(mission.Asset, Is.EqualTo(FireSupportSystem.Asset.ARTILLERY));
            Assert.That(taskForce.ArtilleryMissions, Is.Zero);
            Assert.That(Controller.Commands.FireAsset, Is.Null);
            Assert.That(notifications, Has.Count.EqualTo(1));
            StringAssert.Contains("요청", notifications[0]);
        }

        [Test]
        public void DelayAndObserverPreviewsUseOrderSystemAndFirePreview()
        {
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            Company company = taskForce.Companies[0];
            Battle.Factory.Redeploy(company, taskForce.Guide.Pos + new SimVec2(0f, 100f), 0.0);
            Battle.Comms.Rebuild();
            Controller.Commands.SelectUnit(company.Id);
            Controller.Commands.SetOrderMode(BattleCommandType.Defend);
            Controller.UpdateCursorPreview(new Vector2(50f, 50f));
            StringAssert.Contains("방어", Controller.CursorPreviewText);
            StringAssert.Contains("실행까지", Controller.CursorPreviewText);

            Platoon platoon = company.Platoons[0];
            Controller.Commands.SelectUnit(platoon.Id);
            Controller.UpdateCursorPreview(new Vector2(50f, 50f));
            StringAssert.Contains("직접 명령: 분리", Controller.CursorPreviewText);

            taskForce.ArtilleryMissions = 1;
            Controller.Commands.SetFireMode(BattleFireAsset.Artillery);
            Controller.UpdateCursorPreview(new Vector2(100f, 100f));
            StringAssert.Contains("포병", Controller.CursorPreviewText);
            StringAssert.Contains("약", Controller.CursorPreviewText);
            Assert.That(Controller.CursorPreviewForced, Is.True);
        }

        [Test]
        public void DeploymentUsesBattleDeployUnitInsteadOfCreatingOrders()
        {
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            Battle.Deploying = true;
            Battle.World.SetPaused(true);
            Controller.Commands.SelectUnit(company.Id);
            SimVec2 target = Battle.Scenario.Sides[Battle.PlayerSide].DeployZone.Center;

            int placed = Controller.Commands.DeploySelection(target, 0.7);

            Assert.That(placed, Is.EqualTo(1));
            Assert.That(company.Guide.Pos.X, Is.EqualTo(target.X).Within(0.001f));
            Assert.That(company.Guide.Pos.Y, Is.EqualTo(target.Y).Within(0.001f));
            Assert.That(company.Guide.Heading, Is.EqualTo(0.7).Within(0.000001));
            Assert.That(Battle.OrdersById, Is.Empty);
            Controller.UpdateCursorPreview(Vector2.zero);
            StringAssert.Contains("배치", Controller.CursorPreviewText);
        }

        [Test]
        public void PauseSpeedFogDebugAndBattleStartCallbacksRemainPublic()
        {
            bool paused = Battle.World.Paused;
            Controller.HandleKey(Key.Space);
            Assert.That(Battle.World.Paused, Is.EqualTo(!paused));
            Controller.Commands.TogglePause();
            Assert.That(Battle.World.Paused, Is.EqualTo(paused));

            int speed = Battle.World.SpeedIndex;
            Controller.Commands.ChangeSpeed(1);
            Assert.That(Battle.World.SpeedIndex, Is.EqualTo(speed + 1));
            Controller.HandleKey(Key.Minus);
            Assert.That(Battle.World.SpeedIndex, Is.EqualTo(speed));

            bool? fog = null;
            bool? debug = null;
            Controller.FogVisibilityChanged += value => fog = value;
            Controller.DebugOverlayVisibilityChanged += value => debug = value;
            Controller.HandleKey(Key.F9);
            Controller.HandleKey(Key.F10);
            Assert.That(fog, Is.False);
            Assert.That(debug, Is.True);

            int starts = 0;
            Controller.BattleStartRequested += () => starts++;
            Controller.RequestBattleStart();
            Assert.That(starts, Is.EqualTo(1));

            Battle.Deploying = true;
            paused = Battle.World.Paused;
            Controller.Commands.TogglePause();
            Assert.That(Battle.World.Paused, Is.EqualTo(paused));
        }

        [Test]
        public void CameraWasdAndPointerRemainBlockedByUiFocusOrGuide()
        {
            var rig = new GameObject("BattleInputCameraTests", typeof(Camera));
            try
            {
                var camera = rig.AddComponent<RtsCameraController>();
                Controller.BindCamera(camera);
                Controller.UiKeyboardFocusProbe = () => true;
                Controller.PointerOverUiProbe = _ => true;
                Controller.Update();
                Assert.That(camera.WasdInputBlocked, Is.True);
                Assert.That(camera.PointerInputBlocked, Is.True);

                Controller.UiKeyboardFocusProbe = () => false;
                Controller.PointerOverUiProbe = _ => false;
                Controller.SetGuideOpen(true);
                Controller.Update();
                Assert.That(camera.WasdInputBlocked, Is.True);
                Assert.That(camera.PointerInputBlocked, Is.True);

                Controller.SetGuideOpen(false);
                Controller.Update();
                Assert.That(camera.WasdInputBlocked, Is.False);
                Assert.That(camera.PointerInputBlocked, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }
    }
}
