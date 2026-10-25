using System;
using System.Collections.Generic;
using System.Globalization;
using Echelon.Unity.UI.Common;
using UnityEngine;

namespace Echelon.Unity.UI.Battle
{
    public static class BattleFormationCatalog
    {
        public static readonly string[] PlatoonIds =
        {
            "column", "wedge", "line", "echelon_left", "echelon_right", "vee", "coil"
        };

        public static readonly string[] CompanyIds =
        {
            "co_column", "co_wedge", "co_line", "co_echelon_left", "co_echelon_right", "co_vee", "co_box"
        };

        public static IReadOnlyList<string> IdsFor(BattleUnitLevel level)
        {
            return level == BattleUnitLevel.Company ? CompanyIds : PlatoonIds;
        }

        public static IReadOnlyList<BattleFormationOption> DefaultsFor(BattleUnitLevel level)
        {
            string[] ids = level == BattleUnitLevel.Company ? CompanyIds : PlatoonIds;
            var result = new List<BattleFormationOption>(ids.Length);
            foreach (string id in ids)
                result.Add(new BattleFormationOption(id, DisplayName(id)));
            return result;
        }

        public static string DisplayName(string id)
        {
            switch (id)
            {
                case "column": return "종대";
                case "wedge": return "쐐기";
                case "line": return "횡대";
                case "echelon_left": return "제대 좌";
                case "echelon_right": return "제대 우";
                case "vee": return "V";
                case "coil": return "사주 경계";
                case "co_column": return "소대 종대";
                case "co_wedge": return "쐐기 (1 up 2 back)";
                case "co_line": return "소대 횡대";
                case "co_echelon_left": return "제대 좌";
                case "co_echelon_right": return "제대 우";
                case "co_vee": return "V (2 up 1 back)";
                case "co_box": return "박스 / 다이아몬드";
                default: return id ?? string.Empty;
            }
        }
    }

    public static class BattleUiText
    {
        public const string NoSelectionTitle = "선택한 부대 없음";

        public const string NoSelectionHint =
            "좌클릭 선택 · 우클릭 명령(드래그: 도착 방향) · Shift 대기열\n" +
            "F1~F4 중대 · Tab 하위 제대 · Ctrl+1~9 부대 지정 · / 가이드";

        public static string Speed(float speed)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.#}×", speed);
        }

        /// <summary>상단 바 상태 꼬리표: 배치 단계가 일시정지보다 앞선다.</summary>
        public static string Phase(BattleHudState state)
        {
            if (state == null)
                return string.Empty;
            if (state.Deploying)
                return "배치 단계";
            return state.Paused ? "일시정지" : string.Empty;
        }

        public static string Remaining(BattleHudState state)
        {
            int remaining = Mathf.Max(0, Mathf.RoundToInt(state.TimeLimitSeconds - state.SimulationSeconds));
            return Clock(remaining);
        }

        public static string Percent(float ratio)
        {
            return Mathf.RoundToInt(ratio * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>지휘 용량 "사용 / 최대 (대기 n)". 용량 정보가 없으면 빈 문자열.</summary>
        public static string Capacity(BattleHudState state)
        {
            if (state == null || !state.CommandCapacityUsed.HasValue || !state.CommandCapacityMaximum.HasValue)
                return string.Empty;
            string capacity = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.0} / {1:0}",
                state.CommandCapacityUsed.Value,
                state.CommandCapacityMaximum.Value);
            if (state.CommandCapacityQueue > 0)
                capacity += string.Format(CultureInfo.InvariantCulture, "  (대기 {0})", state.CommandCapacityQueue);
            return capacity;
        }

