using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Echelon.Core;
using Echelon.Unity.Platform;
using Echelon.Unity.Presentation;
using Echelon.Unity.Presentation.Elements;
using Echelon.Unity.Presentation.Fx;
using Echelon.Unity.Presentation.Symbols;
using Echelon.Unity.Presentation.Terrain;
using Echelon.Unity.UI.Aar;
using Echelon.Unity.UI.Battle;
using Echelon.Unity.UI.Builder;
using Echelon.Unity.UI.Common;
using Echelon.Unity.UI.Guide;
using Echelon.Unity.UI.Minimap;
using Echelon.Unity.UI.Tutorial;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CoreBattle = Echelon.Core.Battle;

namespace Echelon.Unity.App
{
    [DisallowMultipleComponent]
    public sealed class BattleController : MonoBehaviour, ITutorialEventSource, IDisposable
    {
        private static readonly string[] ObserverNames =
        {
            "관측반 관측", "일반 엘리먼트 관측", "관측 없음, 지도 좌표 사격"
        };

        private readonly List<Unit> _selection = new List<Unit>();
        private readonly List<BattleUnitView> _selectionViews = new List<BattleUnitView>();
        private readonly List<int> _selectionIds = new List<int>();
        private readonly Dictionary<int, List<int>> _controlGroups = new Dictionary<int, List<int>>();
        private readonly List<FxSmoke> _smokeSnapshots = new List<FxSmoke>();

        private bool _composed;
        private bool _disposed;
        private bool _syncingSelection;
        private bool _syncingFog;
        private bool _fogEnabled = true;
        private bool _finishedDelivered;
        private long _advancePeakMicroseconds;
        private Component _overlay;
        private GuideScreen _guide;
        private AarScreen _aar;
        private ForceBuilder _builder;
        private bool _overlayPauseActive;
        private bool _pausedBeforeOverlay;
        private bool _cameraInputBeforeOverlay;
        private TutorialPanel _tutorialPanel;
        private TutorialWorldMarkerLayer _tutorialMarkers;

        private bool _environmentCaptured;
        private Material _skyboxMaterial;
        private Material _previousSkybox;
        private AmbientMode _previousAmbientMode;
        private Color _previousAmbientLight;
        private Color _previousAmbientSky;
        private Color _previousAmbientEquator;
        private Color _previousAmbientGround;
        private float _previousAmbientIntensity;
        private bool _previousFog;
        private Color _previousFogColor;
        private FogMode _previousFogMode;
        private float _previousFogDensity;

        public CoreBattle Battle { get; private set; }
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
        public IReadOnlyList<Unit> Selection => _selection;
        public bool IsComposed => _composed;
        public bool IsDisposed => _disposed;
        public bool HasResult => Battle != null && Battle.HasResult;
        public bool FogEnabled => _fogEnabled;
        public bool IsOverlayOpen => _overlay != null;
        public bool IsGuideOpen => _guide != null && _guide.IsOpen;
        public int LastTicksAdvanced { get; private set; }
        public int LastDrainedEventCount { get; private set; }
        public long LastAdvanceMicroseconds { get; private set; }
        public long LastPresentationTick { get; private set; } = -1;
        public int PresentationRevision { get; private set; }

        public event Action<SimEvent> SimulationEvent;
        public event Action<TutorialPlayerOrderEvent> PlayerOrderIssued;
        public event Action<TutorialSimulationEvent> SimulationEventEmitted;
        public event Action<Order> OrderIssued;
        public event Action<AarData> Finished;
        public event Action<AarData> ShowAarRequested;
        public event Action MenuRequested;
        public event Action TutorialExited;
        public event Action<ForceSpec> BuilderStartRequested;
        public event Action BuilderBackRequested;
        public event Action<IReadOnlyList<Unit>> SelectionChanged;
        public event Action<bool> FogChanged;

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
            Compose();
        }

        public void StartBattle()
        {
            RequireBattle();
            if (!Battle.Deploying)
                return;
            Battle.BeginBattle();
            if (_overlayPauseActive)
                Battle.World.SetPaused(true);
            if (DeploymentPanel != null)
                DeploymentPanel.SetDeploymentState(false);
            Notifications?.Push("전투 시작", UiTheme.Accent);
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
            if (_tutorialMarkers != null)
            {
                DestroyUnityObject(_tutorialMarkers.gameObject);
                _tutorialMarkers = null;
            }
            if (_tutorialPanel != null)
            {
                DestroyUnityObject(_tutorialPanel.gameObject);
                _tutorialPanel = null;
            }
        }

        public void SetFog(bool on)
        {
            _fogEnabled = on;
            if (Input != null && !_syncingFog && Input.FogEnabled != on)
            {
                _syncingFog = true;
                try
                {
                    Input.SetFogEnabled(on);
                }
                finally
                {
                    _syncingFog = false;
                }
                return;
            }
            if (ElementViews != null)
                ElementViews.FogSide = on && Battle != null ? Battle.PlayerSide : string.Empty;
            if (IsComposed)
                RefreshPresentation(0f, 0f);
            FogChanged?.Invoke(on);
        }

        public void ToggleFog()
        {
            SetFog(!_fogEnabled);
            if (Input == null)
                Notifications?.Push("전장의 안개 " + (_fogEnabled ? "켬" : "끔"), UiTheme.TextDim);
        }

