using System;
using System.Collections.Generic;
using System.Globalization;
using Echelon.Core;
using Echelon.Unity.Presentation;
using Echelon.Unity.Presentation.Symbols;
using Echelon.Unity.UI.Battle;
using Echelon.Unity.UI.Minimap;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Echelon.Unity.App
{
    public delegate bool HeightFieldPickDelegate(Vector2 screenPosition, out Vector2 simPosition);

    public enum BattlePointerButton
    {
        Left,
        Right
    }

    [DisallowMultipleComponent]
    public sealed class BattleInputController : MonoBehaviour
    {
        public const double DoubleClickSeconds = 0.35;
        public const float DragThresholdPixels = 18f;
        public const float FacingDragMinimumMeters = 10f;
        public const float MoveSpacingMeters = 250f;

        private static readonly string[] ObserverNames =
        {
            "관측반 관측", "일반 엘리먼트 관측", "관측 없음, 지도 좌표 사격"
        };

        private static readonly string[] Techniques =
        {
            BattleSop.Traveling, BattleSop.Overwatch, BattleSop.Bounding
        };

        private static readonly Key[] TrackedKeys =
        {
            Key.Escape, Key.Slash, Key.Space, Key.Equals, Key.Minus, Key.NumpadPlus, Key.NumpadMinus,
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7,
            Key.Digit8, Key.Digit9, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5,
            Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9,
            Key.F1, Key.F2, Key.F3, Key.F4, Key.F9, Key.F10,
            Key.M, Key.F, Key.Q, Key.Y, Key.J, Key.B, Key.E, Key.H, Key.U, Key.L, Key.K,
            Key.T, Key.G, Key.P, Key.Tab, Key.V, Key.N, Key.I, Key.O, Key.R, Key.Z, Key.X, Key.C
        };

        private readonly List<Unit> _selection = new List<Unit>();
        private readonly List<int> _selectedUnitIds = new List<int>();
        private readonly Dictionary<int, List<Unit>> _groups = new Dictionary<int, List<Unit>>();

        private Battle _battle;
        private HeightFieldPickDelegate _heightFieldPick;
        private BattleHud _hud;
        private OrbatPanel _orbat;
        private BattlefieldSymbolLayer _symbols;
        private DeploymentPanel _deploymentPanel;
        private MinimapView _minimap;
        private RtsCameraController _camera;

        private double _lastClickTime = double.NegativeInfinity;
        private int _lastClickedUnitId = -1;
        private int _lastGroup = -1;
        private double _lastGroupTime = double.NegativeInfinity;

        private bool _rightDragActive;
        private Vector2 _rightClickScreen;
        private Vector2 _rightClickWorld;
        private Vector2 _pointerScreenPosition;

        public event Action<IReadOnlyList<Unit>> SelectionChanged;
        public event Action<Order> CommandIssued;
        public event Action<Vector2> FocusRequested;
        public event Action GuideRequested;
        public event Action<string> NotificationRequested;
        public event Action<BattleCommandType> OrderModeChanged;
        public event Action<BattleFireAsset?, bool> FireModeChanged;
        public event Action<FireSupportSystem.Mission> FireMissionRequested;
        public event Action<bool> FogVisibilityChanged;
        public event Action<bool> DebugOverlayVisibilityChanged;
        public event Action BattleStartRequested;

        public Battle Battle => _battle;
        public IReadOnlyList<Unit> Selection => _selection;
        public IReadOnlyList<Unit> SelectedUnits => _selection;
        public IReadOnlyList<int> SelectedUnitIds => _selectedUnitIds;
        public BattleCommandType OrderMode { get; private set; } = BattleCommandType.Move;
        public OrderType CurrentOrderType => ToOrderType(OrderMode);
        public BattleFireAsset? FireAsset { get; private set; }
        public bool FireSmoke { get; private set; }
        public bool GuideOpen { get; private set; }
        public bool FogEnabled { get; private set; } = true;
        public bool DebugOverlayVisible { get; private set; }
        public string CursorPreviewText { get; private set; } = string.Empty;
        public bool CursorPreviewForced { get; private set; }
        public bool IsRightDragging => _rightDragActive;
        public bool HasDirectionPreview { get; private set; }
        public Vector2 DirectionDragFrom { get; private set; }
        public Vector2 DirectionDragTo { get; private set; }
        public Func<double> TimeSource { get; set; }
        public Func<bool> UiKeyboardFocusProbe { get; set; }
        public Func<Vector2, bool> PointerOverUiProbe { get; set; }

        public void Configure(Battle battle, HeightFieldPickDelegate heightFieldPick = null)
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            if (!ReferenceEquals(_battle, battle))
            {
                CancelPointerInteraction();
                _battle = battle;
                _groups.Clear();
                ReplaceSelection(null);
                _lastGroup = -1;
                _lastGroupTime = double.NegativeInfinity;
                _lastClickTime = double.NegativeInfinity;
                _lastClickedUnitId = -1;
                SetOrderMode(BattleCommandType.Move);
                ClearFireMode();
            }
            _heightFieldPick = heightFieldPick;
            TimeSource ??= () => Time.unscaledTimeAsDouble;
        }

        public void Configure(
            Battle battle,
            UnityEngine.Camera camera,
            MapBounds bounds,
            HeightSampler sampleHeight)
        {
            Configure(battle);
            SetHeightFieldPicker(camera, bounds, sampleHeight);
        }

        public void SetHeightFieldPicker(
            UnityEngine.Camera camera,
            MapBounds bounds,
            HeightSampler sampleHeight)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            if (sampleHeight == null)
                throw new ArgumentNullException(nameof(sampleHeight));
            _heightFieldPick = (Vector2 screenPosition, out Vector2 simPosition) =>
                HeightFieldPicker.TryPick(camera, screenPosition, bounds, sampleHeight, out simPosition);
        }

        public void SetHeightFieldPickDelegate(HeightFieldPickDelegate heightFieldPick)
        {
            _heightFieldPick = heightFieldPick;
        }

        public void BindHud(BattleHud hud)
        {
            if (ReferenceEquals(_hud, hud))
                return;
            UnbindHud();
            _hud = hud;
            if (_hud == null)
                return;
            _hud.OrderModeSelected += SetOrderMode;
            _hud.ImmediateOrderRequested += OnHudImmediateOrder;
            _hud.FormationSelected += SetFormation;
            _hud.FireModeSelected += OnHudFireMode;
            _hud.AltitudeToggleRequested += ToggleAltitude;
            _hud.PauseToggleRequested += TogglePause;
            _hud.SpeedChangeRequested += ChangeSpeed;
            _hud.SetOrderMode(OrderMode);
            _hud.SetFireMode(FireAsset, FireSmoke);
        }

        public void BindOrbat(OrbatPanel orbat)
        {
            if (ReferenceEquals(_orbat, orbat))
                return;
            if (_orbat != null)
            {
                _orbat.SelectionRequested -= OnOrbatSelection;
                _orbat.FocusRequested -= OnOrbatFocus;
            }
            _orbat = orbat;
            if (_orbat == null)
                return;
            _orbat.SelectionRequested += OnOrbatSelection;
            _orbat.FocusRequested += OnOrbatFocus;
            _orbat.ShowSelection(_selectedUnitIds);
        }

        public void BindSymbols(BattlefieldSymbolLayer symbols)
        {
            _symbols = symbols;
            if (_symbols == null)
                return;
            _symbols.ClearDirectionDrag();
            _symbols.SetCursorPreview(CursorPreviewText, _pointerScreenPosition, CursorPreviewForced);
        }

        public void BindDeploymentPanel(DeploymentPanel panel)
        {
            if (ReferenceEquals(_deploymentPanel, panel))
                return;
            if (_deploymentPanel != null)
            {
                _deploymentPanel.StartRequested -= RequestBattleStart;
                _deploymentPanel.PlacementRequested -= OnDeploymentPlacement;
            }
            _deploymentPanel = panel;
            if (_deploymentPanel == null)
                return;
            _deploymentPanel.StartRequested += RequestBattleStart;
            _deploymentPanel.PlacementRequested += OnDeploymentPlacement;
        }

        public void BindMinimap(MinimapView minimap)
        {
            if (ReferenceEquals(_minimap, minimap))
                return;
            if (_minimap != null)
                _minimap.NavigationRequested -= RequestFocus;
            _minimap = minimap;
            if (_minimap != null)
                _minimap.NavigationRequested += RequestFocus;
        }

        public void BindCamera(RtsCameraController camera)
        {
            _camera = camera;
            UpdateCameraInputBlockers();
        }

        public void Update()
        {
            UpdateCameraInputBlockers();
            if (_battle == null)
                return;
            PruneSelection();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool control = ControlHeld(keyboard);
                bool shift = ShiftHeld(keyboard);
                bool alt = AltHeld(keyboard);
                foreach (Key key in TrackedKeys)
                {
                    if (keyboard[key].wasPressedThisFrame)
                        HandleKey(key, control, shift, alt);
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                bool control = keyboard != null && ControlHeld(keyboard);
                bool shift = keyboard != null && ShiftHeld(keyboard);
                bool pointerOverUi = IsPointerOverUi(position);
                if (!pointerOverUi && mouse.leftButton.wasPressedThisFrame)
                    PointerDown(position, BattlePointerButton.Left, control, shift);
                if (!pointerOverUi && mouse.rightButton.wasPressedThisFrame)
                    PointerDown(position, BattlePointerButton.Right, control, shift);
                PointerMove(position);
                if (mouse.rightButton.wasReleasedThisFrame)
                    PointerUp(position, BattlePointerButton.Right, control, shift);
            }
            else
            {
                UpdateCursorPreview(_pointerScreenPosition);
            }
        }

        public bool HandleKey(Key key, bool control = false, bool shift = false, bool alt = false)
        {
            if (_battle == null)
                return false;
            if (GuideOpen)
            {
                if (key != Key.Escape && key != Key.Slash)
                    return false;
                RequestGuideToggle();
                return true;
            }

            int group = GroupNumber(key);
            if (group > 0)
            {
                if (alt)
                    SelectFormation(group - 1);
                else if (control)
                    AssignGroup(group);
                else
                    RecallGroup(group, shift);
                return true;
            }

            switch (key)
            {
                case Key.Space:
                    TogglePause();
                    return true;
                case Key.Equals:
                case Key.NumpadPlus:
                    ChangeSpeed(1);
                    return true;
                case Key.Minus:
                case Key.NumpadMinus:
                    ChangeSpeed(-1);
                    return true;
                case Key.Slash:
                    RequestGuideToggle();
                    return true;
                case Key.F9:
                    SetFogEnabled(!FogEnabled);
                    return true;
                case Key.F10:
                    SetDebugOverlayVisible(!DebugOverlayVisible);
                    return true;
                case Key.M:
                    SetOrderMode(BattleCommandType.Move);
                    return true;
                case Key.F:
                    SetOrderMode(BattleCommandType.FastMove);
                    return true;
                case Key.Q:
                    SetOrderMode(BattleCommandType.AttackMove);
                    return true;
                case Key.Y:
                    SetOrderMode(BattleCommandType.Seize);
                    return true;
                case Key.J:
                    SetOrderMode(BattleCommandType.Defend);
                    return true;
                case Key.B:
                    SetOrderMode(BattleCommandType.Withdraw);
                    return true;
                case Key.E:
                    IssueImmediateOrders(BattleCommandType.Halt);
                    SetOrderMode(BattleCommandType.Move);
                    return true;
                case Key.H:
                    ToggleHoldFire();
                    return true;
                case Key.U:
                    ToggleMount();
                    return true;
                case Key.L:
                    CycleTechnique();
                    return true;
                case Key.K:
                    IssueImmediateOrders(BattleCommandType.Resupply);
                    return true;
                case Key.T:
                    CycleFireMode(false);
                    return true;
                case Key.G:
                    CycleFireMode(true);
                    return true;
                case Key.P:
                    SetFireMode(BattleFireAsset.Cas, false);
                    return true;
                case Key.Tab:
                    NavigateSelection(!shift);
                    return true;
                case Key.F1:
                    SelectCompany(0);
                    return true;
                case Key.F2:
                    SelectCompany(1);
                    return true;
                case Key.F3:
                    SelectCompany(2);
                    return true;
                case Key.F4:
                    SelectCompany(3);
                    return true;
                case Key.V:
                    ToggleAltitude();
                    return true;
                case Key.N:
                    SetOrderMode(BattleCommandType.LayMines);
                    return true;
                case Key.I:
                    SetOrderMode(BattleCommandType.ClearMines);
                    return true;
                case Key.O:
                    SetOrderMode(BattleCommandType.Fortify);
                    return true;
                case Key.R:
                    NotifyNotImplemented("후진");
                    return true;
                case Key.Z:
                    NotifyNotImplemented("질주");
                    return true;
                case Key.X:
                    NotifyNotImplemented("연막탄 발사");
                    return true;
                case Key.C:
                    NotifyNotImplemented("기만체");
                    return true;
                case Key.Escape:
                    if (FireAsset.HasValue)
                        ClearFireMode();
                    else if (_rightDragActive)
                        CancelPointerInteraction();
                    else
                        ClearSelection();
                    return true;
                default:
                    return false;
            }
        }

        public bool PointerDown(
            Vector2 screenPosition,
            BattlePointerButton button,
            bool control = false,
            bool shift = false)
        {
            _pointerScreenPosition = screenPosition;
            if (_battle == null || GuideOpen || IsPointerOverUi(screenPosition))
                return false;

            if (button == BattlePointerButton.Left && FireAsset.HasValue && !_battle.Deploying)
            {
                if (TryPickTerrain(screenPosition, out Vector2 fireTarget))
                    RequestFireSupport(fireTarget);
                return true;
            }
            if (button == BattlePointerButton.Right && FireAsset.HasValue)
            {
                ClearFireMode();
                return true;
            }
            if (button == BattlePointerButton.Left)
            {
                int unitId = -1;
                if (_symbols != null)
                {
                    string picked = _symbols.PickUnit(screenPosition, _battle.PlayerSide);
                    int.TryParse(picked, NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId);
                }
                ClickUnit(unitId, control);
                return true;
            }

            if (TryPickTerrain(screenPosition, out Vector2 target))
            {
                _rightDragActive = true;
                _rightClickScreen = screenPosition;
                _rightClickWorld = target;
                DirectionDragFrom = target;
                DirectionDragTo = target;
                HasDirectionPreview = false;
            }
            return true;
        }

        public bool PointerUp(
            Vector2 screenPosition,
            BattlePointerButton button,
            bool control = false,
            bool shift = false)
        {
            _pointerScreenPosition = screenPosition;
            if (button != BattlePointerButton.Right || !_rightDragActive || _battle == null)
                return false;

            Vector2 target = _rightClickWorld;
            double? facing = null;
            if (HasDirectionPreview && Vector2.Distance(DirectionDragTo, _rightClickWorld) > FacingDragMinimumMeters)
            {
                Vector2 direction = DirectionDragTo - _rightClickWorld;
                facing = Math.Atan2(direction.x, direction.y);
            }

            if (_battle.Deploying)
                DeploySelection(ToSimVec2(target), facing);
            else
                IssueMovementOrders(ToSimVec2(target), facing, shift);
            CancelPointerInteraction();
            return true;
        }

        public void PointerMove(Vector2 screenPosition)
        {
            _pointerScreenPosition = screenPosition;
            if (_rightDragActive && Vector2.Distance(screenPosition, _rightClickScreen) > DragThresholdPixels
                && TryPickTerrain(screenPosition, out Vector2 dragTarget))
            {
                DirectionDragTo = dragTarget;
                HasDirectionPreview = true;
                if (_symbols != null)
                    _symbols.SetDirectionDrag(DirectionDragFrom, DirectionDragTo);
            }
            UpdateCursorPreview(screenPosition);
        }

        public void CancelPointerInteraction()
        {
            _rightDragActive = false;
            HasDirectionPreview = false;
            if (_symbols != null)
                _symbols.ClearDirectionDrag();
        }

        public void OnSymbolClicked(SymbolPickResult pick)
        {
            if (!int.TryParse(pick.UnitId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int unitId))
                return;
            ClickUnit(unitId, ControlHeld());
        }

        public void ClickUnit(int unitId, bool control = false)
        {
            Unit unit = SelectableUnit(unitId);
            double now = NowSeconds();
            int clickedId = unit?.Id ?? -1;
            if (unit != null && unit.Parent != null && unit.Parent.Level == UnitLevel.Company
                && clickedId == _lastClickedUnitId && now - _lastClickTime < DoubleClickSeconds)
            {
                unit = unit.Parent;
            }
            _lastClickTime = now;
            _lastClickedUnitId = clickedId;

            if (unit == null)
            {
                ClearSelection();
                return;
            }
            if (!control)
            {
                ReplaceSelection(new[] { unit });
                return;
            }

            var changed = new List<Unit>(_selection);
            int existing = changed.IndexOf(unit);
            if (existing >= 0)
                changed.RemoveAt(existing);
            else
                changed.Add(unit);
            ReplaceSelection(changed);
        }

        public void SelectUnit(int unitId, bool additive = false)
        {
            Unit unit = SelectableUnit(unitId);
            if (unit == null)
            {
                if (!additive)
                    ClearSelection();
                return;
            }
            if (!additive)
            {
                ReplaceSelection(new[] { unit });
                return;
            }
            var units = new List<Unit>(_selection);
            if (!units.Contains(unit))
                units.Add(unit);
            ReplaceSelection(units);
        }

        public void SelectUnits(IEnumerable<Unit> units)
        {
            ReplaceSelection(units);
        }

        public void SelectUnitsById(IEnumerable<int> unitIds, bool additive = false)
        {
            var units = additive ? new List<Unit>(_selection) : new List<Unit>();
            if (unitIds != null)
            {
                foreach (int id in unitIds)
                {
                    Unit unit = SelectableUnit(id);
                    if (unit != null && !units.Contains(unit))
                        units.Add(unit);
                }
            }
            ReplaceSelection(units);
        }

        public void ClearSelection()
        {
            ReplaceSelection(null);
        }

        public void PruneSelection()
        {
            ReplaceSelection(_selection);
        }

        public IReadOnlyList<Unit> GetGroupUnits(int group)
        {
            var result = new List<Unit>();
            if (!_groups.TryGetValue(group, out List<Unit> units))
                return result;
            foreach (Unit unit in units)
            {
                if (IsSelectable(unit))
                    result.Add(unit);
            }
            _groups[group] = new List<Unit>(result);
            return result;
        }

        public void AssignGroup(int group)
        {
            if (!ValidGroup(group) || _selection.Count == 0)
                return;
            _groups[group] = new List<Unit>(_selection);
            Notify(string.Format(CultureInfo.InvariantCulture, "부대 지정 {0}: {1}개 제대", group, _selection.Count));
        }

        public void RecallGroup(int group, bool additive = false)
        {
            if (!ValidGroup(group) || !_groups.ContainsKey(group))
                return;
            IReadOnlyList<Unit> units = GetGroupUnits(group);
            if (units.Count == 0)
                return;

            double now = NowSeconds();
            bool focus = group == _lastGroup && now - _lastGroupTime < DoubleClickSeconds;
            _lastGroup = group;
            _lastGroupTime = now;
            if (additive)
            {
                var merged = new List<Unit>(_selection);
                foreach (Unit unit in units)
                {
                    if (!merged.Contains(unit))
                        merged.Add(unit);
                }
                ReplaceSelection(merged);
            }
            else
            {
                ReplaceSelection(units);
            }
            if (focus)
                RequestFocus(ToVector2(units[0].Centroid()));
        }

        public void NavigateSelection(bool down)
        {
            if (_selection.Count == 0)
                return;
            Unit[] selected = _selection.ToArray();
            var result = new List<Unit>();
            foreach (Unit unit in selected)
            {
                if (down)
                {
                    foreach (Unit child in unit.ChildrenUnits())
                    {
                        if (IsSelectable(child) && !result.Contains(child))
                            result.Add(child);
                    }
                }
                else if (unit.Parent != null && unit.Parent.Level != UnitLevel.TaskForce
                         && IsSelectable(unit.Parent) && !result.Contains(unit.Parent))
                {
                    result.Add(unit.Parent);
                }
            }
            if (result.Count > 0)
                ReplaceSelection(result);
        }

        public void SelectCompany(int index, bool focus = true)
        {
            TaskForce taskForce = _battle?.GetTaskForce(_battle.PlayerSide);
            if (taskForce == null || index < 0 || index >= taskForce.Companies.Count)
                return;
            Company company = taskForce.Companies[index];
            ReplaceSelection(new[] { company });
            if (focus)
                RequestFocus(ToVector2(company.Centroid()));
        }

        public bool FocusUnit(int unitId)
        {
            Unit unit = _battle?.GetUnit(unitId);
            if (unit == null)
                return false;
            RequestFocus(ToVector2(unit.Centroid()));
            return true;
        }

        public void RequestFocus(Vector2 simPosition)
        {
            if (_camera != null)
                _camera.FocusOnSim(simPosition);
            FocusRequested?.Invoke(simPosition);
        }

        public void SetOrderMode(BattleCommandType type)
        {
            if (!IsTargetOrder(type) || OrderMode == type)
                return;
            OrderMode = type;
            if (_hud != null)
                _hud.SetOrderMode(type);
            OrderModeChanged?.Invoke(type);
            UpdateCursorPreview(_pointerScreenPosition);
        }

        public void SetFormation(string formationId)
        {
            if (string.IsNullOrEmpty(formationId))
                return;
            IssueImmediateOrders(BattleCommandType.SetFormation, new JObject { ["formation"] = formationId });
        }

        public void SelectFormation(int index)
        {
            if (_selection.Count == 0)
                return;
            BattleUnitLevel level = _selection[0].Level == UnitLevel.Company
                ? BattleUnitLevel.Company
                : BattleUnitLevel.Platoon;
            IReadOnlyList<string> ids = BattleFormationCatalog.IdsFor(level);
            if (index >= 0 && index < ids.Count)
                SetFormation(ids[index]);
        }

        public IReadOnlyList<Order> IssueMovementOrders(
            SimVec2 target,
            double? facing = null,
            bool append = false)
        {
            var orders = new List<Order>();
            if (_battle == null || _battle.Deploying || _selection.Count == 0)
                return orders;

            Unit[] selected = _selection.ToArray();
            for (int i = 0; i < selected.Length; i++)
            {
                Unit unit = selected[i];
                double heading = facing.HasValue && !double.IsNaN(facing.Value)
                    ? facing.Value
                    : FormationSolver.HeadingFromVector(target - unit.Guide.Pos);
                double offset = (i - (selected.Length - 1) * 0.5) * MoveSpacingMeters;
                var parameters = new JObject();
                if (append)
                    parameters["append"] = true;
                Order order = Order.Make(ToOrderType(OrderMode), unit.Id, parameters);
                order.Issuer = "player";
                order.Bypass = unit.Level == UnitLevel.Platoon && unit.FollowsParent();
                order.SetPos(_battle.Terrain.ClampToMap(target + FormationSolver.RightVector(heading) * offset));
                if (facing.HasValue && !double.IsNaN(facing.Value))
                    order.SetFacing(facing.Value);
                orders.Add(IssueOrder(order));
            }
            if (orders.Count > 0)
                SetOrderMode(BattleCommandType.Move);
            return orders;
        }

        public IReadOnlyList<Order> IssueMovementOrders(
            Vector2 target,
            float? facing = null,
            bool append = false)
        {
            return IssueMovementOrders(ToSimVec2(target), facing.HasValue ? (double?)facing.Value : null, append);
        }

        public IReadOnlyList<Order> IssueImmediateOrders(
            BattleCommandType type,
            JObject parameters = null)
        {
            if (type == BattleCommandType.CallFire)
                return Array.Empty<Order>();
            return IssueToUnits(_selection.ToArray(), ToOrderType(type), parameters);
        }

        public IReadOnlyList<Order> IssueImmediateOrder(BattleCommandIntent intent)
        {
            if (intent == null || intent.Type == BattleCommandType.CallFire)
                return Array.Empty<Order>();
            JObject parameters = null;
            if (intent.Type == BattleCommandType.SetFormation)
            {
                if (string.IsNullOrEmpty(intent.FormationId))
                    return Array.Empty<Order>();
                parameters = new JObject { ["formation"] = intent.FormationId };
            }
            else if (intent.Type == BattleCommandType.SetSop)
            {
                parameters = SopParameters(intent.Technique, intent.RulesOfEngagement, intent.Altitude);
            }
            return IssueImmediateOrders(intent.Type, parameters);
        }

        public void ToggleHoldFire()
        {
            if (_selection.Count == 0)
                return;
            bool hold = _selection[0].Sop.Roe != BattleSop.HoldFire;
            IssueImmediateOrders(BattleCommandType.SetSop,
                SopParameters(rulesOfEngagement: hold ? BattleSop.HoldFire : BattleSop.FireAtWill));
            Notify("사격 " + (hold ? "중지(응사만)" : "자유"));
        }

        public void ToggleMount()
        {
            bool anyMounted = false;
            foreach (Unit unit in _selection)
            {
                var platoons = new List<Unit> { unit };
                platoons.AddRange(unit.ChildrenUnits());
                foreach (Unit child in platoons)
                {
                    if (child is Platoon platoon && platoon.HasSquads() && platoon.Mounted)
                        anyMounted = true;
                }
            }
            IssueImmediateOrders(anyMounted ? BattleCommandType.Dismount : BattleCommandType.Mount);
        }

        public void CycleTechnique()
        {
            if (_selection.Count == 0)
                return;
            int index = Array.IndexOf(Techniques, _selection[0].Sop.Technique);
            string next = Techniques[(index + 1) % Techniques.Length];
            IssueImmediateOrders(BattleCommandType.SetSop, SopParameters(technique: next));
            Notify("이동 방식: " + BattleUiText.TechniqueName(next));
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
                Notify("고도 전환은 공격헬기만 할 수 있습니다");
                return;
            }
            bool high = helicopters[0].Sop.Altitude != BattleSop.HighAltitude;
            IssueToUnits(helicopters.ToArray(), OrderType.SET_SOP,
                SopParameters(altitude: high ? BattleSop.HighAltitude : BattleSop.LowAltitude));
            Notify("헬기 고도: " + (high ? "고공(멀리 보지만 잘 보임)" : "저공(지형 뒤에 숨음)"));
        }

        public void SetFireMode(BattleFireAsset? asset, bool smoke = false)
        {
            BattleFireAsset? previous = FireAsset;
            bool previousSmoke = FireSmoke;
            FireAsset = asset;
            FireSmoke = asset.HasValue && asset.Value != BattleFireAsset.Cas && smoke;
            if (_hud != null)
                _hud.SetFireMode(FireAsset, FireSmoke);
            if (previous != FireAsset || previousSmoke != FireSmoke)
                FireModeChanged?.Invoke(FireAsset, FireSmoke);
            UpdateCursorPreview(_pointerScreenPosition);
        }

        public void CycleFireMode(bool smoke)
        {
            if (FireAsset == BattleFireAsset.Mortar && FireSmoke == smoke)
                SetFireMode(BattleFireAsset.Artillery, smoke);
            else if (FireAsset == BattleFireAsset.Artillery && FireSmoke == smoke)
                ClearFireMode();
            else
                SetFireMode(BattleFireAsset.Mortar, smoke);
        }

        public void ClearFireMode()
        {
            SetFireMode(null, false);
        }

        public FireSupportSystem.Mission RequestFireSupport(SimVec2 target)
        {
            if (_battle == null || _battle.Deploying || !FireAsset.HasValue)
                return null;
            BattleFireAsset requestedAsset = FireAsset.Value;
            bool requestedSmoke = FireSmoke;
            Dictionary<string, object> result = _battle.Fires.Request(
                _battle.PlayerSide,
                (FireSupportSystem.Asset)(int)requestedAsset,
                target,
                requestedSmoke);
            FireSupportSystem.Mission mission = null;
            bool ok = result.TryGetValue("ok", out object value) && value is bool succeeded && succeeded;
            if (ok)
            {
                mission = result.TryGetValue("mission", out object rawMission)
                    ? rawMission as FireSupportSystem.Mission
                    : null;
                if (mission != null)
                {
                    FireMissionRequested?.Invoke(mission);
                    int observer = Mathf.Clamp(mission.ObserverQuality, 0, ObserverNames.Length - 1);
                    Notify(string.Format(CultureInfo.InvariantCulture,
                        "{0} 요청 — 약 {1:0}초 뒤 초탄 ({2})",
                        mission.Label(), mission.DelaySec, ObserverNames[observer]));
                }
            }
            else
            {
                string error = result.TryGetValue("error", out object rawError)
                    ? Convert.ToString(rawError, CultureInfo.InvariantCulture)
                    : string.Empty;
                Notify(string.Format(CultureInfo.InvariantCulture, "{0} 요청 불가: {1}",
                    BattleUiText.FireAssetName(requestedAsset), error));
            }
            ClearFireMode();
            return mission;
        }

        public FireSupportSystem.Mission RequestFireSupport(Vector2 target)
        {
            return RequestFireSupport(ToSimVec2(target));
        }

        public void UpdateCursorPreview(Vector2 screenPosition)
        {
            _pointerScreenPosition = screenPosition;
            string text = string.Empty;
            bool force = false;
            if (_battle != null)
            {
                if (_battle.Deploying)
                {
                    if (_selection.Count > 0)
                        text = "배치: 우클릭으로 옮기기";
                }
                else if (FireAsset.HasValue)
                {
                    force = true;
                    if (TryPickTerrain(screenPosition, out Vector2 target))
                        text = FirePreviewText(FireAsset.Value, FireSmoke, ToSimVec2(target));
                }
                else if (_selection.Count > 0 && _battle.OrderSystem != null)
                {
                    Unit unit = _selection[0];
                    bool bypass = unit.Level == UnitLevel.Platoon && unit.FollowsParent();
                    double seconds = _battle.OrderSystem.EstimateDelaySec(unit, ToOrderType(OrderMode), bypass);
                    string label = BattleUiText.OrderName(OrderMode);
                    text = seconds < 0.0
                        ? label + " — 통신 두절, 명령 대기"
                        : string.Format(CultureInfo.InvariantCulture, "{0} — 실행까지 약 {1:0}초{2}",
                            label, seconds, bypass ? "  (직접 명령: 분리, 용량 2)" : string.Empty);
                }
            }
            CursorPreviewText = text;
            CursorPreviewForced = force;
            if (_symbols != null)
                _symbols.SetCursorPreview(text, screenPosition, force);
        }

        public int DeploySelection(SimVec2 target, double? facing = null)
        {
            if (_battle == null || !_battle.Deploying || _selection.Count == 0)
                return 0;
            var states = new List<DeploymentUnitState>(_selection.Count);
            foreach (Unit unit in _selection)
                states.Add(new DeploymentUnitState(unit.Id, (float)unit.Guide.Heading));
            float? heading = facing.HasValue && !double.IsNaN(facing.Value) ? (float?)facing.Value : null;
            IReadOnlyList<DeploymentPlacementIntent> placements = DeploymentPlacement.CreateIntents(
                states, ToVector2(target), heading);
            return PlaceDeployment(placements);
        }

        public int DeploySelection(Vector2 target, float? facing = null)
        {
            return DeploySelection(ToSimVec2(target), facing.HasValue ? (double?)facing.Value : null);
        }

        public int PlaceDeployment(IReadOnlyList<DeploymentPlacementIntent> placements)
        {
            if (_battle == null || !_battle.Deploying || placements == null)
                return 0;
            int placed = 0;
            foreach (DeploymentPlacementIntent placement in placements)
            {
                if (placement == null)
                    continue;
                Unit unit = _battle.GetUnit(placement.UnitId);
                if (unit != null && _battle.DeployUnit(unit,
                        ToSimVec2(placement.Position), placement.HeadingRadians))
                {
                    placed++;
                }
            }
            return placed;
        }

        public void RequestBattleStart()
        {
            BattleStartRequested?.Invoke();
        }

        public void TogglePause()
        {
            if (_battle != null && !_battle.Deploying)
                _battle.World.TogglePause();
        }

        public void ChangeSpeed(int direction)
        {
            if (_battle == null || direction == 0)
                return;
            if (direction > 0)
                _battle.World.SpeedUp();
            else
                _battle.World.SpeedDown();
        }

        public void RequestGuideToggle()
        {
            SetGuideOpen(!GuideOpen);
            GuideRequested?.Invoke();
        }

        public void SetGuideOpen(bool open)
        {
            GuideOpen = open;
            if (open)
                CancelPointerInteraction();
            UpdateCameraInputBlockers();
        }

        public void SetFogEnabled(bool enabled)
        {
            if (FogEnabled == enabled)
                return;
            FogEnabled = enabled;
            FogVisibilityChanged?.Invoke(enabled);
            Notify("전장의 안개 " + (enabled ? "켬" : "끔"));
        }

        public void SetDebugOverlayVisible(bool visible)
        {
            if (DebugOverlayVisible == visible)
                return;
            DebugOverlayVisible = visible;
            DebugOverlayVisibilityChanged?.Invoke(visible);
        }

        public void SetGuideOpenFromView(bool open)
        {
            SetGuideOpen(open);
        }

        private IReadOnlyList<Order> IssueToUnits(
            IEnumerable<Unit> units,
            OrderType type,
            JObject parameters)
        {
            var orders = new List<Order>();
            if (_battle == null || units == null)
                return orders;
            foreach (Unit unit in units)
            {
                if (!IsSelectable(unit))
                    continue;
                Order order = Order.Make(type, unit.Id,
                    parameters == null ? new JObject() : (JObject)parameters.DeepClone());
                order.Issuer = "player";
                orders.Add(IssueOrder(order));
            }
            return orders;
        }

        private Order IssueOrder(Order order)
        {
            _battle.IssueOrder(order);
            CommandIssued?.Invoke(order);
            return order;
        }

        private string FirePreviewText(BattleFireAsset asset, bool smoke, SimVec2 target)
        {
            Dictionary<string, object> preview = _battle.Fires.Preview(
                _battle.PlayerSide, (FireSupportSystem.Asset)(int)asset, target);
            string name = BattleUiText.FireAssetName(asset) + (smoke ? " 연막" : string.Empty);
            if (preview.TryGetValue("error", out object error))
                return name + " — " + Convert.ToString(error, CultureInfo.InvariantCulture);
            int quality = preview.TryGetValue("quality", out object rawQuality)
                ? Convert.ToInt32(rawQuality, CultureInfo.InvariantCulture)
                : 2;
            double seconds = preview.TryGetValue("seconds", out object rawSeconds)
                ? Convert.ToDouble(rawSeconds, CultureInfo.InvariantCulture)
                : 0.0;
            quality = Mathf.Clamp(quality, 0, ObserverNames.Length - 1);
            return string.Format(CultureInfo.InvariantCulture, "{0} — {1}, 약 {2:0}초",
                name, ObserverNames[quality], seconds);
        }

        private void ReplaceSelection(IEnumerable<Unit> units)
        {
            var result = new List<Unit>();
            if (units != null)
            {
                foreach (Unit unit in units)
                {
                    if (IsSelectable(unit) && !result.Contains(unit))
                        result.Add(unit);
                }
            }

            bool changed = result.Count != _selection.Count;
            if (!changed)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    if (!ReferenceEquals(result[i], _selection[i]))
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed)
                return;

            _selection.Clear();
            _selection.AddRange(result);
            _selectedUnitIds.Clear();
            foreach (Unit unit in _selection)
                _selectedUnitIds.Add(unit.Id);
            SelectionChanged?.Invoke(_selection);
            if (_orbat != null)
                _orbat.ShowSelection(_selectedUnitIds);
            UpdateCursorPreview(_pointerScreenPosition);
        }

        private Unit SelectableUnit(int unitId)
        {
            Unit unit = _battle?.GetUnit(unitId);
            return IsSelectable(unit) ? unit : null;
        }

        private bool IsSelectable(Unit unit)
        {
            return _battle != null && unit != null && unit.Side == _battle.PlayerSide
                && unit.Level != UnitLevel.TaskForce && !unit.IsDestroyed();
        }

        private bool TryPickTerrain(Vector2 screenPosition, out Vector2 target)
        {
            target = default;
            if (_heightFieldPick == null || !_heightFieldPick(screenPosition, out target)
                || float.IsNaN(target.x) || float.IsNaN(target.y)
                || float.IsInfinity(target.x) || float.IsInfinity(target.y))
            {
                target = default;
                return false;
            }
            return true;
        }

        private void OnOrbatSelection(OrbatSelectionIntent intent)
        {
            if (intent != null)
                SelectUnit(intent.UnitId, intent.Additive);
        }

        private void OnOrbatFocus(int unitId)
        {
            FocusUnit(unitId);
        }

        private void OnHudImmediateOrder(BattleCommandIntent intent)
        {
            IssueImmediateOrder(intent);
        }

        private void OnHudFireMode(BattleFireAsset asset, bool smoke)
        {
            SetFireMode(asset, smoke);
        }

        private void OnDeploymentPlacement(IReadOnlyList<DeploymentPlacementIntent> placements)
        {
            PlaceDeployment(placements);
        }

        private void NotifyNotImplemented(string command)
        {
            Notify(command + " 명령은 이 게임에 아직 없습니다");
        }

        private void Notify(string message)
        {
            NotificationRequested?.Invoke(message);
        }

        private double NowSeconds()
        {
            return TimeSource == null ? Time.unscaledTimeAsDouble : TimeSource();
        }

        private void UpdateCameraInputBlockers()
        {
            if (_camera == null)
                return;
            Vector2 pointer = Mouse.current == null ? _pointerScreenPosition : Mouse.current.position.ReadValue();
            _camera.WasdInputBlocked = GuideOpen || UiHasKeyboardFocus();
            _camera.PointerInputBlocked = GuideOpen || IsPointerOverUi(pointer);
        }

        private bool UiHasKeyboardFocus()
        {
            if (UiKeyboardFocusProbe != null)
                return UiKeyboardFocusProbe();
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.currentSelectedGameObject != null;
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            if (PointerOverUiProbe != null)
                return PointerOverUiProbe(screenPosition);
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        private void UnbindHud()
        {
            if (_hud == null)
                return;
            _hud.OrderModeSelected -= SetOrderMode;
            _hud.ImmediateOrderRequested -= OnHudImmediateOrder;
            _hud.FormationSelected -= SetFormation;
            _hud.FireModeSelected -= OnHudFireMode;
            _hud.AltitudeToggleRequested -= ToggleAltitude;
            _hud.PauseToggleRequested -= TogglePause;
            _hud.SpeedChangeRequested -= ChangeSpeed;
            _hud = null;
        }

        private void OnDisable()
        {
            CancelPointerInteraction();
            if (_camera != null)
            {
                _camera.WasdInputBlocked = false;
                _camera.PointerInputBlocked = false;
            }
        }

        private void OnDestroy()
        {
            UnbindHud();
            BindOrbat(null);
            BindDeploymentPanel(null);
            BindMinimap(null);
            _symbols = null;
            _camera = null;
            _heightFieldPick = null;
        }

        private static int GroupNumber(Key key)
        {
            if (key >= Key.Digit1 && key <= Key.Digit9)
                return key - Key.Digit1 + 1;
            if (key >= Key.Numpad1 && key <= Key.Numpad9)
                return key - Key.Numpad1 + 1;
            return -1;
        }

        private static bool ValidGroup(int group)
        {
            return group >= 1 && group <= 9;
        }

        private static bool IsTargetOrder(BattleCommandType type)
        {
            return type == BattleCommandType.Move || type == BattleCommandType.FastMove
                || type == BattleCommandType.AttackMove || type == BattleCommandType.Seize
                || type == BattleCommandType.Defend || type == BattleCommandType.Withdraw
                || type == BattleCommandType.LayMines || type == BattleCommandType.ClearMines
                || type == BattleCommandType.Fortify;
        }

        private static OrderType ToOrderType(BattleCommandType type)
        {
            switch (type)
            {
                case BattleCommandType.Move: return OrderType.MOVE;
                case BattleCommandType.FastMove: return OrderType.FAST_MOVE;
                case BattleCommandType.AttackMove: return OrderType.ATTACK_MOVE;
                case BattleCommandType.Seize: return OrderType.SEIZE;
                case BattleCommandType.Defend: return OrderType.DEFEND;
                case BattleCommandType.Withdraw: return OrderType.WITHDRAW;
                case BattleCommandType.Resupply: return OrderType.RESUPPLY;
                case BattleCommandType.Mount: return OrderType.MOUNT;
                case BattleCommandType.Dismount: return OrderType.DISMOUNT;
                case BattleCommandType.SetFormation: return OrderType.SET_FORMATION;
                case BattleCommandType.SetSop: return OrderType.SET_SOP;
                case BattleCommandType.CallFire: return OrderType.CALL_FIRE;
                case BattleCommandType.Detach: return OrderType.DETACH;
                case BattleCommandType.Rejoin: return OrderType.REJOIN;
                case BattleCommandType.Reorganize: return OrderType.REORGANIZE;
                case BattleCommandType.Cancel: return OrderType.CANCEL;
                case BattleCommandType.Follow: return OrderType.FOLLOW;
                case BattleCommandType.Halt: return OrderType.HALT;
                case BattleCommandType.LayMines: return OrderType.LAY_MINES;
                case BattleCommandType.ClearMines: return OrderType.CLEAR_MINES;
                case BattleCommandType.Fortify: return OrderType.FORTIFY;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static JObject SopParameters(
            string technique = "",
            string rulesOfEngagement = "",
            string altitude = "")
        {
            var sop = new JObject();
            if (!string.IsNullOrEmpty(technique))
                sop["technique"] = technique;
            if (!string.IsNullOrEmpty(rulesOfEngagement))
                sop["roe"] = rulesOfEngagement;
            if (!string.IsNullOrEmpty(altitude))
                sop["altitude"] = altitude;
            return new JObject { ["sop"] = sop };
        }

        private static bool ControlHeld(Keyboard keyboard = null)
        {
            keyboard ??= Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private static bool ShiftHeld(Keyboard keyboard)
        {
            return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }

        private static bool AltHeld(Keyboard keyboard)
        {
            return keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
        }

        private static Vector2 ToVector2(SimVec2 value)
        {
            return new Vector2(value.X, value.Y);
        }

        private static SimVec2 ToSimVec2(Vector2 value)
        {
            return new SimVec2(value.x, value.y);
        }
    }
}