        public static string SelectionTitle(IReadOnlyList<BattleUnitView> selection)
        {
            if (selection == null || selection.Count == 0 || selection[0] == null)
                return NoSelectionTitle;
            return selection.Count > 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}  외 {1}", selection[0].DisplayName, selection.Count - 1)
                : selection[0].DisplayName;
        }

        /// <summary>대형 · 이동 방식 · 사격 통제.</summary>
        public static string UnitSetup(BattleUnitView unit)
        {
            string formation = !string.IsNullOrEmpty(unit.FormationName)
                ? unit.FormationName
                : !string.IsNullOrEmpty(unit.FormationId)
                    ? BattleFormationCatalog.DisplayName(unit.FormationId)
                    : "-";
            return formation + " · " + TechniqueName(unit.Technique) + " · 사격 " +
                   (unit.RulesOfEngagement == BattleSop.HoldFire ? "중지" : "자유");
        }

        /// <summary>자세와 현재 임무.</summary>
        public static string UnitActivity(BattleUnitView unit)
        {
            return PostureName(unit.Posture) + " · 임무 " +
                   (unit.MissionType.HasValue ? OrderName(unit.MissionType.Value) : "없음");
        }

        /// <summary>부대 카드 아래쪽에 띄우는 상태 꼬리표(글자, 색).</summary>
        public static List<(string Text, Color Color)> UnitTags(BattleUnitView unit)
        {
            var tags = new List<(string, Color)>
            {
                ("통신 " + CommsName(unit.Comms), CommsColor(unit.Comms))
            };
            if (unit.Detached)
                tags.Add(("분리됨", UiTheme.Warning));
            if (unit.Leaderless)
                tags.Add(("지휘 공백", UiTheme.Error));
            if (unit.Reforming)
                tags.Add(("대형 전환 중", UiTheme.Friendly));
            if (unit.Level == BattleUnitLevel.Platoon && unit.HasSquads)
                tags.Add(("보병 " + (unit.Mounted ? "탑승" : "하차"), UiTheme.TextDim));
            if (unit.Level == BattleUnitLevel.Platoon && unit.Kind == "engineer")
            {
                tags.Add((string.Format(CultureInfo.InvariantCulture, "지뢰 {0} · 진지 {1}/{2}",
                    unit.MinesLeft, unit.PositionsBuilt, unit.PositionsCapacity), UiTheme.TextDim));
            }
            if (unit.Kind == "aviation")
                tags.Add((unit.Altitude == BattleSop.HighAltitude ? "고공" : "저공", UiTheme.Friendly));
            if (!string.IsNullOrEmpty(unit.AirStatus))
                tags.Add((unit.AirStatus, UiTheme.Friendly));
            return tags;
        }

        public static string FireSupport(BattleFireSupportState state)
        {
            if (state == null)
                return string.Empty;
            var lines = new List<string>
            {
                state.ArtilleryBatteries > 0
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "포병 중대 포대 {0}개 · 지도 밖 임무 {1}   항공 출격 {2}",
                        state.ArtilleryBatteries,
                        state.ArtilleryMissions,
                        state.CasSorties)
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "남은 포병 임무 {0}   항공 출격 {1}",
                        state.ArtilleryMissions,
                        state.CasSorties)
            };
            if (state.ActiveMissions == null)
                return lines[0];

            foreach (BattleFireMissionView mission in state.ActiveMissions)
            {
                if (mission == null || mission.Phase == BattleFirePhase.Done || mission.Phase == BattleFirePhase.Cancelled)
                    continue;
                string phase = FirePhaseName(mission.Phase);
                if (mission.Phase == BattleFirePhase.Processing)
                {
                    phase += string.Format(
                        CultureInfo.InvariantCulture,
                        " {0:0}초",
                        Mathf.Max(0f, mission.SecondsRemaining));
                }
                else if (mission.Phase == BattleFirePhase.Effect)
                {
                    phase += string.Format(CultureInfo.InvariantCulture, " (남은 {0}발)", mission.RoundsLeft);
                }
                string label = !string.IsNullOrEmpty(mission.Label)
                    ? mission.Label
                    : FireAssetName(mission.Asset) + (mission.Smoke ? " 연막" : string.Empty);
                lines.Add("• " + Escape(label) + " — " + phase);
            }
            return string.Join("\n", lines);
        }

        public static string OrbatStatus(BattleUnitView unit)
        {
            if (unit == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(unit.AirStatus) && !unit.Destroyed)
                return unit.AirStatus;
            if (!unit.OnMap)
                return "증원 대기";
            if (unit.Destroyed && unit.Level != BattleUnitLevel.TaskForce)
                return "전멸";
            if (unit.Leaderless)
                return "지휘 공백";
            if (unit.Routed)
                return "패주";
            if (unit.AwaitingFollow)
                return "명령 대기";
            if (unit.Detached)
                return "분리";
            if (unit.OrderPending)
                return "명령 전파 중";
            if (unit.MissionType.HasValue)
                return OrderName(unit.MissionType.Value);
            return unit.Posture != "idle" ? PostureName(unit.Posture) : string.Empty;
        }

        public static string OrderName(BattleCommandType type)
        {
            switch (type)
            {
                case BattleCommandType.Move: return "이동";
                case BattleCommandType.FastMove: return "급속 이동";
                case BattleCommandType.AttackMove: return "공격 이동";
                case BattleCommandType.Seize: return "확보";
                case BattleCommandType.Defend: return "방어";
                case BattleCommandType.Withdraw: return "철수";
                case BattleCommandType.Resupply: return "보급";
                case BattleCommandType.Mount: return "탑승";
                case BattleCommandType.Dismount: return "하차";
                case BattleCommandType.SetFormation: return "대형 변경";
                case BattleCommandType.SetSop: return "SOP 변경";
                case BattleCommandType.CallFire: return "화력 요청";
                case BattleCommandType.Detach: return "분리";
                case BattleCommandType.Rejoin: return "복귀";
                case BattleCommandType.Reorganize: return "재편";
                case BattleCommandType.Cancel: return "취소";
                case BattleCommandType.Follow: return "중대 대형 추종";
                case BattleCommandType.Halt: return "정지";
                case BattleCommandType.LayMines: return "지뢰 매설";
                case BattleCommandType.ClearMines: return "지뢰 제거";
                case BattleCommandType.Fortify: return "진지 구축";
                case BattleCommandType.Airdrop: return "보급 투하";
                default: return "?";
            }
        }

        public static string LevelName(BattleUnitLevel level)
        {
            switch (level)
            {
                case BattleUnitLevel.TaskForce: return "대대 전투단";
                case BattleUnitLevel.Company: return "중대";
                case BattleUnitLevel.Platoon: return "소대";
                default: return "?";
            }
        }

        public static string CommsName(BattleCommsState comms)
        {
            switch (comms)
            {
                case BattleCommsState.Good: return "양호";
                case BattleCommsState.Poor: return "불량";
                case BattleCommsState.Cut: return "두절";
                default: return "?";
            }
        }

        public static string CommsIcon(BattleCommsState comms)
        {
            switch (comms)
            {
                case BattleCommsState.Good: return "●";
                case BattleCommsState.Poor: return "◐";
                case BattleCommsState.Cut: return "○";
                default: return "?";
            }
        }

        public static Color CommsColor(BattleCommsState comms)
        {
            switch (comms)
            {
                case BattleCommsState.Good: return UiTheme.Ok;
                case BattleCommsState.Poor: return UiTheme.Warning;
                default: return UiTheme.Error;
            }
        }

        public static string TechniqueName(string technique)
        {
            switch (technique)
            {
                case BattleSop.Traveling: return "일반";
                case BattleSop.Overwatch: return "경계 이동";
                case BattleSop.Bounding: return "교대 약진";
                default: return technique ?? string.Empty;
            }
        }

        public static string PostureName(string posture)
        {
            switch (posture)
            {
                case "idle": return "대기";
                case "moving": return "이동";
                case "defending": return "방어";
                case "withdrawing": return "철수";
                case "routed": return "패주";
                case "resupplying": return "보급 중";
                case "laying": return "지뢰 매설 중";
                case "clearing": return "지뢰 제거 중";
                case "fortifying": return "진지 구축 중";
                default: return posture ?? string.Empty;
            }
        }

        public static string FireAssetName(BattleFireAsset asset)
        {
            switch (asset)
            {
                case BattleFireAsset.Mortar: return "박격포";
                case BattleFireAsset.Artillery: return "포병";
                case BattleFireAsset.Cas: return "근접항공지원";
                default: return "?";
            }
        }

        public static string FirePhaseName(BattleFirePhase phase)
        {
            switch (phase)
            {
                case BattleFirePhase.Processing: return "처리 중";
                case BattleFirePhase.Adjusting: return "수정 사격";
                case BattleFirePhase.Effect: return "효력사";
                case BattleFirePhase.Done: return "완료";
                case BattleFirePhase.Cancelled: return "취소";
                default: return "?";
            }
        }

        public static string Clock(int seconds)
        {
            int value = Mathf.Max(0, seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", value / 60, value % 60);
        }

        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
