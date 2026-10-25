using System;
using System.Collections.Generic;
using Echelon.Core;
using Echelon.Unity.App;
using Echelon.Unity.Presentation.Elements;
using Echelon.Unity.Presentation.Symbols;
using Echelon.Unity.Presentation.Terrain;
using Echelon.Unity.UI.Aar;
using Echelon.Unity.UI.Battle;
using Echelon.Unity.UI.Minimap;
using Echelon.Unity.UI.Tutorial;
using NUnit.Framework;
using UnityEngine;
using CoreBattle = Echelon.Core.Battle;

namespace Echelon.Unity.Tests.BattleComposition
{
    public sealed class BattleControllerTests : BattleControllerTestFixture
    {
        [Test]
        public void CompositionUsesRealTerrainElementsAndUi()
        {
            Compose();

            Assert.IsTrue(Controller.IsComposed);
            Assert.AreSame(Battle, Controller.Battle);
            Assert.AreSame(Battle, Controller.Input.Battle);
            Assert.NotNull(Controller.RtsCamera.TargetCamera);
            Assert.NotNull(Controller.Terrain);
            Assert.NotNull(Controller.ElementViews);
            Assert.NotNull(Controller.Fx);
            Assert.NotNull(Controller.Symbols.Snapshot);
            Assert.NotNull(Controller.Minimap.Snapshot);
            Assert.NotNull(Controller.OverlayRoot);
            Assert.IsTrue(Controller.Hud.IsBuilt);
            Assert.NotNull(Controller.Orbat.Root);
            Assert.AreEqual(BattleUnitLevel.TaskForce, Controller.Orbat.Root.Unit.Level);

            TerrainMeshData data = Controller.TerrainData;
            Assert.AreEqual(Battle.Terrain.Width, data.GridWidth);
            Assert.AreEqual(Battle.Terrain.HeightCells, data.GridHeight);
            Assert.AreEqual(Battle.Terrain.Heights.Length, data.Heights.Length);
            Assert.AreEqual(Battle.Map.Roads.Count, data.Roads.Length);
            Assert.AreEqual(Battle.Map.Rivers.Count, data.Rivers.Length);
            Assert.AreEqual(Battle.Map.Forests.Count, data.Forests.Length);
            Assert.AreEqual(Battle.Map.Towns.Count, data.Towns.Length);
            for (int i = 0; i < Battle.Terrain.Types.Length; i += 37)
                Assert.AreEqual((int)Battle.Terrain.Types[i], (int)data.Types.Span[i]);
            CollectionAssert.IsEmpty(data.Validate());
            CollectionAssert.IsEmpty(Controller.Terrain.Validate());

            Assert.AreEqual(Battle.Elements.Count, Controller.CurrentElementStates.Count);
            Assert.AreEqual(Battle.Elements.Count, Controller.ElementViews.ActiveCount);
            Assert.AreEqual(210, Controller.Minimap.TerrainTexture.width);
            Assert.AreEqual(210, Controller.Minimap.TerrainTexture.height);
        }

        [Test]
        public void DeploymentSelectionAndStartUseCoreState()
        {
            CoreBattle battle = CreateTutorialBattle();
            battle.Deploying = true;
            battle.World.SetPaused(true);
            Compose(battle);
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            DeploymentZoneData zone = BattleViewAdapter.CreateDeploymentZone(Battle);
            Controller.SelectUnit(company.Id);

            int placed = Controller.DeploySelection(zone.Zone.center, 0.5f);

            Assert.AreEqual(1, placed);
            Assert.Less(BattleViewAdapter.ToVector(company.Guide.Pos).x, zone.Zone.xMax);
            Assert.IsTrue(Controller.DeploymentPanel.IsDeploying);
            Controller.StartBattle();
            Assert.IsFalse(Battle.Deploying);
            Assert.IsFalse(Battle.World.Paused);
            Assert.IsFalse(Controller.DeploymentPanel.IsDeploying);
            Controller.AdvanceFrame(0f);
            Assert.Greater(Controller.LastDrainedEventCount, 0);
            Assert.Greater(Controller.Notifications.Count, 0);
        }