        public void ToggleDebug()
        {
            if (DebugOverlay == null)
                return;
            DebugOverlay.Toggle();
            Input?.SetDebugOverlayVisible(DebugOverlay.IsVisible);
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
            PruneSelection();

            float gameDelta = Battle.World.Paused ? 0f : delta * (float)Battle.World.GetSpeed();
            RefreshPresentation(realDelta, gameDelta);
        }

        public bool DeployUnit(int unitId, Vector2 target, float headingRadians)
        {
            RequireBattle();
            return DeployUnit(Battle.GetUnit(unitId), target, headingRadians);
        }

        public bool DeployUnit(Unit unit, Vector2 target, float headingRadians)
        {
            RequireBattle();
            return Battle.DeployUnit(unit, BattleViewAdapter.ToSim(target), headingRadians);
        }

        public int DeploySelection(Vector2 target, float? facingRadians = null)
        {
            RequireBattle();
            var units = new List<DeploymentUnitState>(_selection.Count);
            foreach (Unit unit in _selection)
                units.Add(new DeploymentUnitState(unit.Id, (float)unit.Guide.Heading));
            IReadOnlyList<DeploymentPlacementIntent> intents =
                DeploymentPlacement.CreateIntents(units, target, facingRadians);
            return ApplyDeployment(intents);
        }

        public bool SelectUnit(int unitId, bool additive = false)
        {
            RequireBattle();
            Unit unit = Battle.GetUnit(unitId);
            if (unit == null || unit.Side != Battle.PlayerSide || unit.IsDestroyed())
                return false;
            if (!additive)
                _selection.Clear();
            if (!_selection.Contains(unit))
                _selection.Add(unit);
            NotifySelectionChanged();
            return true;
        }

        public void SelectUnits(IEnumerable<Unit> units)
        {
            RequireBattle();
            _selection.Clear();
            if (units != null)
            {
                foreach (Unit unit in units)
                {
                    if (unit != null && unit.Side == Battle.PlayerSide && !unit.IsDestroyed() && !_selection.Contains(unit))
                        _selection.Add(unit);
                }
            }
            NotifySelectionChanged();
        }

        public void SelectUnits(IEnumerable<int> unitIds)
        {
            RequireBattle();
            _selection.Clear();
            if (unitIds != null)
            {
                foreach (int id in unitIds)
                {
                    Unit unit = Battle.GetUnit(id);
                    if (unit != null && unit.Side == Battle.PlayerSide && !unit.IsDestroyed() && !_selection.Contains(unit))
                        _selection.Add(unit);
                }
            }
            NotifySelectionChanged();
        }

        public void ClearSelection()
        {
            if (_selection.Count == 0)
                return;
            _selection.Clear();
            NotifySelectionChanged();
        }

