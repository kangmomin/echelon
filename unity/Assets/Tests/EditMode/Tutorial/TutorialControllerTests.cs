using System;
using System.Collections.Generic;
using Echelon.Unity.UI.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Tests.UI.Tutorial
{
    public sealed class TutorialControllerTests
    {
        private const string Company = "company";
        private const string Platoon1 = "platoon_1";
        private const string Platoon2 = "platoon_2";
        private const string Enemy = "enemy";

        private TutorialState _state;
        private TutorialController _controller;
        private List<TutorialIntent> _intents;

        [SetUp]
        public void SetUp()
        {
            _state = new TutorialState
            {
                PlayerSide = "blue",
                CompanyUnitId = Company,
                EnemyUnitId = Enemy,
                CompanyPosition = new Vector2(750f, 650f),
                CameraFocus = new Vector2(100f, 100f),
                CameraDistance = 1000f,
                CompanyFormationId = "co_column",
                CompanyRoe = "fire_at_will",
                TimeSpeed = 1f
            };
            _state.CompanyElements.Add(new TutorialElementState(true, Vector2.zero));
            _state.EnemyElements.Add(new TutorialElementState(true, Vector2.zero));
            _controller = new TutorialController(_state);
            _intents = new List<TutorialIntent>();
            _controller.IntentRequested += _intents.Add;
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        [Test]
        public void Welcome_WaitsForNextAndStartsCameraBaselineOnEntry()
        {
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Welcome));
            Assert.That(_controller.SuppressNormalAar, Is.True);
            _controller.Tick(10f);
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Welcome));

            _controller.Next();
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Camera));
            _state.CameraFocus = new Vector2(251f, 100f);
            _state.CameraDistance = 1201f;
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void Camera_RequiresBothPanAndZoomAboveThresholds()
        {
            GoTo(TutorialStepId.Camera);
            _state.CameraFocus = new Vector2(250.01f, 100f);
            Assert.That(Satisfied(), Is.False);

            _state.CameraFocus = new Vector2(100f, 100f);
            _state.CameraDistance = 1200.01f;
            Assert.That(Satisfied(), Is.False);

            _state.CameraFocus = new Vector2(250.01f, 100f);
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void Camera_ThresholdsAreStrictAndZeroBaselineCannotPass()
        {
            GoTo(TutorialStepId.Camera);
            _state.CameraFocus = new Vector2(250f, 100f);
            _state.CameraDistance = 1200f;
            Assert.That(Satisfied(), Is.False);

            _controller.Quit();
            _state.CameraFocus = new Vector2(100f, 100f);
            _state.CameraDistance = 0f;
            _controller.Start();
            GoTo(TutorialStepId.Camera);
            _state.CameraFocus = new Vector2(500f, 100f);
            _state.CameraDistance = 1000f;
            Assert.That(Satisfied(), Is.False);
        }

        [Test]
        public void CompanySelection_RequiresExactlyTheTutorialCompany()
        {
            GoTo(TutorialStepId.CompanySelection);
            Assert.That(Satisfied(), Is.False);

            SetSelection(Unit(Company), Unit(Platoon1, Company));
            Assert.That(Satisfied(), Is.False);
            SetSelection(Unit(Platoon1, Company));
            Assert.That(Satisfied(), Is.False);
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void Subselection_LatchesPlatoonsThenRequiresCompanyAgain()
        {
            GoTo(TutorialStepId.Subselection);
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.False);

            SetSelection(Unit(Platoon1, Company), Unit(Platoon2, Company));
            Assert.That(Satisfied(), Is.False);
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void Subselection_CompanyItselfInMultiSelectionDoesNotCountAsPlatoons()
        {
            GoTo(TutorialStepId.Subselection);
            SetSelection(Unit(Company), Unit(Platoon1, Company));
            Assert.That(Satisfied(), Is.False);
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.False);
        }

        [Test]
        public void ControlGroups_RequiresAssignedGroupClearedSelectionThenRecall()
        {
            GoTo(TutorialStepId.ControlGroups);
            _state.ControlGroup1.Add(Company);
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.False);

            SetSelection();
            Assert.That(Satisfied(), Is.False);
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void ControlGroups_CompanyRecallWithoutGroupAssignmentDoesNotPass()
        {
            GoTo(TutorialStepId.ControlGroups);
            SetSelection();
            Assert.That(Satisfied(), Is.False);
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.False);
        }

        [Test]
        public void Formation_UsesAppliedCompanyFormationNotOrderIntent()
        {
            GoTo(TutorialStepId.Formation);
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.WithoutPosition(
                TutorialOrderType.Other,
                Company,
                string.Empty));
            Assert.That(Satisfied(), Is.False);

            _state.CompanyFormationId = "co_line";
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void TimeControls_RequiresPauseFastSpeedThenResume()
        {
            GoTo(TutorialStepId.TimeControls);
            Assert.That(Satisfied(), Is.False);

            _state.IsPaused = true;
            Assert.That(Satisfied(), Is.False);
            _state.TimeSpeed = 2f;
            Assert.That(Satisfied(), Is.False);
            _state.IsPaused = false;
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void TimeControls_FastSpeedThresholdIsStrict()
        {
            GoTo(TutorialStepId.TimeControls);
            _state.IsPaused = true;
            _state.TimeSpeed = 1.01f;
            Assert.That(Satisfied(), Is.False);
            _state.IsPaused = false;
            Assert.That(Satisfied(), Is.False);

            _state.IsPaused = true;
            _state.TimeSpeed = 1.011f;
            Assert.That(Satisfied(), Is.False);
            _state.IsPaused = false;
            Assert.That(Satisfied(), Is.True);
        }

        [TestCase(TutorialOrderType.Move)]
        [TestCase(TutorialOrderType.FastMove)]
        [TestCase(TutorialOrderType.AttackMove)]
        public void MovementOrder_AcceptsGodotMovementTypesNearMarker(TutorialOrderType type)
        {
            GoTo(TutorialStepId.MovementOrder);
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                type,
                Company,
                string.Empty,
                TutorialController.TutorialWaypoint));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void MovementOrder_RejectsWrongTypeUnitDistanceAndMissingPosition()
        {
            GoTo(TutorialStepId.MovementOrder);
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.Seize, Company, string.Empty, TutorialController.TutorialWaypoint));
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.Move, "other", string.Empty, TutorialController.TutorialWaypoint));
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.Move,
                Company,
                string.Empty,
                TutorialController.TutorialWaypoint + new Vector2(351f, 0f)));
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.WithoutPosition(
                TutorialOrderType.Move, Company, string.Empty));
            Assert.That(Satisfied(), Is.False);

            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.Move,
                Platoon1,
                Company,
                TutorialController.TutorialWaypoint + new Vector2(350f, 0f)));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void MovementOrder_UsesAccumulatedRealPlayerOrderLog()
        {
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.Move,
                Company,
                string.Empty,
                TutorialController.TutorialWaypoint));
            GoTo(TutorialStepId.MovementOrder);
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void CommandDelay_RequiresReceiptDuringStepAndAliveElementMovement()
        {
            _controller.ObserveSimulationEvent(TutorialSimulationEvent.OrderReceived(Platoon1, Company));
            GoTo(TutorialStepId.CommandDelay);
            Assert.That(Satisfied(), Is.False);

            _controller.ObserveSimulationEvent(TutorialSimulationEvent.OrderReceived(Company, "task_force"));
            Assert.That(Satisfied(), Is.False);
            _controller.ObserveSimulationEvent(TutorialSimulationEvent.OrderReceived(Platoon1, Company));
            Assert.That(Satisfied(), Is.False);

            _state.CompanyElements[0] = new TutorialElementState(false, new Vector2(5f, 0f));
            Assert.That(Satisfied(), Is.False);
            _state.CompanyElements[0] = new TutorialElementState(true, new Vector2(1f, 0f));
            Assert.That(Satisfied(), Is.False);
            _state.CompanyElements[0] = new TutorialElementState(true, new Vector2(1.01f, 0f));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void CommandDelay_ReceiptFromAnotherCompanyDoesNotCount()
        {
            GoTo(TutorialStepId.CommandDelay);
            _state.CompanyElements[0] = new TutorialElementState(true, new Vector2(2f, 0f));
            _controller.ObserveSimulationEvent(TutorialSimulationEvent.OrderReceived("other_platoon", "other_company"));
            Assert.That(Satisfied(), Is.False);
            _controller.ObserveSimulationEvent(TutorialSimulationEvent.OrderReceived(Platoon1, Company));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void RulesOfEngagement_RequiresAppliedHoldThenFireAtWill()
        {
            GoTo(TutorialStepId.RulesOfEngagement);
            Assert.That(Satisfied(), Is.False);
            _state.CompanyRoe = "hold_fire";
            Assert.That(Satisfied(), Is.False);
            _state.CompanyRoe = "fire_at_will";
            Assert.That(Satisfied(), Is.True);
        }

        [TestCase(TutorialOrderType.AttackMove)]
        [TestCase(TutorialOrderType.Seize)]
        public void AttackMove_AcceptsAttackOrSeizeFromAnyPlayerUnit(TutorialOrderType type)
        {
            GoTo(TutorialStepId.AttackMove);
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                type,
                "any_player_unit",
                string.Empty,
                TutorialController.TutorialEnemy + new Vector2(700f, 0f)));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void AttackMove_RejectsOtherOrdersAndTargetsOutsideRadius()
        {
            GoTo(TutorialStepId.AttackMove);
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.Move, Company, string.Empty, TutorialController.TutorialEnemy));
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.AttackMove,
                Company,
                string.Empty,
                TutorialController.TutorialEnemy + new Vector2(701f, 0f)));
            Assert.That(Satisfied(), Is.False);
        }

        [Test]
        public void AttackMoveEntry_RevealsOffMapEnemyAtGodotPositionAndHeading()
        {
            GoTo(TutorialStepId.AttackMove);
            TutorialIntent reveal = AssertSingleIntent(TutorialIntentKind.RevealEnemy);
            Assert.That(reveal.UnitId, Is.EqualTo(Enemy));
            Assert.That(reveal.SimPosition, Is.EqualTo(TutorialController.TutorialEnemy));
            Assert.That(reveal.HeadingRadians, Is.EqualTo(Mathf.PI).Within(0.0001f));
            Assert.That(_state.EnemyOnMap, Is.False, "The parent must perform the actual reveal.");
            Assert.That(_controller.Markers.Count, Is.EqualTo(1));
            Assert.That(_controller.Markers[0].SimPosition, Is.EqualTo(TutorialController.TutorialEnemy));
        }

        [Test]
        public void AttackMoveEntry_DoesNotRevealEnemyAlreadyOnMap()
        {
            _state.EnemyOnMap = true;
            GoTo(TutorialStepId.AttackMove);
            Assert.That(_intents.Exists(x => x.Kind == TutorialIntentKind.RevealEnemy), Is.False);
        }

        [Test]
        public void FireSupport_RequiresSuccessfulPlayerSideSimulationEvent()
        {
            GoTo(TutorialStepId.FireSupport);
            _intents.Clear();
            _controller.RequestFireMode(TutorialFireMode.Mortar);
            Assert.That(Satisfied(), Is.False);

            _controller.ObserveSimulationEvent(TutorialSimulationEvent.FireRequested("red"));
            Assert.That(Satisfied(), Is.False);
            _controller.ObserveSimulationEvent(TutorialSimulationEvent.FireRequested("blue"));
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void FireSupport_UsesAccumulatedSuccessfulRequestEvent()
        {
            _controller.ObserveSimulationEvent(TutorialSimulationEvent.FireRequested("blue"));
            GoTo(TutorialStepId.FireSupport);
            Assert.That(Satisfied(), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EnemyDestruction_AcceptsAllGodotTerminalConditions(int condition)
        {
            GoTo(TutorialStepId.EnemyDestruction);
            switch (condition)
            {
                case 0:
                    _state.EnemyIsDestroyed = true;
                    break;
                case 1:
                    _state.EnemyElements[0] = new TutorialElementState(false, Vector2.zero);
                    break;
                case 2:
                    _state.HasBattleResult = true;
                    break;
                case 3:
                    _state.EnemyUnitId = string.Empty;
                    break;
                case 4:
                    _controller.ObserveSimulationEvent(TutorialSimulationEvent.BattleEnded());
                    Assert.That(_state.HasBattleResult, Is.True);
                    break;
            }
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void EnemyDestruction_AnyLivingElementWithoutBattleResultBlocksCompletion()
        {
            GoTo(TutorialStepId.EnemyDestruction);
            _state.EnemyElements.Add(new TutorialElementState(false, Vector2.zero));
            Assert.That(Satisfied(), Is.False);
        }

        [Test]
        public void Completion_IsFinalPanelInsteadOfNormalAarAndNextQuits()
        {
            GoTo(TutorialStepId.EnemyDestruction);
            _state.EnemyIsDestroyed = true;
            CompleteCurrentAndAdvance();

            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Completion));
            Assert.That(_controller.IsFinalPanelVisible, Is.True);
            Assert.That(_controller.SuppressNormalAar, Is.True);
            Assert.That(_controller.Markers, Is.Empty);
            _controller.Tick(20f);
            _controller.Skip();
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Completion));

            _controller.Next();
            Assert.That(_controller.HasExited, Is.True);
            Assert.That(_controller.IsActive, Is.False);
            Assert.That(_controller.SuppressNormalAar, Is.False);
            AssertSingleIntent(TutorialIntentKind.QuitToMenu);
        }

        [Test]
        public void FullFlow_DrivesAllFourteenStepsThroughBattleObservations()
        {
            _controller.Next();
            _state.CameraFocus = new Vector2(251f, 100f);
            _state.CameraDistance = 1201f;
            CompleteCurrentAndAdvance();
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.CompanySelection));

            SetSelection(Unit(Company));
            CompleteCurrentAndAdvance();
            SetSelection(Unit(Platoon1, Company), Unit(Platoon2, Company));
            Assert.That(Satisfied(), Is.False);
            SetSelection(Unit(Company));
            CompleteCurrentAndAdvance();

            _state.ControlGroup1.Add(Company);
            SetSelection();
            Assert.That(Satisfied(), Is.False);
            SetSelection(Unit(Company));
            CompleteCurrentAndAdvance();

            _state.CompanyFormationId = "co_line";
            CompleteCurrentAndAdvance();
            _state.IsPaused = true;
            _state.TimeSpeed = 2f;
            Assert.That(Satisfied(), Is.False);
            _state.IsPaused = false;
            CompleteCurrentAndAdvance();

            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.Move,
                Company,
                string.Empty,
                TutorialController.TutorialWaypoint));
            CompleteCurrentAndAdvance();
            _controller.ObserveSimulationEvent(TutorialSimulationEvent.OrderReceived(Platoon1, Company));
            _state.CompanyElements[0] = new TutorialElementState(true, new Vector2(2f, 0f));
            CompleteCurrentAndAdvance();

            _state.CompanyRoe = "hold_fire";
            Assert.That(Satisfied(), Is.False);
            _state.CompanyRoe = "fire_at_will";
            CompleteCurrentAndAdvance();
            _controller.ObservePlayerOrder(TutorialPlayerOrderEvent.At(
                TutorialOrderType.AttackMove,
                Company,
                string.Empty,
                TutorialController.TutorialEnemy));
            CompleteCurrentAndAdvance();

            _controller.ObserveSimulationEvent(TutorialSimulationEvent.FireRequested("blue"));
            CompleteCurrentAndAdvance();
            _state.EnemyElements[0] = new TutorialElementState(false, Vector2.zero);
            CompleteCurrentAndAdvance();
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Completion));
            _controller.Next();
            Assert.That(_controller.HasExited, Is.True);
        }

        [Test]
        public void SuccessDelay_LatchesOnceAndCannotBeCancelledByStateChanges()
        {
            GoTo(TutorialStepId.CompanySelection);
            int satisfiedCount = 0;
            _controller.StepSatisfied += _ => satisfiedCount++;
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.True);
            Assert.That(_controller.SuccessDelayRemaining, Is.EqualTo(TutorialController.DoneDelaySeconds));

            SetSelection();
            _controller.Tick(TutorialController.DoneDelaySeconds);
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.CompanySelection));
            _controller.Tick(0.001f);
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Subselection));
            Assert.That(satisfiedCount, Is.EqualTo(1));
        }

        [Test]
        public void Skip_ReachesFinalWithoutPredicatesAndPerformsEntryEffects()
        {
            GoTo(TutorialStepId.Completion);
            Assert.That(_controller.CurrentStepIndex, Is.EqualTo(13));
            Assert.That(_controller.HasExited, Is.False);
            Assert.That(_controller.IsFinalPanelVisible, Is.True);
            Assert.That(_controller.Markers, Is.Empty);
            Assert.That(_intents.Exists(x => x.Kind == TutorialIntentKind.RevealEnemy), Is.True);
        }

        [Test]
        public void Skip_DuringSuccessDelayCancelsPendingAdvance()
        {
            GoTo(TutorialStepId.CompanySelection);
            SetSelection(Unit(Company));
            Assert.That(Satisfied(), Is.True);
            _controller.Skip();
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Subselection));
            _controller.Tick(10f);
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Subselection));
        }

        [Test]
        public void Next_CannotBypassActionPredicates()
        {
            GoTo(TutorialStepId.Camera);
            _controller.Next();
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.Camera));
        }

        [Test]
        public void Quit_ClearsMarkersIsIdempotentAndRejectsLateInput()
        {
            GoTo(TutorialStepId.MovementOrder);
            Assert.That(_controller.Markers.Count, Is.EqualTo(1));
            _controller.Quit();
            _controller.Quit();
            Assert.That(_controller.Markers, Is.Empty);
            Assert.That(_intents.FindAll(x => x.Kind == TutorialIntentKind.QuitToMenu).Count, Is.EqualTo(1));

            _controller.ObserveSimulationEvent(TutorialSimulationEvent.FireRequested("blue"));
            _controller.RequestFireMode(TutorialFireMode.Artillery);
            _controller.RequestCameraFocus(Vector2.one);
            _controller.Tick(10f);
            Assert.That(_intents.Count, Is.EqualTo(1));
            Assert.That(_controller.CurrentStepId, Is.EqualTo(TutorialStepId.MovementOrder));
        }

        [Test]
        public void Markers_MoveTargetIsReplacedByEnemyAndClearedAtCompletion()
        {
            GoTo(TutorialStepId.MovementOrder);
            Assert.That(_controller.Markers.Count, Is.EqualTo(1));
            Assert.That(_controller.Markers[0].SimPosition, Is.EqualTo(TutorialController.TutorialWaypoint));
            Assert.That(_controller.Markers[0].Color, Is.EqualTo(TutorialController.MoveMarkerColor));

            GoTo(TutorialStepId.AttackMove);
            Assert.That(_controller.Markers.Count, Is.EqualTo(1));
            Assert.That(_controller.Markers[0].SimPosition, Is.EqualTo(TutorialController.TutorialEnemy));
            Assert.That(_controller.Markers[0].Color, Is.EqualTo(TutorialController.EnemyMarkerColor));

            GoTo(TutorialStepId.Completion);
            Assert.That(_controller.Markers, Is.Empty);
        }

        [Test]
        public void ExplicitIntents_ExposeCameraCompanyFocusAndFireMode()
        {
            _controller.RequestCameraFocus(new Vector2(10f, 20f));
            _controller.FocusCompany();
            _controller.RequestFireMode(TutorialFireMode.Mortar, true);

            Assert.That(_intents.Count, Is.EqualTo(3));
            Assert.That(_intents[0].Kind, Is.EqualTo(TutorialIntentKind.CameraFocus));
            Assert.That(_intents[0].SimPosition, Is.EqualTo(new Vector2(10f, 20f)));
            Assert.That(_intents[1].SimPosition, Is.EqualTo(_state.CompanyPosition));
            Assert.That(_intents[2].Kind, Is.EqualTo(TutorialIntentKind.FireMode));
            Assert.That(_intents[2].FireMode, Is.EqualTo(TutorialFireMode.Mortar));
            Assert.That(_intents[2].Smoke, Is.True);
        }

        [Test]
        public void EventSource_AttachDetachControlsObservation()
        {
            var source = new FakeEventSource();
            _controller.Attach(source);
            GoTo(TutorialStepId.FireSupport);
            _controller.Detach();
            source.Fire("blue");
            Assert.That(Satisfied(), Is.False);

            _controller.Attach(source);
            source.Fire("blue");
            Assert.That(Satisfied(), Is.True);
        }

        [Test]
        public void Dispose_DetachesSourceAndSuppressesFurtherEvents()
        {
            var source = new FakeEventSource();
            _controller.Attach(source);
            _controller.Dispose();
            source.EndBattle();
            Assert.That(_state.HasBattleResult, Is.False);
            Assert.That(_controller.IsDisposed, Is.True);
        }

        private bool Satisfied()
        {
            _controller.Tick(0f);
            return _controller.IsCurrentStepSatisfied;
        }

        private void CompleteCurrentAndAdvance()
        {
            Assert.That(Satisfied(), Is.True, _controller.CurrentStepId.ToString());
            _controller.Tick(TutorialController.DoneDelaySeconds + 0.01f);
        }

        private void GoTo(TutorialStepId target)
        {
            int guard = 0;
            while (_controller.CurrentStepId != target)
            {
                Assert.That(_controller.CurrentStepIndex, Is.LessThan((int)target));
                _controller.Skip();
                guard++;
                Assert.That(guard, Is.LessThanOrEqualTo(TutorialContent.StepCount));
            }
        }

        private TutorialIntent AssertSingleIntent(TutorialIntentKind kind)
        {
            List<TutorialIntent> matches = _intents.FindAll(x => x.Kind == kind);
            Assert.That(matches.Count, Is.EqualTo(1), kind.ToString());
            return matches[0];
        }

        private void SetSelection(params TutorialSelectedUnit[] selection)
        {
            _state.Selection.Clear();
            for (int i = 0; i < selection.Length; i++)
                _state.Selection.Add(selection[i]);
        }

        private static TutorialSelectedUnit Unit(string id, string parent = "")
        {
            return new TutorialSelectedUnit(id, parent);
        }

        private sealed class FakeEventSource : ITutorialEventSource
        {
            public event Action<TutorialPlayerOrderEvent> PlayerOrderIssued;
            public event Action<TutorialSimulationEvent> SimulationEventEmitted;

            internal void Fire(string side)
            {
                SimulationEventEmitted?.Invoke(TutorialSimulationEvent.FireRequested(side));
            }

            internal void EndBattle()
            {
                SimulationEventEmitted?.Invoke(TutorialSimulationEvent.BattleEnded());
            }

            internal void Issue(TutorialPlayerOrderEvent orderEvent)
            {
                PlayerOrderIssued?.Invoke(orderEvent);
            }
        }
    }
}