        [Test]
        public void FrameAdvancesThenDrainsThenRefreshesAndDeliversAar()
        {
            Compose();
            long initialTick = Battle.World.Tick;
            long markerTick = -1;
            int markerRevision = -1;
            int finishedRevision = -1;
            AarData finished = null;
            Controller.SimulationEvent += simEvent =>
            {
                if (simEvent.Type == "test_marker")
                {
                    markerTick = Battle.World.Tick;
                    markerRevision = Controller.PresentationRevision;
                }
            };
            Controller.Finished += data =>
            {
                finished = data;
                finishedRevision = Controller.PresentationRevision;
            };
            Battle.World.Events.EmitEvent("test_marker", Battle.World.Tick, new Dictionary<string, object>());

            Controller.AdvanceFrame(0.1f);

            Assert.AreEqual(initialTick + 1, Battle.World.Tick);
            Assert.AreEqual(1, Controller.LastTicksAdvanced);
            Assert.AreEqual(Battle.World.Tick, markerTick);
            Assert.Less(markerRevision, Controller.PresentationRevision);
            Assert.AreEqual(Battle.World.Tick, Controller.LastPresentationTick);

            Battle.SetResult(new Dictionary<string, object>
            {
                ["winner"] = "blue",
                ["reason"] = "time"
            });
            int beforeFinish = Controller.PresentationRevision;
            Controller.AdvanceFrame(0f);

            Assert.NotNull(finished);
            Assert.AreEqual("blue", finished.Result.Winner);
            Assert.AreEqual(beforeFinish, finishedRevision);
            Assert.AreEqual(beforeFinish + 1, Controller.PresentationRevision);
            Assert.AreSame(finished, Controller.CurrentAar);
        }

        [Test]
        public void FogOmitsUnknownEnemiesButKeepsModelsAndUsesReports()
        {
            Battle = CreateTutorialBattle();
            Platoon enemy = EnemyPlatoon();
            List<ElementVisualState> initialStates = BattleViewAdapter.CreateElementStates(Battle);
            Assert.AreEqual(Battle.Elements.Count, initialStates.Count);
            Reveal(Battle, enemy, new SimVec2(1150.0, 2050.0));
            Element target = enemy.Elements[0];
            Element shooter = Battle.GetTaskForce(Battle.PlayerSide).Companies[0].AllElements()[0];
            shooter.TargetId = target.Id;

            List<ElementVisualState> states = BattleViewAdapter.CreateElementStates(Battle);
            Assert.AreEqual(target.Id, states.Find(state => state.Id == shooter.Id).TargetId);
            Assert.IsNotNull(states.Find(state => state.Id == target.Id));

            BattlefieldSymbolSnapshot hidden = BattleViewAdapter.CreateSymbolSnapshot(Battle, null, true);
            MinimapSnapshot hiddenMap = BattleViewAdapter.CreateMinimapSnapshot(Battle, true);
            Assert.IsFalse(hidden.Units.Exists(unit => unit.Side != Battle.PlayerSide));
            Assert.IsFalse(hiddenMap.Contacts.Exists(contact => contact.Side != Battle.PlayerSide));

            var reportPosition = new SimVec2(1234.0, 2345.0);
            Battle.GetTaskForce(Battle.PlayerSide).KnownEnemies[target.Id] = new Dictionary<string, object>
            {
                ["pos"] = reportPosition,
                ["tick"] = Battle.World.Tick,
                ["class"] = target.Def.ElementClass
            };
            BattlefieldSymbolSnapshot reported = BattleViewAdapter.CreateSymbolSnapshot(Battle, null, true);
            SymbolUnitSnapshot reportedUnit = reported.Units.Find(unit => unit.Side != Battle.PlayerSide);
            Assert.NotNull(reportedUnit);
            Assert.AreEqual(SymbolContactState.Reported, reportedUnit.Contact);
            Assert.AreEqual(reportPosition.X, reportedUnit.Position.x, 0.001f);
            Assert.AreEqual(reportPosition.Y, reportedUnit.Position.y, 0.001f);
            MinimapContactSnapshot reportedContact = BattleViewAdapter.CreateMinimapSnapshot(Battle, true)
                .Contacts.Find(contact => contact.Side != Battle.PlayerSide);
            Assert.NotNull(reportedContact);
            Assert.AreEqual(reportPosition.X, reportedContact.Position.x, 0.001f);
            Assert.AreEqual(reportPosition.Y, reportedContact.Position.y, 0.001f);

            Battle.Detection.Seen[Battle.PlayerSide][target.Id] = Battle.World.Tick;
            BattlefieldSymbolSnapshot current = BattleViewAdapter.CreateSymbolSnapshot(Battle, null, true);
            SymbolUnitSnapshot currentUnit = current.Units.Find(unit => unit.Side != Battle.PlayerSide);
            Assert.AreEqual(SymbolContactState.Current, currentUnit.Contact);
            Assert.AreEqual(target.Pos.X, currentUnit.Position.x, 0.001f);
            Assert.AreEqual(target.Pos.Y, currentUnit.Position.y, 0.001f);

            BattlefieldSymbolSnapshot noFog = BattleViewAdapter.CreateSymbolSnapshot(Battle, null, false);
            Assert.IsTrue(noFog.Units.Exists(unit => unit.Side != Battle.PlayerSide));
        }