        public Unit PickUnit(Vector2 screenPosition)
        {
            if (Symbols == null || Battle == null)
                return null;
            if (!Symbols.TryPickUnit(screenPosition, Battle.PlayerSide, out SymbolPickResult result))
                return null;
            return int.TryParse(result.UnitId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                ? Battle.GetUnit(id)
                : null;
        }

        public void FocusCamera(Vector2 simulationPosition)
        {
            RtsCamera?.FocusOnSim(simulationPosition);
        }

        public void FocusUnit(int unitId)
        {
            RequireBattle();
            Unit unit = Battle.GetUnit(unitId);
            if (unit != null && unit.Side == Battle.PlayerSide)
                FocusCamera(BattleViewAdapter.ToVector(unit.Centroid()));
        }

        public bool NavigateMinimap(Vector2 screenPosition)
        {
            return Minimap != null && Minimap.Click(screenPosition);
        }

        public Vector2 PickTerrain(Vector2 screenPosition)
        {
            RequireBattle();
            return HeightFieldPicker.Pick(
                RtsCamera.TargetCamera,
                screenPosition,
                RtsCamera.Bounds,
                position => BattleViewAdapter.GroundHeight(Battle, position));
        }

        public void SubSelect(bool down = true)
        {
            if (_selection.Count == 0)
                return;
            var result = new List<Unit>();
            foreach (Unit unit in _selection)
            {
                if (down)
                {
                    foreach (Unit child in unit.ChildrenUnits())
                    {
                        if (!child.IsDestroyed() && !result.Contains(child))
                            result.Add(child);
                    }
                }
                else if (unit.Parent != null && unit.Parent.Level != UnitLevel.TaskForce && !result.Contains(unit.Parent))
                {
                    result.Add(unit.Parent);
                }
            }
            if (result.Count > 0)
                SelectUnits(result);
        }

        public void SelectCompany(int index)
        {
            RequireBattle();
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            if (taskForce == null || index < 0 || index >= taskForce.Companies.Count)
                return;
            Company company = taskForce.Companies[index];
            SelectUnits(new Unit[] { company });
            FocusUnit(company.Id);
        }

        public void AssignControlGroup(int group)
        {
            if (_selection.Count == 0)
                return;
            var ids = new List<int>(_selection.Count);
            foreach (Unit unit in _selection)
                ids.Add(unit.Id);
            _controlGroups[group] = ids;
            if (Input != null)
            {
                Input.AssignGroup(group);
                return;
            }
            Notifications?.Push(
                string.Format(CultureInfo.InvariantCulture, "부대 지정 {0}: {1}개 제대", group, ids.Count),
                UiTheme.TextDim);
        }

        public void RecallControlGroup(int group, bool additive = false)
        {
            RequireBattle();
            if (Input != null)
            {
                Input.RecallGroup(group, additive);
                _controlGroups[group] = UnitIds(Input.GetGroupUnits(group));
                return;
            }
            if (!_controlGroups.TryGetValue(group, out List<int> ids))
                return;
            var units = new List<Unit>();
            var liveIds = new List<int>();
            foreach (int id in ids)
            {
                Unit unit = Battle.GetUnit(id);
                if (unit != null && !unit.IsDestroyed())
                {
                    units.Add(unit);
                    liveIds.Add(id);
                }
            }
            _controlGroups[group] = liveIds;
            if (units.Count == 0)
                return;
            if (additive)
            {
                var combined = new List<Unit>(_selection);
                foreach (Unit unit in units)
                {
                    if (!combined.Contains(unit))
                        combined.Add(unit);
                }
                SelectUnits(combined);
            }
            else
            {
                SelectUnits(units);
            }
        }

        public IReadOnlyList<Unit> ControlGroupUnits(int group)
        {
            if (Input != null)
                return Input.GetGroupUnits(group);
            var units = new List<Unit>();
            if (Battle == null || !_controlGroups.TryGetValue(group, out List<int> ids))
                return units;
            foreach (int id in ids)
            {
                Unit unit = Battle.GetUnit(id);
                if (unit != null && !unit.IsDestroyed())
                    units.Add(unit);
            }
            return units;
        }

        public void SetOrderMode(BattleCommandType type)
        {
            Hud?.SetOrderMode(type);
            Input?.SetOrderMode(type);
        }

        public void IssueMoveOrders(Vector2 target, float? facingRadians = null, bool append = false)
        {
            RequireBattle();
            if (_selection.Count == 0 || Battle.Deploying)
                return;
            int count = _selection.Count;
            for (int i = 0; i < count; i++)
            {
                Unit unit = _selection[i];
                double heading = facingRadians.HasValue && !float.IsNaN(facingRadians.Value)
                    ? facingRadians.Value
                    : FormationSolver.HeadingFromVector(BattleViewAdapter.ToSim(target) - unit.Guide.Pos);
                double offset = (i - (count - 1) * 0.5) * 250.0;
                SimVec2 destination = Battle.Terrain.ClampToMap(
                    BattleViewAdapter.ToSim(target) + FormationSolver.RightVector(heading) * offset);
                var parameters = new JObject();
                if (append)
                    parameters["append"] = true;
                Order order = Order.Make(Hud != null ? (OrderType)(int)Hud.OrderMode : OrderType.MOVE, unit.Id, parameters);
                order.SetPos(destination);
                if (facingRadians.HasValue && !float.IsNaN(facingRadians.Value))
                    order.SetFacing(heading);
                order.Bypass = unit is Platoon && unit.FollowsParent();
                IssuePlayerOrder(unit, order);
            }
            SetOrderMode(BattleCommandType.Move);
        }

        public void IssueImmediateOrder(BattleCommandType type)
        {
            IssueImmediateOrder(new BattleCommandIntent(type));
        }

        public void IssueImmediateOrder(BattleCommandIntent intent)
        {
            RequireBattle();
            if (intent == null || Battle.Deploying)
                return;
            var parameters = new JObject();
            if (intent.Type == BattleCommandType.SetFormation && !string.IsNullOrEmpty(intent.FormationId))
                parameters["formation"] = intent.FormationId;
            if (intent.Type == BattleCommandType.SetSop)
            {
                var sop = new JObject();
                if (!string.IsNullOrEmpty(intent.Technique))
                    sop["technique"] = intent.Technique;
                if (!string.IsNullOrEmpty(intent.RulesOfEngagement))
                    sop["roe"] = intent.RulesOfEngagement;
                if (!string.IsNullOrEmpty(intent.Altitude))
                    sop["altitude"] = intent.Altitude;
                parameters["sop"] = sop;
            }
            foreach (Unit unit in _selection)
                IssuePlayerOrder(unit, Order.Make((OrderType)(int)intent.Type, unit.Id, (JObject)parameters.DeepClone()));
        }

        public void IssueFormation(string formationId)
        {
            if (string.IsNullOrEmpty(formationId))
                return;
            IssueImmediateOrder(BattleCommandIntent.Formation(formationId));
        }

        public void ToggleHoldFire()
        {
            if (_selection.Count == 0)
                return;
            bool hold = _selection[0].Sop.Roe != BattleSop.HoldFire;
            IssueImmediateOrder(BattleCommandIntent.Sop(
                rulesOfEngagement: hold ? BattleSop.HoldFire : BattleSop.FireAtWill));
            Notifications?.Push("사격 " + (hold ? "중지(응사만)" : "자유"), UiTheme.TextDim);
        }

        public void ToggleMount()
        {
            bool anyMounted = false;
            foreach (Unit unit in _selection)
            {
                var platoons = new List<Unit> { unit };
                platoons.AddRange(unit.ChildrenUnits());
                foreach (Unit candidate in platoons)
                {
                    if (candidate is Platoon platoon && platoon.HasSquads() && platoon.Mounted)
                        anyMounted = true;
                }
            }
            IssueImmediateOrder(anyMounted ? BattleCommandType.Dismount : BattleCommandType.Mount);
        }

        public void CycleTechnique()
        {
            if (_selection.Count == 0)
                return;
            string[] techniques = { BattleSop.Traveling, BattleSop.Overwatch, BattleSop.Bounding };
            int index = Array.IndexOf(techniques, _selection[0].Sop.Technique);
            string next = techniques[(index + 1 + techniques.Length) % techniques.Length];
            IssueImmediateOrder(BattleCommandIntent.Sop(technique: next));
            Notifications?.Push("이동 방식: " + BattleUiText.TechniqueName(next), UiTheme.TextDim);
        }

        public void ToggleAltitude()
        {
            var helicopters = new List<Unit>();
            foreach (Unit unit in _selection)
            {
                if (unit.Kind == "aviation")
                    helicopters.Add(unit);
            }
            if (helicopters.Count == 0)
            {
                Notifications?.Push("고도 전환은 공격헬기만 할 수 있습니다", UiTheme.TextDim);
                return;
            }
            bool high = helicopters[0].Sop.Altitude != BattleSop.HighAltitude;
            foreach (Unit unit in helicopters)
            {
                IssueImmediateOrderForUnit(unit, BattleCommandIntent.Sop(
                    altitude: high ? BattleSop.HighAltitude : BattleSop.LowAltitude));
            }
            Notifications?.Push(
                "헬기 고도: " + (high ? "고공(멀리 보지만 잘 보임)" : "저공(지형 뒤에 숨음)"),
                UiTheme.TextDim);
        }

        public void SetFireMode(BattleFireAsset? asset, bool smoke = false)
        {
            Hud?.SetFireMode(asset, smoke);
            Input?.SetFireMode(asset, smoke);
        }

        public void CycleFireMode(bool smoke)
        {
            if (Input != null)
                Input.CycleFireMode(smoke);
            else if (Hud != null)
                Hud.CycleFireMode(smoke);
        }

        public Dictionary<string, object> RequestFire(Vector2 target)
        {
            RequireBattle();
            if (Hud == null || !Hud.FireAsset.HasValue || Battle.Deploying)
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = "화력 자산을 선택하세요."
                };
            }
            BattleFireAsset asset = Hud.FireAsset.Value;
            Dictionary<string, object> result = Battle.Fires.Request(
                Battle.PlayerSide,
                (FireSupportSystem.Asset)(int)asset,
                BattleViewAdapter.ToSim(target),
                Hud.FireSmoke);
            if (result.TryGetValue("ok", out object ok) && Convert.ToBoolean(ok, CultureInfo.InvariantCulture))
            {
                if (result.TryGetValue("mission", out object value) && value is FireSupportSystem.Mission mission)
                {
                    string observer = mission.ObserverQuality >= 0 && mission.ObserverQuality < ObserverNames.Length
                        ? ObserverNames[mission.ObserverQuality]
                        : ObserverNames[ObserverNames.Length - 1];
                    Notifications?.Push(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} 요청 — 약 {1:0}초 뒤 초탄 ({2})",
                            mission.Label(),
                            mission.DelaySec,
                            observer),
                        BattleEventText.Info);
                }
            }
            else
            {
                Notifications?.Push(
                    BattleUiText.FireAssetName(asset) + " 요청 불가: " +
                    (result.TryGetValue("error", out object error) ? Convert.ToString(error, CultureInfo.InvariantCulture) : string.Empty),
                    BattleEventText.Bad);
            }
            SetFireMode(null, false);
            return result;
        }

        public void TogglePause()
        {
            RequireBattle();
            if (!Battle.Deploying && !Battle.HasResult && !IsOverlayOpen)
                Battle.World.TogglePause();
        }

        public void SetPaused(bool paused)
        {
            RequireBattle();
            if (!Battle.Deploying && !Battle.HasResult && !IsOverlayOpen)
                Battle.World.SetPaused(paused);
        }

        public void ChangeSpeed(int direction)
        {
            RequireBattle();
            if (direction > 0)
                Battle.World.SpeedUp();
            else if (direction < 0)
                Battle.World.SpeedDown();
        }

        public void UpdateCursorPreview(Vector2 screenPosition)
        {
            if (Battle == null || Symbols == null)
                return;
            if (Battle.Deploying)
            {
                Symbols.SetCursorPreview(_selection.Count > 0 ? "배치: 우클릭으로 옮기기" : string.Empty, screenPosition);
                return;
            }
            if (Hud != null && Hud.FireAsset.HasValue)
            {
                Vector2 world = PickTerrain(screenPosition);
                if (float.IsInfinity(world.x) || float.IsInfinity(world.y))
                {
                    Symbols.SetCursorPreview(string.Empty, screenPosition, true);
                    return;
                }
                BattleFireAsset asset = Hud.FireAsset.Value;
                Dictionary<string, object> preview = Battle.Fires.Preview(
                    Battle.PlayerSide,
                    (FireSupportSystem.Asset)(int)asset,
                    BattleViewAdapter.ToSim(world));
                string name = BattleUiText.FireAssetName(asset) + (Hud.FireSmoke ? " 연막" : string.Empty);
                string text;
                if (preview.TryGetValue("error", out object error))
                {
                    text = name + " — " + Convert.ToString(error, CultureInfo.InvariantCulture);
                }
                else
                {
                    int quality = Convert.ToInt32(preview["quality"], CultureInfo.InvariantCulture);
                    double seconds = Convert.ToDouble(preview["seconds"], CultureInfo.InvariantCulture);
                    text = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} — {1}, 약 {2:0}초",
                        name,
                        quality >= 0 && quality < ObserverNames.Length ? ObserverNames[quality] : ObserverNames[2],
                        seconds);
                }
                Symbols.SetCursorPreview(text, screenPosition, true);
                return;
            }
            if (_selection.Count == 0 || Battle.OrderSystem == null || Hud == null)
            {
                Symbols.ClearCursorPreview();
                return;
            }
            Unit selected = _selection[0];
            bool bypass = selected is Platoon && selected.FollowsParent();
            double delay = Battle.OrderSystem.EstimateDelaySec(selected, (OrderType)(int)Hud.OrderMode, bypass);
            string orderName = BattleUiText.OrderName(Hud.OrderMode);
            Symbols.SetCursorPreview(
                delay < 0.0
                    ? orderName + " — 통신 두절, 명령 대기"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} — 실행까지 약 {1:0}초{2}",
                        orderName,
                        delay,
                        bypass ? "  (직접 명령: 분리, 용량 2)" : string.Empty),
                screenPosition);
        }

        public void SetDirectionDrag(Vector2 from, Vector2 to)
        {
            Symbols?.SetDirectionDrag(from, to);
        }

        public void ClearDirectionDrag()
        {
            Symbols?.ClearDirectionDrag();
        }

        public GuideScreen OpenGuide()
        {
            RequireBattle();
            if (_guide != null)
                return _guide;
            CloseOverlay();
            _guide = GuideScreen.Create(OverlayRoot);
            _overlay = _guide;
            _guide.Closed += OnGuideClosed;
            PauseForOverlay();
            RunWithSymbolsSuspended(_guide.Open);
            return _guide;
        }

        public void ToggleGuide()
        {
            if (IsGuideOpen)
                CloseOverlay();
            else
                OpenGuide();
        }

        public ForceBuilder OpenBuilder(ForceSpec initial = null, IForceStore store = null)
        {
            RequireBattle();
            CloseOverlay();
            RunWithSymbolsSuspended(() =>
            {
                _builder = ForceBuilder.Create(
                    OverlayRoot,
                    Battle.Catalog,
                    Battle.Scenario,
                    store ?? UnityForceStore.Create(),
                    initial);
            });
            _overlay = _builder;
            _builder.StartRequested += OnBuilderStart;
            _builder.BackRequested += OnBuilderBack;
            PauseForOverlay();
            return _builder;
        }

        public AarScreen ShowAar(AarData data = null)
        {
            RequireBattle();
            CloseOverlay();
            CurrentAar = data ?? CurrentAar ?? BattleViewAdapter.CreateAar(Battle.Stats.Summary());
            RunWithSymbolsSuspended(() =>
            {
                _aar = AarScreen.Create(OverlayRoot, CurrentAar, Battle.PlayerSide);
            });
            _overlay = _aar;
            _aar.ViewBattlefieldRequested += OnAarViewBattlefield;
            _aar.MenuRequested += OnAarMenu;
            PauseForOverlay();
            RunWithSymbolsSuspended(_aar.Open);
            return _aar;
        }

        private void RunWithSymbolsSuspended(Action action)
        {
            bool restore = Symbols != null && Symbols.gameObject.activeSelf;
            if (restore)
                Symbols.gameObject.SetActive(false);
            try
            {
                action();
            }
            finally
            {
                if (restore && Symbols != null)
                    Symbols.gameObject.SetActive(true);
            }
        }

        public void CloseOverlay()
        {
            Component old = _overlay;
            _overlay = null;
            if (_guide != null)
            {
                _guide.Closed -= OnGuideClosed;
                if (_guide.IsOpen)
                    _guide.Close();
                _guide = null;
            }
            if (_aar != null)
            {
                _aar.ViewBattlefieldRequested -= OnAarViewBattlefield;
                _aar.MenuRequested -= OnAarMenu;
                _aar = null;
            }
            if (_builder != null)
            {
                _builder.StartRequested -= OnBuilderStart;
                _builder.BackRequested -= OnBuilderBack;
                _builder = null;
            }
            if (old != null)
                DestroyUnityObject(old.gameObject);
            RestoreOverlayPause();
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
            BuildEnvironment();
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
            _composed = true;
            SetFog(_fogEnabled);
            RefreshPresentation(0f, 0f);
        }

        private void BuildEnvironment()
        {
            if (!_environmentCaptured)
            {
                _environmentCaptured = true;
                _previousSkybox = RenderSettings.skybox;
                _previousAmbientMode = RenderSettings.ambientMode;
                _previousAmbientLight = RenderSettings.ambientLight;
                _previousAmbientSky = RenderSettings.ambientSkyColor;
                _previousAmbientEquator = RenderSettings.ambientEquatorColor;
                _previousAmbientGround = RenderSettings.ambientGroundColor;
                _previousAmbientIntensity = RenderSettings.ambientIntensity;
                _previousFog = RenderSettings.fog;
                _previousFogColor = RenderSettings.fogColor;
                _previousFogMode = RenderSettings.fogMode;
                _previousFogDensity = RenderSettings.fogDensity;
            }

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                _skyboxMaterial = new Material(skyShader) { name = "BattleSky" };
                if (_skyboxMaterial.HasProperty("_SkyTint"))
                    _skyboxMaterial.SetColor("_SkyTint", new Color(0.36f, 0.52f, 0.72f));
                if (_skyboxMaterial.HasProperty("_GroundColor"))
                    _skyboxMaterial.SetColor("_GroundColor", new Color(0.25f, 0.27f, 0.23f));
                RenderSettings.skybox = _skyboxMaterial;
                RenderSettings.ambientMode = AmbientMode.Skybox;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.66f);
            }
            RenderSettings.ambientIntensity = 0.75f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.66f, 0.72f, 0.78f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00006f;

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(transform, false);
            sunObject.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("RtsCamera");
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.52f, 0.72f);
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
            var canvasObject = new GameObject(
                "BattleUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            BattleCanvas = canvasObject.GetComponent<Canvas>();
            BattleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            BattleCanvas.pixelPerfect = false;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            UiRoot = (RectTransform)canvasObject.transform;

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
            Symbols.UnitClicked += OnSymbolClicked;

            Hud = BattleHud.Create(UiRoot);
            Orbat = OrbatPanel.Create(UiRoot);
            Orbat.Bind(() => BattleViewAdapter.CreateOrbat(Battle));
            Notifications = NotificationFeed.Create(UiRoot);

            var minimapObject = new GameObject("Minimap", typeof(RectTransform), typeof(MinimapView));
            minimapObject.transform.SetParent(UiRoot, false);
            Minimap = minimapObject.GetComponent<MinimapView>();
            RectTransform minimapRect = (RectTransform)minimapObject.transform;
            minimapRect.anchorMin = new Vector2(1f, 0f);
            minimapRect.anchorMax = new Vector2(1f, 0f);
            minimapRect.pivot = new Vector2(1f, 0f);
            minimapRect.anchoredPosition = new Vector2(-8f, 246f);
            minimapRect.sizeDelta = new Vector2(220f, 220f);
            Minimap.SetTerrain(
                BattleViewAdapter.ToVector(Battle.Terrain.SizeM),
                position => BattleViewAdapter.SampleMinimapTerrain(Battle, position));

            DeploymentPanel = Echelon.Unity.UI.Battle.DeploymentPanel.Create(UiRoot);

            var guideButton = UiKit.CreateButton(UiRoot, "가이드  /", ToggleGuide, 100f);
            guideButton.name = "GuideButton";
            RectTransform guideRect = guideButton.GetComponent<RectTransform>();
            guideRect.anchorMin = new Vector2(0f, 1f);
            guideRect.anchorMax = new Vector2(0f, 1f);
            guideRect.pivot = new Vector2(0f, 1f);
            guideRect.anchoredPosition = new Vector2(8f, -8f);

            DebugOverlay = BattleDebugOverlay.Create(UiRoot);
            DebugOverlay.enabled = false;
            OverlayRoot = UiKit.CreateRect(UiRoot, "OverlayRoot");
            UiKit.Stretch(OverlayRoot);
            OverlayRoot.SetAsLastSibling();
            BuildInput();
        }

        private void BuildInput()
        {
            Input = gameObject.AddComponent<BattleInputController>();
            Input.Configure(
                Battle,
                RtsCamera.TargetCamera,
                RtsCamera.Bounds,
                position => BattleViewAdapter.GroundHeight(Battle, position));
            Input.BindHud(Hud);
            Input.BindOrbat(Orbat);
            Input.BindSymbols(Symbols);
            Input.BindDeploymentPanel(DeploymentPanel);
            Input.BindMinimap(Minimap);
            Input.BindCamera(RtsCamera);
            Input.SelectionChanged += OnInputSelection;
            Input.CommandIssued += OnInputCommand;
            Input.FocusRequested += FocusCamera;
            Input.GuideRequested += OnInputGuide;
            Input.NotificationRequested += OnInputNotification;
            Input.OrderModeChanged += OnInputOrderMode;
            Input.FireModeChanged += OnInputFireMode;
            Input.FogVisibilityChanged += SetFog;
            Input.DebugOverlayVisibilityChanged += OnInputDebugVisibility;
            Input.BattleStartRequested += StartBattle;
        }

        private void RefreshPresentation(float realDeltaSeconds, float gameDeltaSeconds)
        {
            if (Battle == null || !_composed)
                return;
            float alpha = (float)Battle.World.GetInterpolationAlpha();
            CurrentElementStates = BattleViewAdapter.CreateElementStates(Battle);
            ElementViews.Sync(CurrentElementStates);
            ElementViews.UpdateVisuals(
                realDeltaSeconds,
                gameDeltaSeconds,
                alpha,
                TickToInt(Battle.World.Tick));

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
            Fx.Advance(gameDeltaSeconds, TickToInt(Battle.World.Tick));

            SymbolSnapshot = BattleViewAdapter.CreateSymbolSnapshot(
                Battle, _selection, _fogEnabled, alpha, false);
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
            {
                DeploymentZones.Render(
                    BattleViewAdapter.CreateDeploymentZone(Battle),
                    ProjectSimulationToScreen);
            }
            else
            {
                DeploymentZones.Clear();
            }

            Hud.Bind(
                BattleViewAdapter.CreateHudState(Battle),
                SelectionViews(),
                BattleViewAdapter.CreateFireSupportState(Battle));
            Orbat.ShowSelection(SelectionIds());
            if (Input != null && !IsOverlayOpen)
                Input.enabled = !Battle.HasResult;
            if (Tutorial != null)
            {
                IEnumerable<int> tutorialGroup = Input != null
                    ? UnitIds(Input.GetGroupUnits(1))
                    : _controlGroups.TryGetValue(1, out List<int> group) ? group : null;
                BattleViewAdapter.UpdateTutorialState(
                    Battle,
                    TutorialState,
                    _selection,
                    tutorialGroup,
                    RtsCamera);
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
                ShowAarRequested?.Invoke(CurrentAar);
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

        private void IssuePlayerOrder(Unit unit, Order order)
        {
            order.Issuer = "player";
            Battle.IssueOrder(order);
            OrderIssued?.Invoke(order);
            PlayerOrderIssued?.Invoke(ToTutorialOrder(unit, order));
        }

        private void IssueImmediateOrderForUnit(Unit unit, BattleCommandIntent intent)
        {
            var sop = new JObject();
            if (!string.IsNullOrEmpty(intent.Technique))
                sop["technique"] = intent.Technique;
            if (!string.IsNullOrEmpty(intent.RulesOfEngagement))
                sop["roe"] = intent.RulesOfEngagement;
            if (!string.IsNullOrEmpty(intent.Altitude))
                sop["altitude"] = intent.Altitude;
            var parameters = new JObject { ["sop"] = sop };
            IssuePlayerOrder(unit, Order.Make((OrderType)(int)intent.Type, unit.Id, parameters));
        }

        private static TutorialPlayerOrderEvent ToTutorialOrder(Unit unit, Order order)
        {
            TutorialOrderType type;
            switch (order.Type)
            {
                case OrderType.MOVE:
                    type = TutorialOrderType.Move;
                    break;
                case OrderType.FAST_MOVE:
                    type = TutorialOrderType.FastMove;
                    break;
                case OrderType.ATTACK_MOVE:
                    type = TutorialOrderType.AttackMove;
                    break;
                case OrderType.SEIZE:
                    type = TutorialOrderType.Seize;
                    break;
                default:
                    type = TutorialOrderType.Other;
                    break;
            }
            string unitId = Id(unit.Id);
            string parentId = unit.Parent != null ? Id(unit.Parent.Id) : string.Empty;
            return order.HasPos()
                ? TutorialPlayerOrderEvent.At(type, unitId, parentId, BattleViewAdapter.ToVector(order.GetPos()))
                : TutorialPlayerOrderEvent.WithoutPosition(type, unitId, parentId);
        }

        private int ApplyDeployment(IReadOnlyList<DeploymentPlacementIntent> intents)
        {
            if (intents == null)
                return 0;
            int applied = 0;
            foreach (DeploymentPlacementIntent intent in intents)
            {
                if (DeployUnit(intent.UnitId, intent.Position, intent.HeadingRadians))
                    applied++;
            }
            if (applied > 0)
                RefreshPresentation(0f, 0f);
            return applied;
        }

        private void NotifySelectionChanged()
        {
            if (Input != null && !_syncingSelection)
            {
                _syncingSelection = true;
                try
                {
                    Input.SelectUnits(_selection);
                }
                finally
                {
                    _syncingSelection = false;
                }
            }
            if (Hud != null)
                Hud.SetSelection(SelectionViews());
            if (Orbat != null)
                Orbat.ShowSelection(SelectionIds());
            SelectionChanged?.Invoke(_selection);
        }

        private void PruneSelection()
        {
            bool changed = _selection.RemoveAll(unit => unit == null || unit.IsDestroyed()) > 0;
            if (changed)
                NotifySelectionChanged();
        }

        private IReadOnlyList<BattleUnitView> SelectionViews()
        {
            _selectionViews.Clear();
            foreach (Unit unit in _selection)
                _selectionViews.Add(BattleViewAdapter.CreateUnitView(Battle, unit));
            return _selectionViews;
        }

        private IReadOnlyList<int> SelectionIds()
        {
            _selectionIds.Clear();
            foreach (Unit unit in _selection)
                _selectionIds.Add(unit.Id);
            return _selectionIds;
        }

        private static List<int> UnitIds(IEnumerable<Unit> units)
        {
            var ids = new List<int>();
            if (units == null)
                return ids;
            foreach (Unit unit in units)
            {
                if (unit != null)
                    ids.Add(unit.Id);
            }
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

        private void OnSymbolClicked(SymbolPickResult result)
        {
            if (int.TryParse(result.UnitId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                SelectUnit(id);
        }

        private void OnInputSelection(IReadOnlyList<Unit> units)
        {
            if (_syncingSelection)
                return;
            _syncingSelection = true;
            try
            {
                SelectUnits(units);
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        private void OnInputCommand(Order order)
        {
            OrderIssued?.Invoke(order);
            Unit unit = Battle.GetUnit(order.TargetUnitId);
            if (unit != null)
                PlayerOrderIssued?.Invoke(ToTutorialOrder(unit, order));
        }

        private void OnInputGuide()
        {
            if (Input.GuideOpen)
                OpenGuide();
            else if (IsGuideOpen)
                CloseOverlay();
        }

        private void OnInputNotification(string text)
        {
            Notifications?.Push(text, UiTheme.TextDim);
        }

        private void OnInputOrderMode(BattleCommandType type)
        {
            Hud?.SetOrderMode(type);
        }

        private void OnInputFireMode(BattleFireAsset? asset, bool smoke)
        {
            Hud?.SetFireMode(asset, smoke);
        }

        private void OnInputDebugVisibility(bool visible)
        {
            DebugOverlay?.SetVisible(visible);
        }

        private void OnTutorialIntent(TutorialIntent intent)
        {
            switch (intent.Kind)
            {
                case TutorialIntentKind.CameraFocus:
                    FocusCamera(intent.SimPosition);
                    break;
                case TutorialIntentKind.RevealEnemy:
                    if (int.TryParse(intent.UnitId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                        && Battle.GetUnit(id) is Platoon platoon)
                    {
                        BattleSetup.RevealPlatoon(
                            Battle,
                            platoon,
                            BattleViewAdapter.ToSim(intent.SimPosition),
                            intent.HeadingRadians);
                        RefreshPresentation(0f, 0f);
                    }
                    break;
                case TutorialIntentKind.FireMode:
                    switch (intent.FireMode)
                    {
                        case TutorialFireMode.None:
                            SetFireMode(null);
                            break;
                        case TutorialFireMode.Mortar:
                            SetFireMode(BattleFireAsset.Mortar, intent.Smoke);
                            break;
                        case TutorialFireMode.Artillery:
                            SetFireMode(BattleFireAsset.Artillery, intent.Smoke);
                            break;
                        case TutorialFireMode.CloseAirSupport:
                            SetFireMode(BattleFireAsset.Cas, false);
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

        private void OnGuideClosed()
        {
            CloseOverlay();
        }

        private void OnAarViewBattlefield()
        {
            CloseOverlay();
        }

        private void OnAarMenu()
        {
            CloseOverlay();
            MenuRequested?.Invoke();
        }

        private void OnBuilderStart(ForceSpec spec)
        {
            BuilderStartRequested?.Invoke(spec);
        }

        private void OnBuilderBack()
        {
            BuilderBackRequested?.Invoke();
        }

        private void PauseForOverlay()
        {
            if (_overlayPauseActive)
                return;
            _overlayPauseActive = true;
            _pausedBeforeOverlay = Battle.World.Paused;
            _cameraInputBeforeOverlay = RtsCamera.InputEnabled;
            RtsCamera.InputEnabled = false;
            if (Input != null)
            {
                Input.SetGuideOpenFromView(_guide != null);
                Input.enabled = false;
            }
            Battle.World.SetPaused(true);
        }

        private void RestoreOverlayPause()
        {
            if (!_overlayPauseActive)
                return;
            _overlayPauseActive = false;
            if (RtsCamera != null)
                RtsCamera.InputEnabled = _cameraInputBeforeOverlay;
            if (Input != null)
            {
                Input.SetGuideOpenFromView(false);
                Input.enabled = !_disposed && Battle != null && !Battle.HasResult;
            }
            if (Battle != null)
                Battle.World.SetPaused(Battle.HasResult || _pausedBeforeOverlay);
        }

        private void Teardown()
        {
            if (_disposed)
                return;
            _disposed = true;
            _composed = false;
            UnwireUi();
            CloseOverlay();
            StopTutorial();
            if (_environmentCaptured)
            {
                RenderSettings.skybox = _previousSkybox;
                RenderSettings.ambientMode = _previousAmbientMode;
                RenderSettings.ambientLight = _previousAmbientLight;
                RenderSettings.ambientSkyColor = _previousAmbientSky;
                RenderSettings.ambientEquatorColor = _previousAmbientEquator;
                RenderSettings.ambientGroundColor = _previousAmbientGround;
                RenderSettings.ambientIntensity = _previousAmbientIntensity;
                RenderSettings.fog = _previousFog;
                RenderSettings.fogColor = _previousFogColor;
                RenderSettings.fogMode = _previousFogMode;
                RenderSettings.fogDensity = _previousFogDensity;
                _environmentCaptured = false;
            }
            if (_skyboxMaterial != null)
            {
                DestroyUnityObject(_skyboxMaterial);
                _skyboxMaterial = null;
            }
            if (Battle != null)
            {
                Battle.Dispose();
                Battle = null;
            }
            _selection.Clear();
            _controlGroups.Clear();
            CurrentElementStates = null;
            SymbolSnapshot = null;
            MinimapSnapshot = null;
        }

        private void UnwireUi()
        {
            if (Symbols != null)
                Symbols.UnitClicked -= OnSymbolClicked;
            if (Input != null)
            {
                Input.SelectionChanged -= OnInputSelection;
                Input.CommandIssued -= OnInputCommand;
                Input.FocusRequested -= FocusCamera;
                Input.GuideRequested -= OnInputGuide;
                Input.NotificationRequested -= OnInputNotification;
                Input.OrderModeChanged -= OnInputOrderMode;
                Input.FireModeChanged -= OnInputFireMode;
                Input.FogVisibilityChanged -= SetFog;
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
