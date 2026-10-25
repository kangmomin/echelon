using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Echelon.Core;
using Echelon.Unity.Presentation;
using Echelon.Unity.Presentation.Elements;
using Echelon.Unity.Presentation.Fx;
using Echelon.Unity.Presentation.Symbols;
using Echelon.Unity.Presentation.Terrain;
using Echelon.Unity.UI.Aar;
using Echelon.Unity.UI.Battle;
using Echelon.Unity.UI.Common;
using Echelon.Unity.UI.Guide;
using Echelon.Unity.UI.Minimap;
using Echelon.Unity.UI.Tutorial;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using CoreBattle = Echelon.Core.Battle;

namespace Echelon.Unity.App
{
    /// <summary>
    /// 전투 한 판의 조립 지점. Core.Battle 을 프레임마다 진행하고 지형·장비·부호·HUD 를 갱신한다.
    /// 지휘 상태와 명령은 Commands(BattleCommands), 입력 해석은 Input(BattleInputController)이 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleController : MonoBehaviour, ITutorialEventSource, IDisposable
    {
        private readonly List<BattleUnitView> _selectionViews = new List<BattleUnitView>();
        private readonly List<FxSmoke> _smokeSnapshots = new List<FxSmoke>();

        private bool _composed;
        private bool _disposed;
        private bool _fogEnabled = true;
        private bool _finishedDelivered;
        private long _advancePeakMicroseconds;
        private BattleEnvironment _environment;
        private GuideScreen _guide;
        private bool _pausedBeforeGuide;
        private bool _cameraInputBeforeGuide;
        private TutorialPanel _tutorialPanel;
        private TutorialWorldMarkerLayer _tutorialMarkers;

        public CoreBattle Battle { get; private set; }
        public BattleCommands Commands { get; private set; }
        public BattleInputController Input { get; private set; }
        public RtsCameraController RtsCamera { get; private set; }
        public TerrainView Terrain { get; private set; }
        public TerrainMeshData TerrainData { get; private set; }
        public ElementViewManager ElementViews { get; private set; }
        public FxLayer Fx { get; private set; }
        public BattlefieldSymbolLayer Symbols { get; private set; }
        public MinimapView Minimap { get; private set; }
        public BattleHud Hud { get; private set; }
        public OrbatPanel Orbat { get; private set; }
        public NotificationFeed Notifications { get; private set; }
        public DeploymentPanel DeploymentPanel { get; private set; }
        public DeploymentZoneView DeploymentZones { get; private set; }
        public BattleDebugOverlay DebugOverlay { get; private set; }
        public Canvas BattleCanvas { get; private set; }
        public RectTransform UiRoot { get; private set; }
        public RectTransform OverlayRoot { get; private set; }
        public TutorialController Tutorial { get; private set; }
        public TutorialState TutorialState { get; private set; }
        public AarData CurrentAar { get; private set; }
        public BattlefieldSymbolSnapshot SymbolSnapshot { get; private set; }
        public MinimapSnapshot MinimapSnapshot { get; private set; }
        public IReadOnlyList<ElementVisualState> CurrentElementStates { get; private set; }
        public IReadOnlyList<Unit> Selection => Commands != null ? Commands.Selection : Array.Empty<Unit>();
        public bool IsComposed => _composed;
        public bool IsDisposed => _disposed;
        public bool HasResult => Battle != null && Battle.HasResult;
        public bool FogEnabled => _fogEnabled;
        public bool IsGuideOpen => _guide != null && _guide.IsOpen;
        public int LastTicksAdvanced { get; private set; }
        public int LastDrainedEventCount { get; private set; }
        public long LastAdvanceMicroseconds { get; private set; }
        public long LastPresentationTick { get; private set; } = -1;
        public int PresentationRevision { get; private set; }

        public event Action<SimEvent> SimulationEvent;
        public event Action<TutorialPlayerOrderEvent> PlayerOrderIssued;
        public event Action<TutorialSimulationEvent> SimulationEventEmitted;
        public event Action<AarData> Finished;
        public event Action MenuRequested;
        public event Action TutorialExited;

        public void Setup(CoreBattle battle)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BattleController));
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            if (_composed)
            {
                if (ReferenceEquals(Battle, battle))
                    return;
                throw new InvalidOperationException("A BattleController instance owns one battle; create a new controller for another battle.");
            }
            Battle = battle;
            Commands = new BattleCommands(battle);
            Compose();
        }

        public void StartBattle()
        {
            RequireBattle();
            if (!Battle.Deploying)
                return;
            Battle.BeginBattle();
            if (IsGuideOpen)
                Battle.World.SetPaused(true);
            DeploymentPanel.SetDeploymentState(false);
            Notifications.Push("전투 시작", UiTheme.Accent);
            RefreshPresentation(0f, 0f);
        }

        public TutorialController StartTutorial()
        {
            RequireBattle();
            if (Tutorial != null)
                return Tutorial;
            TutorialState = BattleViewAdapter.CreateTutorialState(Battle);
            Tutorial = new TutorialController(TutorialState);
            Tutorial.Attach(this);
            Tutorial.IntentRequested += OnTutorialIntent;
            Tutorial.Exited += OnTutorialExited;
            _tutorialPanel = TutorialPanel.Create(UiRoot, Tutorial);
            _tutorialMarkers = TutorialWorldMarkerLayer.Create(
                transform,
                UiRoot,
                RtsCamera.TargetCamera,
                Tutorial,
                position => BattleViewAdapter.GroundHeight(Battle, position));
            _tutorialPanel.transform.SetAsLastSibling();
            return Tutorial;
        }

        public void StopTutorial()
        {
            if (Tutorial != null)
            {
                Tutorial.IntentRequested -= OnTutorialIntent;
                Tutorial.Exited -= OnTutorialExited;
                Tutorial.Dispose();
                Tutorial = null;
            }
            TutorialState = null;
            DestroyUnityObject(_tutorialMarkers != null ? _tutorialMarkers.gameObject : null);
            _tutorialMarkers = null;
            DestroyUnityObject(_tutorialPanel != null ? _tutorialPanel.gameObject : null);
            _tutorialPanel = null;
        }

        /// <summary>전장의 안개. 끄면 적도 모두 보인다(디버그).</summary>
        public void SetFog(bool on)
        {
            ApplyFog(on);
            Input?.SetFogEnabled(on);
        }

        public void AdvanceFrame(float deltaSeconds)
        {
            AdvanceFrame(deltaSeconds, deltaSeconds);
        }

        public void AdvanceFrame(float deltaSeconds, float realDeltaSeconds)
        {
            if (!_composed || _disposed)
                return;
            float delta = Mathf.Max(0f, deltaSeconds);
            float realDelta = Mathf.Max(0f, realDeltaSeconds);
            long started = Stopwatch.GetTimestamp();
            LastTicksAdvanced = Battle.World.Advance(delta);
            LastAdvanceMicroseconds = ElapsedMicroseconds(started);
            if (LastTicksAdvanced > 0)
                _advancePeakMicroseconds = Math.Max(_advancePeakMicroseconds, LastAdvanceMicroseconds / LastTicksAdvanced);

            List<SimEvent> events = Battle.World.Events.Drain();
            LastDrainedEventCount = events.Count;
            for (int i = 0; i < events.Count; i++)
                ProcessSimulationEvent(events[i]);
            Commands.PruneSelection();

            float gameDelta = Battle.World.Paused ? 0f : delta * (float)Battle.World.GetSpeed();
            RefreshPresentation(realDelta, gameDelta);
        }

        /// <summary>전투 중 가이드. 열려 있는 동안 일시정지하고 침로·명령 입력을 막는다.</summary>
        public GuideScreen OpenGuide()
        {
            RequireBattle();
            if (_guide != null)
                return _guide;
            _guide = GuideScreen.Create(OverlayRoot);
            _guide.Closed += CloseGuide;
            _pausedBeforeGuide = Battle.World.Paused;
            _cameraInputBeforeGuide = RtsCamera.InputEnabled;
            RtsCamera.InputEnabled = false;
            Input.SetGuideOpen(true);
            Input.enabled = false;
            Battle.World.SetPaused(true);
            _guide.Open();
            return _guide;
        }

        public void CloseGuide()
        {
            if (_guide == null)
                return;
            GuideScreen guide = _guide;
            _guide = null;
            guide.Closed -= CloseGuide;
            if (guide.IsOpen)
                guide.Close();
            DestroyUnityObject(guide.gameObject);
            if (RtsCamera != null)
                RtsCamera.InputEnabled = _cameraInputBeforeGuide;
            if (Input != null)
            {
                Input.SetGuideOpen(false);
                Input.enabled = !_disposed && Battle != null && !Battle.HasResult;
            }
            if (Battle != null)
                Battle.World.SetPaused(Battle.HasResult || _pausedBeforeGuide);
        }

        public void ToggleGuide()
        {
            if (IsGuideOpen)
                CloseGuide();
            else
                OpenGuide();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Teardown();
            if (this != null)
                DestroyUnityObject(gameObject);
        }

        private void Update()
        {
            if (_composed && !_disposed)
                AdvanceFrame(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void Compose()
        {
            _environment = BattleEnvironment.Apply(transform);
            TerrainData = BattleViewAdapter.CreateTerrainData(Battle);
            Terrain = new TerrainViewBuilder().Build(TerrainData);
            Terrain.transform.SetParent(transform, false);

            var elementsObject = new GameObject("Elements");
            elementsObject.transform.SetParent(transform, false);
            ElementViews = elementsObject.AddComponent<ElementViewManager>();
            ElementViews.GroundHeight = position => BattleViewAdapter.GroundHeight(Battle, position);

            var fxObject = new GameObject("Fx");
            fxObject.transform.SetParent(transform, false);
            Fx = fxObject.AddComponent<FxLayer>();
            Fx.GroundHeight = position => BattleViewAdapter.GroundHeight(Battle, new Vector2(position.x, position.z));

            BuildCamera();
            BuildUi();
            BuildInput();
            WireCommands();
            _composed = true;
            ApplyFog(_fogEnabled);
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("RtsCamera");
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = BattleEnvironment.BackgroundColor;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cameraObject.tag = "MainCamera";
            RtsCamera = cameraObject.AddComponent<RtsCameraController>();
            RtsCamera.TargetCamera = camera;
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            Vector2 focus = taskForce != null
                ? BattleViewAdapter.ToVector(taskForce.Centroid() + new SimVec2(0.0, 400.0))
                : BattleViewAdapter.ToVector(Battle.Terrain.SizeM * 0.5);
            RtsCamera.Setup(
                new MapBounds(Vector2.zero, BattleViewAdapter.ToVector(Battle.Terrain.SizeM)),
                position => BattleViewAdapter.GroundHeight(Battle, position),
                focus);
        }

        private void BuildUi()
        {
            BattleCanvas = UiKit.CreateCanvas("BattleUI");
            BattleCanvas.transform.SetParent(transform, false);
            UiRoot = (RectTransform)BattleCanvas.transform;

            DeploymentZones = DeploymentZoneView.Create(UiRoot);
            DeploymentZones.gameObject.SetActive(false);

            var symbolsObject = new GameObject(
                "BattlefieldSymbols",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(BattlefieldSymbolLayer));
            symbolsObject.transform.SetParent(UiRoot, false);
            Symbols = symbolsObject.GetComponent<BattlefieldSymbolLayer>();
            UiKit.Stretch((RectTransform)symbolsObject.transform);
            Symbols.SetView(
                RtsCamera.TargetCamera,
                RtsCamera.HeightAboveGround,
                position => BattleViewAdapter.GroundHeight(Battle, position));

            Hud = BattleHud.Create(UiRoot);
            Orbat = OrbatPanel.Create(UiRoot);
            Orbat.Bind(() => BattleViewAdapter.CreateOrbat(Battle));
            Notifications = NotificationFeed.Create(UiRoot);

            RectTransform minimapFrame = UiKit.CreateCard(UiRoot, "MinimapFrame");
            BattleHudLayout.PlaceMinimap(minimapFrame);
            var minimapObject = new GameObject("Minimap", typeof(RectTransform), typeof(MinimapView));
            minimapObject.transform.SetParent(minimapFrame, false);
            Minimap = minimapObject.GetComponent<MinimapView>();
            RectTransform minimapRect = (RectTransform)minimapObject.transform;
            UiKit.Stretch(minimapRect);
            minimapRect.offsetMin = new Vector2(4f, 4f);
            minimapRect.offsetMax = new Vector2(-4f, -4f);
            Minimap.SetTerrain(
                BattleViewAdapter.ToVector(Battle.Terrain.SizeM),
                position => BattleViewAdapter.SampleMinimapTerrain(Battle, position));

            DeploymentPanel = Echelon.Unity.UI.Battle.DeploymentPanel.Create(UiRoot);

            DebugOverlay = BattleDebugOverlay.Create(UiRoot);
            DebugOverlay.enabled = false;
            OverlayRoot = UiKit.CreateRect(UiRoot, "OverlayRoot");
            UiKit.Stretch(OverlayRoot);
            OverlayRoot.SetAsLastSibling();
        }

        private void BuildInput()
        {
            Input = gameObject.AddComponent<BattleInputController>();
            Input.Configure(
                Commands,
                RtsCamera.TargetCamera,
                RtsCamera.Bounds,
                position => BattleViewAdapter.GroundHeight(Battle, position));
            Input.BindHud(Hud);
            Input.BindOrbat(Orbat);
            Input.BindSymbols(Symbols);
            Input.BindDeploymentPanel(DeploymentPanel);
            Input.BindMinimap(Minimap);
            Input.BindCamera(RtsCamera);
            Input.GuideRequested += OnInputGuide;
            Input.FogVisibilityChanged += OnInputFog;
            Input.DebugOverlayVisibilityChanged += OnInputDebugVisibility;
            Input.BattleStartRequested += StartBattle;
        }

        private void WireCommands()
        {
            Commands.SelectionChanged += OnSelectionChanged;
            Commands.OrderIssued += OnOrderIssued;
            Commands.OrderModeChanged += Hud.SetOrderMode;
            Commands.FireModeChanged += Hud.SetFireMode;
            Commands.Notification += OnCommandNotification;
            Commands.FocusRequested += RtsCamera.FocusOnSim;
            Hud.SetOrderMode(Commands.OrderMode);
            Hud.SetFireMode(Commands.FireAsset, Commands.FireSmoke);
        }

        private void ApplyFog(bool on)
        {
            _fogEnabled = on;
            if (ElementViews != null)
                ElementViews.FogSide = on && Battle != null ? Battle.PlayerSide : string.Empty;
            if (_composed)
                RefreshPresentation(0f, 0f);
        }

        private void RefreshPresentation(float realDeltaSeconds, float gameDeltaSeconds)
        {
            if (Battle == null || !_composed)
                return;
            float alpha = (float)Battle.World.GetInterpolationAlpha();
            int tick = TickToInt(Battle.World.Tick);
            CurrentElementStates = BattleViewAdapter.CreateElementStates(Battle);
            ElementViews.Sync(CurrentElementStates);
            ElementViews.UpdateVisuals(realDeltaSeconds, gameDeltaSeconds, alpha, tick);

            _smokeSnapshots.Clear();
            foreach (Los.Smoke smoke in Battle.Smokes)
            {
                if (smoke.UntilTick <= Battle.World.Tick)
                    continue;
                _smokeSnapshots.Add(new FxSmoke
                {
                    Id = RuntimeHelpers.GetHashCode(smoke),
                    Position = ViewCoords.ToWorld(BattleViewAdapter.ToVector(smoke.Pos)),
                    Radius = (float)smoke.Radius,
                    UntilTick = TickToInt(smoke.UntilTick)
                });
            }
            Fx.SetSmokeCircles(_smokeSnapshots);
            Fx.Advance(gameDeltaSeconds, tick);

            SymbolSnapshot = BattleViewAdapter.CreateSymbolSnapshot(Battle, Commands.Selection, _fogEnabled, alpha, false);
            Symbols.SetView(
                RtsCamera.TargetCamera,
                RtsCamera.HeightAboveGround,
                position => BattleViewAdapter.GroundHeight(Battle, position));
            Symbols.SetState(SymbolSnapshot);
            MinimapSnapshot = BattleViewAdapter.CreateMinimapSnapshot(Battle, _fogEnabled, RtsCamera);
            Minimap.SetState(MinimapSnapshot);

            DeploymentPanel.SetDeploymentState(Battle.Deploying);
            DeploymentZones.gameObject.SetActive(Battle.Deploying);
            if (Battle.Deploying)
                DeploymentZones.Render(BattleViewAdapter.CreateDeploymentZone(Battle), ProjectSimulationToScreen);
            else
                DeploymentZones.Clear();

            Hud.Bind(
                BattleViewAdapter.CreateHudState(Battle),
                SelectionViews(),
                BattleViewAdapter.CreateFireSupportState(Battle));
            Orbat.ShowSelection(Commands.SelectedUnitIds);
            if (!IsGuideOpen)
                Input.enabled = !Battle.HasResult;
            if (Tutorial != null)
            {
                BattleViewAdapter.UpdateTutorialState(
                    Battle, TutorialState, Commands.Selection, UnitIds(Commands.GetGroupUnits(1)), RtsCamera);
            }
            if (DebugOverlay.IsVisible)
                DebugOverlay.SetDiagnostics(CreateDiagnostics(realDeltaSeconds));
            LastPresentationTick = Battle.World.Tick;
            PresentationRevision++;
        }

        private void ProcessSimulationEvent(SimEvent simEvent)
        {
            FxEvent fxEvent = BattleViewAdapter.CreateFxEvent(Battle, simEvent, _fogEnabled);
            if (fxEvent != null)
                Fx.Enqueue(fxEvent);
            SimulationEvent?.Invoke(simEvent);
            SimulationEventEmitted?.Invoke(ToTutorialEvent(simEvent));
            if (simEvent.Type == "battle_ended" && !_finishedDelivered
                && (Tutorial == null || !Tutorial.SuppressNormalAar))
            {
                _finishedDelivered = true;
                CurrentAar = BattleViewAdapter.CreateAar(Battle.Stats.Summary());
                Finished?.Invoke(CurrentAar);
            }
            BattleNotification? notification = BattleEventText.Describe(
                BattleViewAdapter.CreateEventData(Battle, simEvent),
                Battle.PlayerSide);
            if (notification.HasValue)
                Notifications.Push(notification.Value);
        }

        private TutorialSimulationEvent ToTutorialEvent(SimEvent simEvent)
        {
            Unit unit = Battle.GetUnit(GetInt(simEvent.Payload, "unit", -1));
            string unitId = unit != null ? Id(unit.Id) : string.Empty;
            string parentId = unit != null && unit.Parent != null ? Id(unit.Parent.Id) : string.Empty;
            string side = simEvent.Payload.TryGetValue("side", out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : unit != null ? unit.Side : string.Empty;
            switch (simEvent.Type)
            {
                case "fire_requested":
                    return TutorialSimulationEvent.FireRequested(side);
                case "order_received":
                    return TutorialSimulationEvent.OrderReceived(unitId, parentId);
                case "battle_ended":
                    return TutorialSimulationEvent.BattleEnded();
                default:
                    return new TutorialSimulationEvent(TutorialSimulationEventType.Other, side, unitId, parentId);
            }
        }

        private static TutorialPlayerOrderEvent ToTutorialOrder(Unit unit, Order order)
        {
            TutorialOrderType type;
            switch (order.Type)
            {
                case OrderType.MOVE: type = TutorialOrderType.Move; break;
                case OrderType.FAST_MOVE: type = TutorialOrderType.FastMove; break;
                case OrderType.ATTACK_MOVE: type = TutorialOrderType.AttackMove; break;
                case OrderType.SEIZE: type = TutorialOrderType.Seize; break;
                default: type = TutorialOrderType.Other; break;
            }
            string unitId = Id(unit.Id);
            string parentId = unit.Parent != null ? Id(unit.Parent.Id) : string.Empty;
            return order.HasPos()
                ? TutorialPlayerOrderEvent.At(type, unitId, parentId, BattleViewAdapter.ToVector(order.GetPos()))
                : TutorialPlayerOrderEvent.WithoutPosition(type, unitId, parentId);
        }

        private IReadOnlyList<BattleUnitView> SelectionViews()
        {
            _selectionViews.Clear();
            foreach (Unit unit in Commands.Selection)
                _selectionViews.Add(BattleViewAdapter.CreateUnitView(Battle, unit));
            return _selectionViews;
        }

        private static List<int> UnitIds(IEnumerable<Unit> units)
        {
            var ids = new List<int>();
            foreach (Unit unit in units)
                ids.Add(unit.Id);
            return ids;
        }

        private Vector2 ProjectSimulationToScreen(Vector2 position)
        {
            float ground = BattleViewAdapter.GroundHeight(Battle, position) + 2f;
            Vector3 screen = RtsCamera.TargetCamera.WorldToScreenPoint(ViewCoords.ToWorld(position, ground));
            return new Vector2(screen.x, screen.y);
        }

        private BattleDiagnostics CreateDiagnostics(float realDeltaSeconds)
        {
            return new BattleDiagnostics
            {
                FramesPerSecond = realDeltaSeconds > 0.000001f ? Mathf.RoundToInt(1f / realDeltaSeconds) : 0,
                Tick = Battle.World.Tick,
                TicksThisFrame = LastTicksAdvanced,
                AdvanceMicroseconds = LastAdvanceMicroseconds,
                PeakTickMicroseconds = _advancePeakMicroseconds,
                ElementCount = Battle.Elements.Count,
                UnitCount = Battle.Units.Count,
                PendingPathRequests = Battle.Pathfinder.PendingCount(),
                ProcessedPathRequests = Battle.Pathfinder.RequestsProcessed,
                CameraHeight = RtsCamera.HeightAboveGround,
                SymbolLevel = (int)Symbols.CurrentLevel
            };
        }

        private void OnSelectionChanged(IReadOnlyList<Unit> units)
        {
            Hud.SetSelection(SelectionViews());
            Orbat.ShowSelection(Commands.SelectedUnitIds);
        }

        private void OnOrderIssued(Order order)
        {
            Unit unit = Battle.GetUnit(order.TargetUnitId);
            if (unit != null)
                PlayerOrderIssued?.Invoke(ToTutorialOrder(unit, order));
        }

        private void OnCommandNotification(string text)
        {
            Notifications.Push(text, UiTheme.TextDim);
        }

        private void OnInputGuide()
        {
            if (Input.GuideOpen)
                OpenGuide();
            else
                CloseGuide();
        }

        private void OnInputFog(bool on)
        {
            if (on == _fogEnabled)
                return;
            ApplyFog(on);
            Notifications.Push("전장의 안개 " + (on ? "켬" : "끔"), UiTheme.TextDim);
        }

        private void OnInputDebugVisibility(bool visible)
        {
            DebugOverlay.SetVisible(visible);
        }

        private void OnTutorialIntent(TutorialIntent intent)
        {
            switch (intent.Kind)
            {
                case TutorialIntentKind.CameraFocus:
                    RtsCamera.FocusOnSim(intent.SimPosition);
                    break;
                case TutorialIntentKind.RevealEnemy:
                    if (int.TryParse(intent.UnitId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                        && Battle.GetUnit(id) is Platoon platoon)
                    {
                        BattleSetup.RevealPlatoon(
                            Battle, platoon, BattleViewAdapter.ToSim(intent.SimPosition), intent.HeadingRadians);
                        RefreshPresentation(0f, 0f);
                    }
                    break;
                case TutorialIntentKind.FireMode:
                    switch (intent.FireMode)
                    {
                        case TutorialFireMode.None:
                            Commands.ClearFireMode();
                            break;
                        case TutorialFireMode.Mortar:
                            Commands.SetFireMode(BattleFireAsset.Mortar, intent.Smoke);
                            break;
                        case TutorialFireMode.Artillery:
                            Commands.SetFireMode(BattleFireAsset.Artillery, intent.Smoke);
                            break;
                        case TutorialFireMode.CloseAirSupport:
                            Commands.SetFireMode(BattleFireAsset.Cas);
                            break;
                    }
                    break;
                case TutorialIntentKind.QuitToMenu:
                    MenuRequested?.Invoke();
                    break;
            }
        }

        private void OnTutorialExited()
        {
            TutorialExited?.Invoke();
        }

        private void Teardown()
        {
            if (_disposed)
                return;
            _disposed = true;
            _composed = false;
            UnwireUi();
            CloseGuide();
            StopTutorial();
            _environment?.Restore();
            _environment = null;
            if (Battle != null)
            {
                Battle.Dispose();
                Battle = null;
            }
            CurrentElementStates = null;
            SymbolSnapshot = null;
            MinimapSnapshot = null;
        }

        private void UnwireUi()
        {
            if (Commands != null)
            {
                Commands.SelectionChanged -= OnSelectionChanged;
                Commands.OrderIssued -= OnOrderIssued;
                Commands.Notification -= OnCommandNotification;
                if (Hud != null)
                {
                    Commands.OrderModeChanged -= Hud.SetOrderMode;
                    Commands.FireModeChanged -= Hud.SetFireMode;
                }
                if (RtsCamera != null)
                    Commands.FocusRequested -= RtsCamera.FocusOnSim;
            }
            if (Input != null)
            {
                Input.GuideRequested -= OnInputGuide;
                Input.FogVisibilityChanged -= OnInputFog;
                Input.DebugOverlayVisibilityChanged -= OnInputDebugVisibility;
                Input.BattleStartRequested -= StartBattle;
                Input.BindHud(null);
                Input.BindOrbat(null);
                Input.BindSymbols(null);
                Input.BindDeploymentPanel(null);
                Input.BindMinimap(null);
                Input.BindCamera(null);
                Input.enabled = false;
            }
        }

        private static int GetInt(IReadOnlyDictionary<string, object> payload, string key, int fallback)
        {
            return payload.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : fallback;
        }

        private static string Id(int id)
        {
            return id.ToString(CultureInfo.InvariantCulture);
        }

        private static int TickToInt(long tick)
        {
            return tick > int.MaxValue ? int.MaxValue : tick < int.MinValue ? int.MinValue : (int)tick;
        }

        private static long ElapsedMicroseconds(long started)
        {
            long elapsed = Stopwatch.GetTimestamp() - started;
            return (long)(elapsed * (1000000.0 / Stopwatch.Frequency));
        }

        private void RequireBattle()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BattleController));
            if (Battle == null)
                throw new InvalidOperationException("BattleController.Setup must be called first.");
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