        [Test]
        public void ElementSnapshotsPreserveInterpolationStateAndHiddenTargets()
        {
            Battle = CreateTutorialBattle();
            Element source = Battle.GetTaskForce(Battle.PlayerSide).Companies[0].AllElements()[0];
            Element target = EnemyPlatoon().Elements[0];
            source.PrevPos = source.Pos - new SimVec2(3.0, 4.0);
            source.PrevHeading = source.Heading - 0.25;
            source.TargetId = target.Id;

            ElementVisualState state = BattleViewAdapter.CreateElementStates(Battle)
                .Find(candidate => candidate.Id == source.Id);

            Assert.NotNull(state);
            Assert.AreEqual(source.Def.Id, state.ElementId);
            Assert.AreEqual(source.Def.ElementClass, state.ElementClass);
            Assert.AreEqual(source.Platoon.Loadout, state.Loadout);
            Assert.AreEqual(source.PrevPos.X, state.PreviousPosition.x, 0.001f);
            Assert.AreEqual(source.PrevPos.Y, state.PreviousPosition.y, 0.001f);
            Assert.AreEqual(source.Pos.X, state.Position.x, 0.001f);
            Assert.AreEqual(source.Pos.Y, state.Position.y, 0.001f);
            Assert.AreEqual(source.PrevHeading, state.PreviousHeading, 0.0001f);
            Assert.AreEqual(target.Id, state.TargetId);
            Assert.AreEqual(Battle.World.Tick, state.LastSeenTick);
            Assert.AreEqual(Battle.Terrain.GroundHeight(source.Pos), state.TerrainHeight, 0.001f);
            Assert.AreEqual(source.IsMounted(), state.IsMounted);
            Assert.AreEqual(source.OnMap, state.OnMap);
        }

        [Test]
        public void TutorialCompositionSuppressesNormalAar()
        {
            Compose();
            TutorialController tutorial = Controller.StartTutorial();
            Assert.IsTrue(tutorial.IsActive);
            Assert.IsNotEmpty(Controller.TutorialState.CompanyUnitId);
            Assert.IsNotEmpty(Controller.TutorialState.EnemyUnitId);
            bool finished = false;
            Controller.Finished += _ => finished = true;

            Battle.SetResult(new Dictionary<string, object> { ["winner"] = "red", ["reason"] = "collapse" });
            Controller.AdvanceFrame(0f);

            Assert.IsFalse(finished);
            Assert.IsTrue(Controller.TutorialState.HasBattleResult);
            Assert.IsTrue(tutorial.SuppressNormalAar);
        }

        [Test]
        public void GuideOverlayRestoresPauseAndInputState()
        {
            Compose();
            Battle.World.SetPaused(false);

            Controller.OpenGuide();

            Assert.IsTrue(Controller.IsGuideOpen);
            Assert.IsTrue(Battle.World.Paused);
            Assert.IsFalse(Controller.Input.enabled);
            Assert.IsFalse(Controller.RtsCamera.InputEnabled);

            Controller.CloseOverlay();

            Assert.IsFalse(Controller.IsGuideOpen);
            Assert.IsFalse(Battle.World.Paused);
            Assert.IsTrue(Controller.Input.enabled);
            Assert.IsTrue(Controller.RtsCamera.InputEnabled);
        }

