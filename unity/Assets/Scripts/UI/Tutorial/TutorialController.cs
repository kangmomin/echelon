using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Echelon.Unity.UI.Tutorial
{
    public sealed class TutorialController : IDisposable
    {
        public const float DoneDelaySeconds = 1.2f;
        public const float CameraPanThreshold = 150f;
        public const float CameraZoomThreshold = 0.2f;
        public const float MoveOrderRadius = 350f;
        public const float AttackOrderRadius = 700f;
        public const float MovingSpeedThreshold = 1f;

        public static readonly Vector2 TutorialStart = new Vector2(750f, 650f);
        public static readonly Vector2 TutorialWaypoint = new Vector2(900f, 1500f);
        public static readonly Vector2 TutorialEnemy = new Vector2(1150f, 2050f);
        public static readonly Color MoveMarkerColor = new Color(1f, 0.85f, 0.3f);
        public static readonly Color EnemyMarkerColor = new Color(1f, 0.4f, 0.35f);

        private readonly TutorialState _state;
        private readonly List<TutorialPlayerOrderEvent> _orders = new List<TutorialPlayerOrderEvent>();
        private readonly List<TutorialWorldMarker> _markers = new List<TutorialWorldMarker>();
        private readonly ReadOnlyCollection<TutorialWorldMarker> _markerView;

        private ITutorialEventSource _eventSource;
        private int _index = -1;
        private float _doneTimer = -1f;
        private Vector2 _cameraStart;
        private float _cameraDistanceStart;
        private bool _sawPlatoons;
        private bool _sawEmptyAfterGroup;
        private bool _sawPause;
        private bool _sawFast;
        private bool _sawHold;
        private bool _fireRequested;
        private bool _listenPlatoonOrders;
        private bool _platoonGotOrder;

        public TutorialController(TutorialState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _markerView = _markers.AsReadOnly();
        }

        public event Action<TutorialStepDefinition> StepEntered;
        public event Action<TutorialStepId> StepSatisfied;
        public event Action<TutorialIntent> IntentRequested;
        public event Action MarkersChanged;
        public event Action Exited;

        public TutorialState State => _state;
        public bool IsActive { get; private set; }
        public bool HasExited { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsCurrentStepSatisfied { get; private set; }
        public float SuccessDelayRemaining => _doneTimer;
        public int CurrentStepIndex => _index;
        public TutorialStepId CurrentStepId => (TutorialStepId)Math.Max(0, _index);
        public TutorialStepDefinition CurrentStep => TutorialContent.Get(CurrentStepId);
        public bool IsFinalPanelVisible => IsActive && CurrentStepId == TutorialStepId.Completion;
        public bool SuppressNormalAar => IsActive && !HasExited;
        public IReadOnlyList<TutorialWorldMarker> Markers => _markerView;

        public void Attach(ITutorialEventSource eventSource)
        {
            ThrowIfDisposed();
            if (eventSource == null)
                throw new ArgumentNullException(nameof(eventSource));
            if (ReferenceEquals(_eventSource, eventSource))
                return;

            Detach();
            _eventSource = eventSource;
            _eventSource.PlayerOrderIssued += ObservePlayerOrder;
            _eventSource.SimulationEventEmitted += ObserveSimulationEvent;
        }

        public void Detach()
        {
            if (_eventSource == null)
                return;
            _eventSource.PlayerOrderIssued -= ObservePlayerOrder;
            _eventSource.SimulationEventEmitted -= ObserveSimulationEvent;
            _eventSource = null;
        }

        public void Start()
        {
            ThrowIfDisposed();
            if (IsActive)
                return;

            _orders.Clear();
            _index = -1;
            _doneTimer = -1f;
            _sawPlatoons = false;
            _sawEmptyAfterGroup = false;
            _sawPause = false;
            _sawFast = false;
            _sawHold = false;
            _fireRequested = false;
            _listenPlatoonOrders = false;
            _platoonGotOrder = false;
            IsCurrentStepSatisfied = false;
            HasExited = false;
            IsActive = true;
            ClearMarkers();
            Advance();
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive)
                return;
            if (float.IsNaN(deltaTime) || deltaTime < 0f)
                deltaTime = 0f;

            if (_doneTimer >= 0f)
            {
                _doneTimer -= deltaTime;
                if (_doneTimer < 0f)
                    Advance();
                return;
            }

            if (!CurrentStep.RequiresCompletion || !EvaluateCurrentStep())
                return;

            IsCurrentStepSatisfied = true;
            _doneTimer = DoneDelaySeconds;
            StepSatisfied?.Invoke(CurrentStepId);
        }

        public void Next()
        {
            if (!IsActive || !CurrentStep.AllowsNext)
                return;
            if (CurrentStep.IsFinal)
                Quit();
            else
                Advance();
        }

        public void Skip()
        {
            if (!IsActive || _index >= TutorialContent.StepCount - 1)
                return;
            Advance();
        }

        public void Quit()
        {
            if (!IsActive || HasExited)
                return;

            ClearMarkers();
            _doneTimer = -1f;
            IsActive = false;
            HasExited = true;
            IntentRequested?.Invoke(TutorialIntent.QuitToMenu());
            Exited?.Invoke();
        }

        public void RequestCameraFocus(Vector2 simPosition)
        {
            EmitIntent(TutorialIntent.FocusCamera(simPosition));
        }

        public void FocusCompany()
        {
            if (!string.IsNullOrEmpty(_state.CompanyUnitId))
                RequestCameraFocus(_state.CompanyPosition);
        }

        public void RequestFireMode(TutorialFireMode fireMode, bool smoke = false)
        {
            EmitIntent(TutorialIntent.SetFireMode(fireMode, smoke));
        }

        public void ObservePlayerOrder(TutorialPlayerOrderEvent orderEvent)
        {
            if (IsActive)
                _orders.Add(orderEvent);
        }

        public void ObserveSimulationEvent(TutorialSimulationEvent simulationEvent)
        {
            if (!IsActive)
                return;

            switch (simulationEvent.Type)
            {
                case TutorialSimulationEventType.FireRequested:
                    if (simulationEvent.Side == _state.PlayerSide)
                        _fireRequested = true;
                    break;
                case TutorialSimulationEventType.OrderReceived:
                    if (_listenPlatoonOrders
                        && !string.IsNullOrEmpty(_state.CompanyUnitId)
                        && !string.IsNullOrEmpty(simulationEvent.UnitId)
                        && simulationEvent.ParentUnitId == _state.CompanyUnitId)
                    {
                        _platoonGotOrder = true;
                    }
                    break;
                case TutorialSimulationEventType.BattleEnded:
                    _state.HasBattleResult = true;
                    break;
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            Detach();
            ClearMarkers();
            IsActive = false;
            HasExited = true;
            IsDisposed = true;
        }

        private void Advance()
        {
            _doneTimer = -1f;
            IsCurrentStepSatisfied = false;
            _index++;
            if (_index >= TutorialContent.StepCount)
            {
                Quit();
                return;
            }

            switch (CurrentStepId)
            {
                case TutorialStepId.Camera:
                    _cameraStart = _state.CameraFocus;
                    _cameraDistanceStart = _state.CameraDistance;
                    break;
                case TutorialStepId.MovementOrder:
                    AddMarker(TutorialWaypoint, MoveMarkerColor, "이동 목표");
                    break;
                case TutorialStepId.CommandDelay:
                    _listenPlatoonOrders = true;
                    break;
                case TutorialStepId.AttackMove:
                    ClearMarkers();
                    if (!string.IsNullOrEmpty(_state.EnemyUnitId) && !_state.EnemyOnMap)
                    {
                        EmitIntent(TutorialIntent.RevealEnemyAt(
                            _state.EnemyUnitId,
                            TutorialEnemy,
                            Mathf.PI));
                    }
                    AddMarker(TutorialEnemy, EnemyMarkerColor, "적 BMP-3 소대");
                    break;
                case TutorialStepId.Completion:
                    ClearMarkers();
                    break;
            }

            StepEntered?.Invoke(CurrentStep);
        }

        private bool EvaluateCurrentStep()
        {
            switch (CurrentStepId)
            {
                case TutorialStepId.Camera:
                    return CameraComplete();
                case TutorialStepId.CompanySelection:
                    return SelectedOnlyCompany();
                case TutorialStepId.Subselection:
                    return SubselectionComplete();
                case TutorialStepId.ControlGroups:
                    return ControlGroupComplete();
                case TutorialStepId.Formation:
                    return !string.IsNullOrEmpty(_state.CompanyUnitId)
                        && _state.CompanyFormationId == "co_line";
                case TutorialStepId.TimeControls:
                    return TimeControlsComplete();
                case TutorialStepId.MovementOrder:
                    return OrderedNear(
                        true,
                        TutorialWaypoint,
                        MoveOrderRadius,
                        TutorialOrderType.Move,
                        TutorialOrderType.FastMove,
                        TutorialOrderType.AttackMove);
                case TutorialStepId.CommandDelay:
                    return _platoonGotOrder && CompanyMoving();
                case TutorialStepId.RulesOfEngagement:
                    return RoeComplete();
                case TutorialStepId.AttackMove:
                    return OrderedNear(
                        false,
                        TutorialEnemy,
                        AttackOrderRadius,
                        TutorialOrderType.AttackMove,
                        TutorialOrderType.Seize);
                case TutorialStepId.FireSupport:
                    return _fireRequested;
                case TutorialStepId.EnemyDestruction:
                    return EnemyDestroyed();
                default:
                    return false;
            }
        }

        private bool CameraComplete()
        {
            if (!IsFinite(_state.CameraFocus)
                || !IsFinite(_state.CameraDistance)
                || !IsFinite(_cameraDistanceStart)
                || _cameraDistanceStart <= 0f)
            {
                return false;
            }

            float pan = Vector2.Distance(_state.CameraFocus, _cameraStart);
            float zoom = Mathf.Abs(_state.CameraDistance - _cameraDistanceStart) / _cameraDistanceStart;
            return IsFinite(pan)
                && IsFinite(zoom)
                && pan > CameraPanThreshold
                && zoom > CameraZoomThreshold;
        }

        private bool SelectedOnlyCompany()
        {
            return !string.IsNullOrEmpty(_state.CompanyUnitId)
                && _state.Selection.Count == 1
                && _state.Selection[0].UnitId == _state.CompanyUnitId;
        }

        private bool SubselectionComplete()
        {
            if (!string.IsNullOrEmpty(_state.CompanyUnitId)
                && _state.Selection.Count > 1
                && _state.Selection[0].ParentUnitId == _state.CompanyUnitId)
            {
                _sawPlatoons = true;
            }
            return _sawPlatoons && SelectedOnlyCompany();
        }

        private bool ControlGroupComplete()
        {
            if (!string.IsNullOrEmpty(_state.CompanyUnitId)
                && Contains(_state.ControlGroup1, _state.CompanyUnitId)
                && _state.Selection.Count == 0)
            {
                _sawEmptyAfterGroup = true;
            }
            return _sawEmptyAfterGroup && ContainsSelected(_state.CompanyUnitId);
        }

        private bool TimeControlsComplete()
        {
            if (_state.IsPaused)
                _sawPause = true;
            if (_state.TimeSpeed > 1.01f)
                _sawFast = true;
            return _sawPause && _sawFast && !_state.IsPaused;
        }

        private bool RoeComplete()
        {
            if (string.IsNullOrEmpty(_state.CompanyUnitId))
                return false;
            if (_state.CompanyRoe == "hold_fire")
                _sawHold = true;
            return _sawHold && _state.CompanyRoe == "fire_at_will";
        }

        private bool CompanyMoving()
        {
            for (int i = 0; i < _state.CompanyElements.Count; i++)
            {
                TutorialElementState element = _state.CompanyElements[i];
                if (element.IsAlive && element.Velocity.magnitude > MovingSpeedThreshold)
                    return true;
            }
            return false;
        }

        private bool EnemyDestroyed()
        {
            if (string.IsNullOrEmpty(_state.EnemyUnitId)
                || _state.EnemyIsDestroyed
                || _state.HasBattleResult)
            {
                return true;
            }

            for (int i = 0; i < _state.EnemyElements.Count; i++)
            {
                if (_state.EnemyElements[i].IsAlive)
                    return false;
            }
            return true;
        }

        private bool OrderedNear(
            bool companyOnly,
            Vector2 target,
            float radius,
            params TutorialOrderType[] acceptedTypes)
        {
            float radiusSquared = radius * radius;
            for (int i = 0; i < _orders.Count; i++)
            {
                TutorialPlayerOrderEvent orderEvent = _orders[i];
                if (!orderEvent.HasPosition || !IsFinite(orderEvent.Position))
                    continue;
                if (!ContainsType(acceptedTypes, orderEvent.Type))
                    continue;
                if ((orderEvent.Position - target).sqrMagnitude > radiusSquared)
                    continue;
                if (!companyOnly)
                    return true;
                if (string.IsNullOrEmpty(orderEvent.TargetUnitId))
                    continue;
                if (orderEvent.TargetUnitId == _state.CompanyUnitId
                    || orderEvent.TargetParentUnitId == _state.CompanyUnitId)
                {
                    return true;
                }
            }
            return false;
        }

        private bool ContainsSelected(string unitId)
        {
            for (int i = 0; i < _state.Selection.Count; i++)
            {
                if (_state.Selection[i].UnitId == unitId)
                    return true;
            }
            return false;
        }

        private void AddMarker(Vector2 position, Color color, string text)
        {
            _markers.Add(new TutorialWorldMarker(position, color, text));
            MarkersChanged?.Invoke();
        }

        private void ClearMarkers()
        {
            if (_markers.Count == 0)
                return;
            _markers.Clear();
            MarkersChanged?.Invoke();
        }

        private void EmitIntent(TutorialIntent intent)
        {
            if (IsActive)
                IntentRequested?.Invoke(intent);
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(TutorialController));
        }

        private static bool Contains(IList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == expected)
                    return true;
            }
            return false;
        }

        private static bool ContainsType(TutorialOrderType[] types, TutorialOrderType expected)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == expected)
                    return true;
            }
            return false;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
