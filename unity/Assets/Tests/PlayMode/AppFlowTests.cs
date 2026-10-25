using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Echelon.Core;
using Echelon.Unity.App;
using Echelon.Unity.UI.Aar;
using Echelon.Unity.UI.Battle;
using Echelon.Unity.UI.Builder;
using Echelon.Unity.UI.MainMenu;
using Echelon.Unity.UI.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using CoreBattle = Echelon.Core.Battle;

namespace Echelon.Unity.Tests.PlayMode
{
    public sealed class AppFlowTests
    {
        private Func<IForceStore> _previousStoreProvider;
        private EventSystem[] _existingEventSystems;
        private string _storePath;
        private string _storeFile;
        private AppFlowController _app;

        [SetUp]
        public void SetUp()
        {
            _previousStoreProvider = AppFlowController.ForceStoreProvider;
            _existingEventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            _storePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "validation", "flow_store"));
            _storeFile = Path.Combine(_storePath, "flow_test.json");
            AppFlowController.ForceStoreProvider = () => new FileForceStore(_storePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (AppFlowController.Instance != null)
            {
                AppFlowController.Instance.ShowMenu();
                UnityEngine.Object.DestroyImmediate(AppFlowController.Instance.gameObject);
            }
            GameObject screens = GameObject.Find("Screens");
            if (screens != null)
                UnityEngine.Object.DestroyImmediate(screens);
            foreach (EventSystem eventSystem in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (!Array.Exists(_existingEventSystems, existing => existing == eventSystem))
                    UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            }
            AppFlowController.ForceStoreProvider = _previousStoreProvider;
            if (File.Exists(_storeFile))
                File.Delete(_storeFile);
            if (Directory.Exists(_storePath) && Directory.GetFiles(_storePath).Length == 0)
                Directory.Delete(_storePath);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BuilderSaveDeployBattleAndAarUseRealUi()
        {
            yield return EnsureApp();
            MainMenu menu = Find<MainMenu>();
            Assert.NotNull(menu);
            menu.SkirmishButton.onClick.Invoke();
            yield return null;

            ForceBuilder builder = Find<ForceBuilder>();
            Assert.NotNull(builder);
            Assert.IsTrue(builder.CanStart);
            builder.NameInput.text = "flow_test";
            builder.SaveButton.onClick.Invoke();
            Assert.IsTrue(File.Exists(_storeFile));
            int artillery = builder.Current.ArtilleryMissions;
            builder.Controller.SetName("changed");
            builder.Controller.SetArtilleryMissions(0);
            builder.LoadButton.onClick.Invoke();
            Assert.That(builder.Current.Name, Is.EqualTo("flow_test"));
            Assert.That(builder.Current.ArtilleryMissions, Is.EqualTo(artillery));

            builder.StartButton.onClick.Invoke();
            yield return null;
            BattleController battle = _app.Battle;
            Assert.NotNull(battle);
            Assert.IsTrue(battle.Battle.Deploying);
            Company company = battle.Battle.GetTaskForce(battle.Battle.PlayerSide).Companies[0];
            battle.Input.ClickUnit(company.Id);
            DeploymentZoneData zone = BattleViewAdapter.CreateDeploymentZone(battle.Battle);
            Assert.That(battle.DeploySelection(zone.Zone.center, 0f), Is.GreaterThan(0));
            battle.DeploymentPanel.StartButton.onClick.Invoke();
            Assert.IsFalse(battle.Battle.Deploying);

            AdvanceSeconds(battle, 2600);
            AarScreen aar = Find<AarScreen>();
            Assert.NotNull(aar);
            Assert.IsTrue(battle.Battle.HasResult);
            aar.MenuButton.onClick.Invoke();
            yield return null;
            Assert.NotNull(Find<MainMenu>());
            Assert.IsNull(_app.Battle);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TutorialCompletesAllFourteenStepsWithRealCommands()
        {
            yield return EnsureApp();
            Find<MainMenu>().TutorialButton.onClick.Invoke();
            yield return null;
            BattleController battle = _app.Battle;
            Assert.NotNull(battle);
            TutorialController tutorial = battle.Tutorial;
            TutorialPanel panel = Find<TutorialPanel>();
            Assert.NotNull(panel);
            Assert.That(tutorial.CurrentStepId, Is.EqualTo(TutorialStepId.Welcome));
            var visited = new List<TutorialStepId> { TutorialStepId.Welcome };
            tutorial.StepEntered += step => visited.Add(step.Id);
            Company company = battle.Battle.GetTaskForce(battle.Battle.PlayerSide).Companies[0];

            panel.NextButton.onClick.Invoke();
            Assert.That(tutorial.CurrentStepId, Is.EqualTo(TutorialStepId.Camera));
            battle.RtsCamera.Pan(Vector2.right, 1f);
            battle.RtsCamera.ZoomIn();
            battle.RtsCamera.ZoomIn();
            AdvanceTutorial(battle, TutorialStepId.CompanySelection);

            battle.Input.ClickUnit(company.Id);
            AdvanceTutorial(battle, TutorialStepId.Subselection);
            battle.Input.ClickUnit(company.Platoons[0].Id);
            battle.Input.ClickUnit(company.Platoons[1].Id, true);
            battle.AdvanceFrame(0f);
            battle.Tutorial.Tick(0f);
            battle.Input.ClickUnit(company.Id);
            AdvanceTutorial(battle, TutorialStepId.ControlGroups);

            battle.Input.AssignGroup(1);
            battle.Input.ClearSelection();
            battle.AdvanceFrame(0f);
            battle.Tutorial.Tick(0f);
            battle.Input.RecallGroup(1);
            AdvanceTutorial(battle, TutorialStepId.Formation);

            battle.Input.SetFormation("co_line");
            AdvanceSeconds(battle, 30);
            AdvanceTutorial(battle, TutorialStepId.TimeControls);
            battle.Input.TogglePause();
            battle.Input.ChangeSpeed(1);
            battle.AdvanceFrame(0f);
            battle.Tutorial.Tick(0f);
            battle.Input.TogglePause();
            AdvanceTutorial(battle, TutorialStepId.MovementOrder);

            battle.Input.ClickUnit(company.Id);
            Assert.That(battle.Input.IssueMovementOrders(TutorialController.TutorialWaypoint).Count, Is.EqualTo(1));
            AdvanceTutorial(battle, TutorialStepId.CommandDelay);
            AdvanceSeconds(battle, 60);
            AdvanceTutorial(battle, TutorialStepId.RulesOfEngagement);

            battle.Input.ToggleHoldFire();
            AdvanceSeconds(battle, 30);
            battle.Tutorial.Tick(0f);
            battle.Input.ToggleHoldFire();
            AdvanceSeconds(battle, 30);
            AdvanceTutorial(battle, TutorialStepId.AttackMove);
            battle.Input.ClickUnit(company.Id);
            battle.Input.SetOrderMode(BattleCommandType.AttackMove);
            Assert.That(battle.Input.IssueMovementOrders(TutorialController.TutorialEnemy).Count, Is.EqualTo(1));
            AdvanceTutorial(battle, TutorialStepId.FireSupport);

            battle.Input.SetFireMode(BattleFireAsset.Mortar, false);
            Assert.NotNull(battle.Input.RequestFireSupport(TutorialController.TutorialEnemy));
            AdvanceTutorial(battle, TutorialStepId.EnemyDestruction);
            for (int attempt = 0; attempt < 16 && tutorial.CurrentStepId == TutorialStepId.EnemyDestruction; attempt++)
            {
                AdvanceSeconds(battle, 30);
                tutorial.Tick(0f);
                tutorial.Tick(TutorialController.DoneDelaySeconds + 0.1f);
            }
            Assert.That(tutorial.CurrentStepId, Is.EqualTo(TutorialStepId.Completion));
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(TutorialStepId)), visited);
            panel.NextButton.onClick.Invoke();
            yield return null;
            Assert.NotNull(Find<MainMenu>());
            Assert.IsNull(_app.Battle);
        }

        private IEnumerator EnsureApp()
        {
            _app = AppFlowController.Instance;
            if (_app == null)
                _app = new GameObject("AppFlowTest").AddComponent<AppFlowController>();
            _app.ShowMenu();
            yield return null;
            Assert.NotNull(Find<MainMenu>());
        }

        private static T Find<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindFirstObjectByType<T>();
        }

        private static void AdvanceSeconds(BattleController controller, double seconds)
        {
            CoreBattle battle = controller.Battle;
            int ticks = (int)Math.Ceiling(seconds * SimConst.TickRate);
            for (int i = 0; i < ticks && !battle.HasResult; i++)
                battle.StepTick();
            controller.AdvanceFrame(0f);
        }

        private static void AdvanceTutorial(BattleController controller, TutorialStepId expected)
        {
            controller.AdvanceFrame(0f);
            controller.Tutorial.Tick(0f);
            controller.Tutorial.Tick(TutorialController.DoneDelaySeconds + 0.1f);
            Assert.That(controller.Tutorial.CurrentStepId, Is.EqualTo(expected));
        }
    }
}