        [Test]
        public void SymbolLabelsAreCreatedOutsideGraphicRebuild()
        {
            Compose();
            string reentrant = null;
            Application.LogCallback capture = (condition, stackTrace, type) =>
            {
                if (type == LogType.Error && condition.StartsWith("Trying to add SymbolLabel_", StringComparison.Ordinal))
                    reentrant = condition;
            };
            Application.logMessageReceived += capture;
            try
            {
                Canvas.ForceUpdateCanvases();
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            Assert.IsNull(reentrant);
            Assert.Greater(Controller.Symbols.VisibleLabelCount, 0);
        }

        [Test]
        public void DisposeReleasesOwnedBattleAndRestoresEnvironment()
        {
            bool previousFog = RenderSettings.fog;
            float previousDensity = RenderSettings.fogDensity;
            Material previousSkybox = RenderSettings.skybox;
            Compose();
            CoreBattle owned = Battle;

            Controller.Dispose();
            Controller.Dispose();

            Assert.IsTrue(Controller.IsDisposed);
            Assert.IsNull(Controller.Battle);
            Assert.IsTrue(Root == null);
            Assert.AreEqual(0, owned.Elements.Count);
            Assert.AreEqual(previousFog, RenderSettings.fog);
            Assert.AreEqual(previousDensity, RenderSettings.fogDensity);
            Assert.AreSame(previousSkybox, RenderSettings.skybox);
        }

        [Test]
        public void EventVpSupportsWinnerMapAndObjectiveScalar()
        {
            Battle = CreateTutorialBattle();
            var ended = new SimEvent("battle_ended", 4400, new Dictionary<string, object>
            {
                ["winner"] = "blue",
                ["vp"] = new Dictionary<string, object> { ["blue"] = 12.0, ["red"] = 4.0 }
            });
            var objective = new SimEvent("objective_done", 100, new Dictionary<string, object>
            {
                ["side"] = "blue",
                ["type"] = "seize",
                ["vp"] = 8
            });

            Assert.AreEqual(12, BattleViewAdapter.CreateEventData(Battle, ended).VictoryPoints);
            Assert.AreEqual(8, BattleViewAdapter.CreateEventData(Battle, objective).VictoryPoints);
        }

        [Test]
        public void EventDataConvertsEmittedOrderTypeNames()
        {
            Battle = CreateTutorialBattle();
            Company company = Battle.GetTaskForce(Battle.PlayerSide).Companies[0];
            var order = new Order { Type = OrderType.SEIZE, TargetUnitId = company.Id };
            var received = new SimEvent("order_received", 100, new Dictionary<string, object>
            {
                ["order"] = order.Id,
                ["unit"] = order.TargetUnitId,
                ["type"] = order.TypeName(),
                ["side"] = company.Side
            });

            BattleEventData data = BattleViewAdapter.CreateEventData(Battle, received);

            Assert.IsTrue(data.HasUnit);
            Assert.AreEqual(BattleCommandType.Seize, data.OrderType);
        }

        [Test]
        public void AarMappingAdaptsActualBattleStatsSummary()
        {
            Battle = CreateTutorialBattle();
            Battle.RunTicks(12);
            Battle.Scoring.Vp[Battle.PlayerSide] = 12.5;
            Element lost = Battle.GetTaskForce(Battle.PlayerSide).Companies[0].AllElements()[0];
            Battle.World.Events.EmitEvent("point_captured", Battle.World.Tick, new Dictionary<string, object>
            {
                ["side"] = Battle.PlayerSide,
                ["name"] = "테스트 거점"
            });
            Battle.World.Events.EmitEvent("element_destroyed", Battle.World.Tick, new Dictionary<string, object>
            {
                ["element"] = lost.Id,
                ["side"] = lost.Side,
                ["unit"] = lost.Platoon.Id,
                ["pos"] = lost.Pos
            });
            Battle.SetResult(new Dictionary<string, object>
            {
                ["winner"] = Battle.PlayerSide,
                ["reason"] = "time"
            });

            AarData data = BattleViewAdapter.CreateAar(Battle.Stats.Summary());

            Assert.AreEqual(Battle.PlayerSide, data.Result.Winner);
            Assert.AreEqual("time", data.Result.Reason);
            Assert.AreEqual(1.2f, data.DurationSeconds, 0.001f);
            Assert.AreEqual(10f, data.TickRate);
            Assert.AreEqual(2, data.Sides.Count);
            AarSideData side = data.FindSide(Battle.PlayerSide);
            Assert.NotNull(side);
            Assert.AreEqual(12.5f, side.VictoryPoints, 0.001f);
            Assert.AreEqual(1, side.LossesTotal);
            Assert.AreEqual(1, side.Losses.Count);
            Assert.AreEqual(lost.Def.ElementClass, side.Losses[0].ClassName);
            Assert.AreEqual(Battle.Scoring.Points.Count, data.Points.Count);
            Assert.IsTrue(data.Timeline.Exists(entry => entry.Type == "point_captured"));
            Assert.IsTrue(data.Timeline.Exists(entry => entry.Type == "battle_ended"));
        }
    }
}
