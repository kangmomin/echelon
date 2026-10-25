using System;
using System.Collections.Generic;
using System.Globalization;
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
        public const string NoSelection =
            "부대를 선택하세요.\n좌클릭: 선택  우클릭: 명령  우클릭 드래그: 방향 지정\n" +
            "Shift: 명령 대기열  Ctrl+클릭: 추가 선택\n" +
            "WASD: \uCE74\uBA54\uB77C  Ctrl+1~9: 부대 지정  1~9: 불러오기\n" +
            "Tab: 하위 제대  L: 이동 방식 전환";

        public static string TopBar(BattleHudState state)
        {
            if (state == null)
                return string.Empty;

            int seconds = Mathf.Max(0, (int)state.SimulationSeconds);
            var parts = new List<string>
            {
                Clock(seconds),
                string.Format(CultureInfo.InvariantCulture, "{0:0.0}x", state.Speed)
            };
            if (state.HasScoring)
            {
                parts.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "승점 {0:0} : {1:0}",
                    state.PlayerVictoryPoints,
                    state.EnemyVictoryPoints));
                int remaining = Mathf.Max(0, Mathf.RoundToInt(state.TimeLimitSeconds - state.SimulationSeconds));
                parts.Add("남은 " + Clock(remaining));
                parts.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "전력 {0}% : {1}%",
                    Mathf.RoundToInt(state.PlayerForceRatio * 100f),
                    Mathf.RoundToInt(state.EnemyForceRatio * 100f)));
            }

            if (state.Deploying)
                parts.Add("배치 단계");
            else if (state.Paused)
                parts.Add("일시정지");

            if (state.CommandCapacityUsed.HasValue && state.CommandCapacityMaximum.HasValue)
            {
                string capacity = string.Format(
                    CultureInfo.InvariantCulture,
                    "지휘 용량 {0:0.0} / {1:0}",
                    state.CommandCapacityUsed.Value,
                    state.CommandCapacityMaximum.Value);
                if (state.CommandCapacityQueue > 0)
                    capacity += string.Format(CultureInfo.InvariantCulture, "  (대기 {0})", state.CommandCapacityQueue);
                parts.Add(capacity);
            }

            if (!string.IsNullOrEmpty(state.AdditionalStatus))
                parts.Add(state.AdditionalStatus);
            return string.Join("   ", parts);
        }

        public static string Selection(IReadOnlyList<BattleUnitView> selection)
        {
            if (selection == null || selection.Count == 0)
                return "<color=#aaaaaa>" + NoSelection + "</color>";

            BattleUnitView unit = selection[0];
            if (unit == null)
                return "<color=#aaaaaa>" + NoSelection + "</color>";

            var lines = new List<string>
            {
                "<b>" + Escape(unit.DisplayName) + "</b>  (" + LevelName(unit.Level) + ")"
            };
            if (selection.Count > 1)
                lines.Add(string.Format(CultureInfo.InvariantCulture, "외 {0}개 제대 선택", selection.Count - 1));
            lines.Add(string.Format(
                CultureInfo.InvariantCulture,
                "전투력 {0}%   사기 {1}   통신 {2}",
                Mathf.RoundToInt(unit.Strength * 100f),
                Mathf.RoundToInt(unit.Morale),
                CommsName(unit.Comms)));
            string formation = !string.IsNullOrEmpty(unit.FormationName)
                ? unit.FormationName
                : !string.IsNullOrEmpty(unit.FormationId)
                    ? BattleFormationCatalog.DisplayName(unit.FormationId)
                    : "-";
            lines.Add("대형 " + Escape(formation) + "   이동 방식 " + Escape(TechniqueName(unit.Technique)) +
                      "   사격 " + (unit.RulesOfEngagement == BattleSop.HoldFire ? "중지" : "자유"));
            lines.Add("자세 " + Escape(PostureName(unit.Posture)) + "   임무 " +
                      (unit.MissionType.HasValue ? OrderName(unit.MissionType.Value) : "없음"));
            if (unit.Detached)
                lines.Add("<color=#ffd166>분리됨 — 중대 지휘에서 빠짐</color>");
            if (unit.Leaderless)
                lines.Add("<color=#ff6b6b>지휘 공백 — 승계 중</color>");
            if (unit.Reforming)
                lines.Add("<color=#9fd3ff>대형 전환 중</color>");
            if (unit.Level == BattleUnitLevel.Platoon && unit.HasSquads)
                lines.Add("\uBCF4\uBCD1 " + (unit.Mounted ? "탑승" : "하차"));
            lines.Add(string.Format(CultureInfo.InvariantCulture, "탄약 {0}%", Mathf.RoundToInt(unit.AmmoRatio * 100f)));
            if (unit.Level == BattleUnitLevel.Platoon && unit.Kind == "engineer")
            {
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "공병: 지뢰 지대 {0}개 남음, 진지 {1}/{2}",
                    unit.MinesLeft,
                    unit.PositionsBuilt,
                    unit.PositionsCapacity));
            }
            if (unit.Kind == "aviation")
            {
                lines.Add("고도: " + (unit.Altitude == BattleSop.HighAltitude
                    ? "고공 — 멀리 보지만 잘 보임"
                    : "저공 — 지형 뒤에 숨음") + "  (V 로 전환)");
            }
            return string.Join("\n", lines);
        }

        public static string FireSupport(BattleFireSupportState state)
        {
            if (state == null)
                return string.Empty;
            var lines = new List<string>
            {
                string.Format(
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
                case BattleCommsState.Good: return new Color(0.45f, 1f, 0.45f);
                case BattleCommsState.Poor: return new Color(1f, 0.8f, 0.25f);
                default: return new Color(1f, 0.35f, 0.35f);
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
