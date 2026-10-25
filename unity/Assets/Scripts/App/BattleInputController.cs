using System;
using System.Collections.Generic;
using System.Globalization;
using Echelon.Core;
using Echelon.Unity.Presentation;
using Echelon.Unity.Presentation.Symbols;
using Echelon.Unity.UI.Battle;
using Echelon.Unity.UI.Minimap;
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

    /// <summary>
    /// 키보드·마우스와 HUD 이벤트를 BattleCommands 호출로 바꾼다. 지휘 상태는 들고 있지 않고,
    /// 더블클릭·연타 판정과 우클릭 드래그(도착 방향) 같은 입력 상태만 가진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleInputController : MonoBehaviour
    {
        public const double DoubleClickSeconds = 0.35;
        public const float DragThresholdPixels = 18f;
        public const float FacingDragMinimumMeters = 10f;

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

        private BattleCommands _commands;
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

        public event Action GuideRequested;
        public event Action<bool> FogVisibilityChanged;
        public event Action<bool> DebugOverlayVisibilityChanged;
        public event Action BattleStartRequested;

        public BattleCommands Commands => _commands;
        public Battle Battle => _commands?.Battle;
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

        public void Configure(BattleCommands commands, HeightFieldPickDelegate heightFieldPick = null)
        {
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));
            if (!ReferenceEquals(_commands, commands))
            {
                CancelPointerInteraction();
                UnsubscribeCommands();
                _commands = commands;
                _commands.SelectionChanged += OnSelectionChanged;
                _commands.OrderModeChanged += OnOrderModeChanged;
                _commands.FireModeChanged += OnFireModeChanged;
                _lastGroup = -1;
                _lastGroupTime = double.NegativeInfinity;
                _lastClickTime = double.NegativeInfinity;
                _lastClickedUnitId = -1;
            }
            _heightFieldPick = heightFieldPick;
            TimeSource ??= () => Time.unscaledTimeAsDouble;
        }

        public void Configure(BattleCommands commands, UnityEngine.Camera camera, MapBounds bounds, HeightSampler sampleHeight)
        {
            Configure(commands);
            SetHeightFieldPicker(camera, bounds, sampleHeight);
        }

        public void SetHeightFieldPicker(UnityEngine.Camera camera, MapBounds bounds, HeightSampler sampleHeight)
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
            _hud.OrderModeSelected += OnHudOrderMode;
            _hud.ImmediateOrderRequested += OnHudImmediateOrder;
            _hud.FormationSelected += OnHudFormation;
            _hud.FireModeSelected += OnHudFireMode;
            _hud.AltitudeToggleRequested += OnHudAltitude;
            _hud.PauseToggleRequested += OnHudPause;
            _hud.SpeedChangeRequested += OnHudSpeed;
            _hud.GuideRequested += RequestGuideToggle;
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
                _minimap.NavigationRequested -= OnMinimapNavigation;
            _minimap = minimap;
            if (_minimap != null)
                _minimap.NavigationRequested += OnMinimapNavigation;
        }

        public void BindCamera(RtsCameraController camera)
        {
            _camera = camera;
            UpdateCameraInputBlockers();
        }

        public void Update()
        {
            UpdateCameraInputBlockers();
            if (_commands == null)
                return;
            _commands.PruneSelection();

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
            if (_commands == null)
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
                    _commands.SelectFormation(group - 1);
                else if (control)
                    _commands.AssignGroup(group);
                else
                    RecallGroup(group, shift);
                return true;
            }

            switch (key)
            {
                case Key.Space: _commands.TogglePause(); return true;
                case Key.Equals:
                case Key.NumpadPlus: _commands.ChangeSpeed(1); return true;
                case Key.Minus:
                case Key.NumpadMinus: _commands.ChangeSpeed(-1); return true;
                case Key.Slash: RequestGuideToggle(); return true;
                case Key.F9: SetFogEnabled(!FogEnabled); return true;
                case Key.F10: SetDebugOverlayVisible(!DebugOverlayVisible); return true;
                case Key.M: _commands.SetOrderMode(BattleCommandType.Move); return true;
                case Key.F: _commands.SetOrderMode(BattleCommandType.FastMove); return true;
                case Key.Q: _commands.SetOrderMode(BattleCommandType.AttackMove); return true;
                case Key.Y: _commands.SetOrderMode(BattleCommandType.Seize); return true;
                case Key.J: _commands.SetOrderMode(BattleCommandType.Defend); return true;
                case Key.B:
                    if (_commands.SelectionIsAirWing())
                        _commands.ReturnToBase();
                    else
                        _commands.SetOrderMode(BattleCommandType.Withdraw);
                    return true;
                case Key.N: _commands.SetOrderMode(BattleCommandType.LayMines); return true;
                case Key.I: _commands.SetOrderMode(BattleCommandType.ClearMines); return true;
                case Key.O: _commands.SetOrderMode(BattleCommandType.Fortify); return true;
                case Key.E: _commands.Halt(); return true;
                case Key.H: _commands.ToggleHoldFire(); return true;
                case Key.U: _commands.ToggleMount(); return true;
                case Key.L: _commands.CycleTechnique(); return true;
                case Key.K: _commands.IssueImmediateOrders(BattleCommandType.Resupply); return true;
                case Key.T: _commands.CycleFireMode(false); return true;
                case Key.G: _commands.CycleFireMode(true); return true;
                case Key.P: _commands.SetFireMode(BattleFireAsset.Cas); return true;
                case Key.V: _commands.ToggleAltitude(); return true;
                case Key.Tab: _commands.NavigateSelection(!shift); return true;
                case Key.F1: _commands.SelectCompany(0); return true;
                case Key.F2: _commands.SelectCompany(1); return true;
                case Key.F3: _commands.SelectCompany(2); return true;
                case Key.F4: _commands.SelectCompany(3); return true;
                case Key.R:
                    if (_commands.SelectionCarriesCargo())
                        _commands.SetOrderMode(BattleCommandType.Airdrop);
                    else
                        _commands.NotifyNotImplemented("후진");
                    return true;
                case Key.Z: _commands.NotifyNotImplemented("질주"); return true;
                case Key.X: _commands.NotifyNotImplemented("연막탄 발사"); return true;
                case Key.C: _commands.NotifyNotImplemented("기만체"); return true;
                case Key.Escape:
                    if (_commands.FireAsset.HasValue)
                        _commands.ClearFireMode();
                    else if (_rightDragActive)
                        CancelPointerInteraction();
                    else
                        _commands.ClearSelection();
                    return true;
                default:
                    return false;
            }
        }

        public bool PointerDown(Vector2 screenPosition, BattlePointerButton button, bool control = false, bool shift = false)
        {
            _pointerScreenPosition = screenPosition;
            if (_commands == null || GuideOpen || IsPointerOverUi(screenPosition))
                return false;

            bool fireMode = _commands.FireAsset.HasValue;
            if (button == BattlePointerButton.Left && fireMode && !_commands.Battle.Deploying)
            {
                if (TryPickTerrain(screenPosition, out Vector2 fireTarget))
                    _commands.RequestFireSupport(fireTarget);
                return true;
            }
            if (button == BattlePointerButton.Right && fireMode)
            {
                _commands.ClearFireMode();
                return true;
            }
            if (button == BattlePointerButton.Left)
            {
                int unitId = -1;
                if (_symbols != null)
                    int.TryParse(_symbols.PickUnit(screenPosition, _commands.Battle.PlayerSide),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId);
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

        public bool PointerUp(Vector2 screenPosition, BattlePointerButton button, bool control = false, bool shift = false)
        {
            _pointerScreenPosition = screenPosition;
            if (button != BattlePointerButton.Right || !_rightDragActive || _commands == null)
                return false;

            double? facing = null;
            if (HasDirectionPreview && Vector2.Distance(DirectionDragTo, _rightClickWorld) > FacingDragMinimumMeters)
            {
                Vector2 direction = DirectionDragTo - _rightClickWorld;
                facing = Math.Atan2(direction.x, direction.y);
            }

            var target = new SimVec2(_rightClickWorld.x, _rightClickWorld.y);
            if (_commands.Battle.Deploying)
                _commands.DeploySelection(target, facing);
            else
                _commands.IssueMovementOrders(target, facing, shift);
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
                _symbols?.SetDirectionDrag(DirectionDragFrom, DirectionDragTo);
            }
            UpdateCursorPreview(screenPosition);
        }

        public void CancelPointerInteraction()
        {
            _rightDragActive = false;
            HasDirectionPreview = false;
            _symbols?.ClearDirectionDrag();
        }

        public void OnSymbolClicked(SymbolPickResult pick)
        {
            if (int.TryParse(pick.UnitId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int unitId))
                ClickUnit(unitId, ControlHeld());
        }

        /// <summary>좌클릭 선택. 같은 소대를 빠르게 두 번 누르면 소속 중대를 고른다.</summary>
        public void ClickUnit(int unitId, bool control = false)
        {
            Unit unit = _commands.SelectableUnit(unitId);
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
                _commands.ClearSelection();
            else if (control)
                _commands.ToggleSelected(unit);
            else
                _commands.SelectUnits(new[] { unit });
        }

        /// <summary>1~9: 지정 부대 불러오기. 같은 번호를 빠르게 두 번 누르면 카메라를 옮긴다.</summary>
        public void RecallGroup(int group, bool additive = false)
        {
            IReadOnlyList<Unit> units = _commands.RecallGroup(group, additive);
            if (units.Count == 0)
                return;
            double now = NowSeconds();
            bool focus = group == _lastGroup && now - _lastGroupTime < DoubleClickSeconds;
            _lastGroup = group;
            _lastGroupTime = now;
            if (focus)
                _commands.FocusUnit(units[0].Id);
        }

        public void UpdateCursorPreview(Vector2 screenPosition)
        {
            _pointerScreenPosition = screenPosition;
            string text = string.Empty;
            bool force = false;
            if (_commands != null)
            {
                Vector2? target = null;
                if (_commands.FireAsset.HasValue && TryPickTerrain(screenPosition, out Vector2 picked))
                    target = picked;
                text = _commands.CursorPreview(target, out force);
            }
            CursorPreviewText = text;
            CursorPreviewForced = force;
            _symbols?.SetCursorPreview(text, screenPosition, force);
        }

        public void RequestBattleStart()
        {
            BattleStartRequested?.Invoke();
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
        }

        public void SetDebugOverlayVisible(bool visible)
        {
            if (DebugOverlayVisible == visible)
                return;
            DebugOverlayVisible = visible;
            DebugOverlayVisibilityChanged?.Invoke(visible);
        }

        // 지휘 상태가 바뀌면 커서 설명(실행까지 남은 시간 등)도 바뀐다.
        private void OnSelectionChanged(IReadOnlyList<Unit> units) => UpdateCursorPreview(_pointerScreenPosition);
        private void OnOrderModeChanged(BattleCommandType type) => UpdateCursorPreview(_pointerScreenPosition);
        private void OnFireModeChanged(BattleFireAsset? asset, bool smoke) => UpdateCursorPreview(_pointerScreenPosition);

        private void UnsubscribeCommands()
        {
            if (_commands == null)
                return;
            _commands.SelectionChanged -= OnSelectionChanged;
            _commands.OrderModeChanged -= OnOrderModeChanged;
            _commands.FireModeChanged -= OnFireModeChanged;
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

        private void OnHudOrderMode(BattleCommandType type) => _commands?.SetOrderMode(type);
        private void OnHudImmediateOrder(BattleCommandIntent intent) => _commands?.IssueImmediateOrder(intent);
        private void OnHudFormation(string formationId) => _commands?.SetFormation(formationId);
        private void OnHudFireMode(BattleFireAsset asset, bool smoke) => _commands?.SetFireMode(asset, smoke);
        private void OnHudAltitude() => _commands?.ToggleAltitude();
        private void OnHudPause() => _commands?.TogglePause();
        private void OnHudSpeed(int direction) => _commands?.ChangeSpeed(direction);

        private void OnOrbatSelection(OrbatSelectionIntent intent)
        {
            if (intent != null)
                _commands?.SelectUnit(intent.UnitId, intent.Additive);
        }

        private void OnOrbatFocus(int unitId) => _commands?.FocusUnit(unitId);
        private void OnMinimapNavigation(Vector2 simPosition) => _commands?.RequestFocus(simPosition);
        private void OnDeploymentPlacement(IReadOnlyList<DeploymentPlacementIntent> placements) => _commands?.PlaceDeployment(placements);

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
            _hud.OrderModeSelected -= OnHudOrderMode;
            _hud.ImmediateOrderRequested -= OnHudImmediateOrder;
            _hud.FormationSelected -= OnHudFormation;
            _hud.FireModeSelected -= OnHudFireMode;
            _hud.AltitudeToggleRequested -= OnHudAltitude;
            _hud.PauseToggleRequested -= OnHudPause;
            _hud.SpeedChangeRequested -= OnHudSpeed;
            _hud.GuideRequested -= RequestGuideToggle;
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
            UnsubscribeCommands();
            _commands = null;
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
    }
}
