using System;
using System.Collections.Generic;
using System.Globalization;
using Echelon.Core;
using Echelon.Unity.UI.Battle;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Echelon.Unity.App
{
    /// <summary>
    /// 플레이어 지휘 상태(선택·부대 지정·명령 모드·화력 모드)와 명령 발행을 한곳에서 맡는다.
    /// 입력 장치·HUD·튜토리얼은 모두 이 클래스를 거쳐 명령을 낸다.
    /// </summary>
    public sealed class BattleCommands
    {
        public const float MoveSpacingMeters = 250f;

        private static readonly string[] ObserverNames =
        {
            "관측반 관측", "일반 엘리먼트 관측", "관측 없음, 지도 좌표 사격"
        };

        private static readonly string[] Techniques =
        {
            BattleSop.Traveling, BattleSop.Overwatch, BattleSop.Bounding
        };

        private readonly List<Unit> _selection = new List<Unit>();
        private readonly List<int> _selectedUnitIds = new List<int>();
        private readonly Dictionary<int, List<Unit>> _groups = new Dictionary<int, List<Unit>>();

        public BattleCommands(Battle battle)
        {
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public event Action<IReadOnlyList<Unit>> SelectionChanged;
        public event Action<Order> OrderIssued;
        public event Action<BattleCommandType> OrderModeChanged;
        public event Action<BattleFireAsset?, bool> FireModeChanged;
        public event Action<FireSupportSystem.Mission> FireMissionRequested;
        public event Action<string> Notification;
        public event Action<Vector2> FocusRequested;

        public Battle Battle { get; }
        public IReadOnlyList<Unit> Selection => _selection;
        public IReadOnlyList<int> SelectedUnitIds => _selectedUnitIds;
        public BattleCommandType OrderMode { get; private set; } = BattleCommandType.Move;
        public BattleFireAsset? FireAsset { get; private set; }
        public bool FireSmoke { get; private set; }

        public static OrderType ToOrderType(BattleCommandType type)
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
                case BattleCommandType.Airdrop: return OrderType.AIRDROP;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static bool IsTargetOrder(BattleCommandType type)
        {
            return type == BattleCommandType.Move || type == BattleCommandType.FastMove
                || type == BattleCommandType.AttackMove || type == BattleCommandType.Seize
                || type == BattleCommandType.Defend || type == BattleCommandType.Withdraw
                || type == BattleCommandType.LayMines || type == BattleCommandType.ClearMines
                || type == BattleCommandType.Fortify || type == BattleCommandType.Airdrop;
        }

        // ── 선택 ────────────────────────────────────────────────

        public bool IsSelectable(Unit unit)
        {
            return unit != null && unit.Side == Battle.PlayerSide
                && unit.Level != UnitLevel.TaskForce && !unit.IsDestroyed();
        }

        public Unit SelectableUnit(int unitId)
        {
            Unit unit = Battle.GetUnit(unitId);
            return IsSelectable(unit) ? unit : null;
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
                SelectUnits(new[] { unit });
                return;
            }
            var units = new List<Unit>(_selection);
            if (!units.Contains(unit))
                units.Add(unit);
            SelectUnits(units);
        }

        /// <summary>Ctrl+클릭: 이미 선택돼 있으면 빼고, 아니면 더한다.</summary>
        public void ToggleSelected(Unit unit)
        {
            if (!IsSelectable(unit))
                return;
            var changed = new List<Unit>(_selection);
            if (!changed.Remove(unit))
                changed.Add(unit);
            SelectUnits(changed);
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
            SelectUnits(units);
        }

        public void ClearSelection()
        {
            SelectUnits(null);
        }

        public void PruneSelection()
        {
            SelectUnits(_selection);
        }

        public void SelectUnits(IEnumerable<Unit> units)
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
            if (SameUnits(result, _selection))
                return;
            _selection.Clear();
            _selection.AddRange(result);
            _selectedUnitIds.Clear();
            foreach (Unit unit in _selection)
                _selectedUnitIds.Add(unit.Id);
            SelectionChanged?.Invoke(_selection);
        }

        /// <summary>Tab: 하위 제대로 내려가거나(down) 상위 제대로 올라간다.</summary>
        public void NavigateSelection(bool down)
        {
            if (_selection.Count == 0)
                return;
            var result = new List<Unit>();
            foreach (Unit unit in _selection.ToArray())
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
                SelectUnits(result);
        }

        public void SelectCompany(int index, bool focus = true)
        {
            TaskForce taskForce = Battle.GetTaskForce(Battle.PlayerSide);
            if (taskForce == null || index < 0 || index >= taskForce.Companies.Count)
                return;
            Company company = taskForce.Companies[index];
            SelectUnits(new[] { company });
            if (focus)
                RequestFocus(company);
        }

        public bool FocusUnit(int unitId)
        {
            Unit unit = Battle.GetUnit(unitId);
            if (unit == null)
                return false;
            RequestFocus(unit);
            return true;
        }

        public void RequestFocus(Vector2 simPosition)
        {
            FocusRequested?.Invoke(simPosition);
        }

        // ── 부대 지정 ────────────────────────────────────────────

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

        /// <summary>지정된 부대를 선택한다. 남은 부대가 없으면 빈 목록.</summary>
        public IReadOnlyList<Unit> RecallGroup(int group, bool additive = false)
        {
            if (!ValidGroup(group))
                return Array.Empty<Unit>();
            IReadOnlyList<Unit> units = GetGroupUnits(group);
            if (units.Count == 0)
                return units;
            if (additive)
            {
                var merged = new List<Unit>(_selection);
                foreach (Unit unit in units)
                {
                    if (!merged.Contains(unit))
                        merged.Add(unit);
                }
                SelectUnits(merged);
            }
            else
            {
                SelectUnits(units);
            }
            return units;
        }

        // ── 명령 ────────────────────────────────────────────────

        public void SetOrderMode(BattleCommandType type)
        {
            if (!IsTargetOrder(type) || OrderMode == type)
                return;
            OrderMode = type;
            OrderModeChanged?.Invoke(type);
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

        /// <summary>
        /// 선택한 제대마다 현재 명령 모드로 목표 지점 명령을 낸다. 여러 제대는 도착 방향의 오른쪽으로
        /// MoveSpacingMeters 간격을 두고 나란히 선다. 명령을 냈으면 명령 모드는 이동으로 돌아간다.
        /// </summary>
        public IReadOnlyList<Order> IssueMovementOrders(SimVec2 target, double? facing = null, bool append = false)
        {
            var orders = new List<Order>();
            if (Battle.Deploying || _selection.Count == 0)
                return orders;
            bool hasFacing = facing.HasValue && !double.IsNaN(facing.Value);
            Unit[] selected = _selection.ToArray();
            for (int i = 0; i < selected.Length; i++)
            {
                Unit unit = selected[i];
                double heading = hasFacing ? facing.Value : FormationSolver.HeadingFromVector(target - unit.Guide.Pos);
                double offset = (i - (selected.Length - 1) * 0.5) * MoveSpacingMeters;
                var parameters = new JObject();
                if (append)
                    parameters["append"] = true;
                Order order = Order.Make(ToOrderType(OrderMode), unit.Id, parameters);
                order.Bypass = unit.Level == UnitLevel.Platoon && unit.FollowsParent();
                order.SetPos(Battle.Terrain.ClampToMap(target + FormationSolver.RightVector(heading) * offset));
                if (hasFacing)
                    order.SetFacing(facing.Value);
                orders.Add(Issue(order));
            }
            if (orders.Count > 0)
                SetOrderMode(BattleCommandType.Move);
            return orders;
        }

        public IReadOnlyList<Order> IssueMovementOrders(Vector2 target, float? facing = null, bool append = false)
        {
            return IssueMovementOrders(ToSim(target), facing.HasValue ? (double?)facing.Value : null, append);
        }

        public IReadOnlyList<Order> IssueImmediateOrders(BattleCommandType type, JObject parameters = null)
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

        public void Halt()
        {
            IssueImmediateOrders(BattleCommandType.Halt);
            SetOrderMode(BattleCommandType.Move);
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

        /// <summary>선택한 제대가 모두 고정익 편대·항공대면 true. B 를 누르면 지점 없이 곧장 귀환시킨다.</summary>
        public bool SelectionIsAirWing()
        {
            if (_selection.Count == 0)
                return false;
            foreach (Unit unit in _selection)
            {
                if (unit.Kind != "fixed_wing")
                    return false;
            }
            return true;
        }

        /// <summary>선택 안에 보급품을 실은 수송기 편대가 있으면 true(R 보급 투하).</summary>
        public bool SelectionCarriesCargo()
        {
            foreach (Unit unit in _selection)
            {
                var flights = new List<Unit> { unit };
                flights.AddRange(unit.ChildrenUnits());
                foreach (Unit child in flights)
                {
                    if (child is Platoon platoon && platoon.IsAirWing && AirOpsSystem.CarriesSupply(platoon))
                        return true;
                }
            }
            return false;
        }

        public void ReturnToBase()
        {
            IssueImmediateOrders(BattleCommandType.Withdraw);
            SetOrderMode(BattleCommandType.Move);
        }

        public void NotifyNotImplemented(string command)
        {
            Notify(command + " 명령은 이 게임에 아직 없습니다");
        }

        // ── 화력 지원 ────────────────────────────────────────────

        public void SetFireMode(BattleFireAsset? asset, bool smoke = false)
        {
            BattleFireAsset? previous = FireAsset;
            bool previousSmoke = FireSmoke;
            FireAsset = asset;
            FireSmoke = asset.HasValue && asset.Value != BattleFireAsset.Cas && smoke;
            if (previous != FireAsset || previousSmoke != FireSmoke)
                FireModeChanged?.Invoke(FireAsset, FireSmoke);
        }

        /// <summary>T·G: 박격포 → 포병 → 해제 순서로 돈다.</summary>
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
            if (Battle.Deploying || !FireAsset.HasValue)
                return null;
            BattleFireAsset asset = FireAsset.Value;
            Dictionary<string, object> result = Battle.Fires.Request(
                Battle.PlayerSide, (FireSupportSystem.Asset)(int)asset, target, FireSmoke);
            FireSupportSystem.Mission mission = null;
            if (result.TryGetValue("ok", out object value) && value is bool ok && ok)
            {
                mission = result.TryGetValue("mission", out object raw) ? raw as FireSupportSystem.Mission : null;
                if (mission != null)
                {
                    FireMissionRequested?.Invoke(mission);
                    Notify(string.Format(CultureInfo.InvariantCulture, "{0} 요청 — 약 {1:0}초 뒤 초탄 ({2})",
                        mission.Label(), mission.DelaySec, ObserverName(mission.ObserverQuality)));
                }
            }
            else
            {
                string error = result.TryGetValue("error", out object rawError)
                    ? Convert.ToString(rawError, CultureInfo.InvariantCulture)
                    : string.Empty;
                Notify(string.Format(CultureInfo.InvariantCulture, "{0} 요청 불가: {1}",
                    BattleUiText.FireAssetName(asset), error));
            }
            ClearFireMode();
            return mission;
        }

        public FireSupportSystem.Mission RequestFireSupport(Vector2 target)
        {
            return RequestFireSupport(ToSim(target));
        }

        // ── 커서 미리보기 ────────────────────────────────────────

        /// <summary>
        /// 커서 옆에 띄울 설명. 화력 모드에서는 표적 지점(terrainTarget)이 필요하고, 그때 force 가 참이다
        /// (부호 위에서도 계속 보여야 한다).
        /// </summary>
        public string CursorPreview(Vector2? terrainTarget, out bool force)
        {
            force = false;
            if (Battle.Deploying)
                return _selection.Count > 0 ? "배치: 우클릭으로 옮기기" : string.Empty;
            if (FireAsset.HasValue)
            {
                force = true;
                return terrainTarget.HasValue ? FirePreview(FireAsset.Value, FireSmoke, ToSim(terrainTarget.Value)) : string.Empty;
            }
            if (_selection.Count == 0 || Battle.OrderSystem == null)
                return string.Empty;
            Unit unit = _selection[0];
            bool bypass = unit.Level == UnitLevel.Platoon && unit.FollowsParent();
            double seconds = Battle.OrderSystem.EstimateDelaySec(unit, ToOrderType(OrderMode), bypass);
            string label = BattleUiText.OrderName(OrderMode);
            return seconds < 0.0
                ? label + " — 통신 두절, 명령 대기"
                : string.Format(CultureInfo.InvariantCulture, "{0} — 실행까지 약 {1:0}초{2}",
                    label, seconds, bypass ? "  (직접 명령: 분리, 용량 2)" : string.Empty);
        }

        // ── 배치 ────────────────────────────────────────────────

        public int DeploySelection(SimVec2 target, double? facing = null)
        {
            if (!Battle.Deploying || _selection.Count == 0)
                return 0;
            var states = new List<DeploymentUnitState>(_selection.Count);
            foreach (Unit unit in _selection)
                states.Add(new DeploymentUnitState(unit.Id, (float)unit.Guide.Heading));
            float? heading = facing.HasValue && !double.IsNaN(facing.Value) ? (float?)facing.Value : null;
            return PlaceDeployment(DeploymentPlacement.CreateIntents(states, ToVector(target), heading));
        }

        public int DeploySelection(Vector2 target, float? facing = null)
        {
            return DeploySelection(ToSim(target), facing.HasValue ? (double?)facing.Value : null);
        }

        public int PlaceDeployment(IReadOnlyList<DeploymentPlacementIntent> placements)
        {
            if (!Battle.Deploying || placements == null)
                return 0;
            int placed = 0;
            foreach (DeploymentPlacementIntent placement in placements)
            {
                if (placement == null)
                    continue;
                Unit unit = Battle.GetUnit(placement.UnitId);
                if (unit != null && Battle.DeployUnit(unit, ToSim(placement.Position), placement.HeadingRadians))
                    placed++;
            }
            return placed;
        }

        // ── 시간 ────────────────────────────────────────────────

        public void TogglePause()
        {
            if (!Battle.Deploying && !Battle.HasResult)
                Battle.World.TogglePause();
        }

        public void ChangeSpeed(int direction)
        {
            if (direction > 0)
                Battle.World.SpeedUp();
            else if (direction < 0)
                Battle.World.SpeedDown();
        }

        // ── 내부 ────────────────────────────────────────────────

        private IReadOnlyList<Order> IssueToUnits(IEnumerable<Unit> units, OrderType type, JObject parameters)
        {
            var orders = new List<Order>();
            foreach (Unit unit in units)
            {
                if (!IsSelectable(unit))
                    continue;
                orders.Add(Issue(Order.Make(type, unit.Id,
                    parameters == null ? new JObject() : (JObject)parameters.DeepClone())));
            }
            return orders;
        }

        private Order Issue(Order order)
        {
            order.Issuer = "player";
            Battle.IssueOrder(order);
            OrderIssued?.Invoke(order);
            return order;
        }

        private string FirePreview(BattleFireAsset asset, bool smoke, SimVec2 target)
        {
            Dictionary<string, object> preview = Battle.Fires.Preview(
                Battle.PlayerSide, (FireSupportSystem.Asset)(int)asset, target);
            string name = BattleUiText.FireAssetName(asset) + (smoke ? " 연막" : string.Empty);
            if (preview.TryGetValue("error", out object error))
                return name + " — " + Convert.ToString(error, CultureInfo.InvariantCulture);
            int quality = preview.TryGetValue("quality", out object rawQuality)
                ? Convert.ToInt32(rawQuality, CultureInfo.InvariantCulture)
                : ObserverNames.Length - 1;
            double seconds = preview.TryGetValue("seconds", out object rawSeconds)
                ? Convert.ToDouble(rawSeconds, CultureInfo.InvariantCulture)
                : 0.0;
            return string.Format(CultureInfo.InvariantCulture, "{0} — {1}, 약 {2:0}초",
                name, ObserverName(quality), seconds);
        }

        private void RequestFocus(Unit unit)
        {
            RequestFocus(ToVector(unit.Centroid()));
        }

        private void Notify(string message)
        {
            Notification?.Invoke(message);
        }

        private static string ObserverName(int quality)
        {
            return ObserverNames[Mathf.Clamp(quality, 0, ObserverNames.Length - 1)];
        }

        private static bool ValidGroup(int group)
        {
            return group >= 1 && group <= 9;
        }

        private static bool SameUnits(List<Unit> a, List<Unit> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                    return false;
            }
            return true;
        }

        private static JObject SopParameters(string technique = "", string rulesOfEngagement = "", string altitude = "")
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

        private static Vector2 ToVector(SimVec2 value)
        {
            return new Vector2(value.X, value.Y);
        }

        private static SimVec2 ToSim(Vector2 value)
        {
            return new SimVec2(value.x, value.y);
        }
    }
}
