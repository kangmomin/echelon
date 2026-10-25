using System;
using System.Globalization;
using Echelon.Unity.UI.Common;
using UnityEngine;

namespace Echelon.Unity.UI.Battle
{
    [Serializable]
    public sealed class BattleEventData
    {
        public string Type { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string TargetUnitName { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Winner { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string OrderTypeName { get; set; } = string.Empty;
        public bool HasUnit { get; set; }
        public bool AwaitingFollow { get; set; }
        public bool FollowsParent { get; set; }
        public bool None { get; set; }
        public int VictoryPoints { get; set; }
        public BattleCommandType OrderType { get; set; }
        public BattleCommsState CommsState { get; set; }
        public float Seconds { get; set; }
        public BattleFirePhase Phase { get; set; }
        public BattleFireAsset Asset { get; set; }
    }

    public static class BattleEventText
    {
        public static readonly Color Info = UiTheme.Text;
        public static readonly Color Good = UiTheme.Ok;
        public static readonly Color Warning = UiTheme.Warning;
        public static readonly Color Bad = UiTheme.Error;

        public static BattleNotification? Describe(BattleEventData data, string playerSide)
        {
            return TryDescribe(data, playerSide, out BattleNotification notification) ? notification : null;
        }

        public static bool TryDescribe(
            BattleEventData data,
            string playerSide,
            out BattleNotification notification)
        {
            notification = default;
            if (data == null)
                return false;
            string type = data.Type ?? string.Empty;
            string side = data.Side ?? string.Empty;
            string player = playerSide ?? string.Empty;

            switch (type)
            {
                case "point_captured":
                {
                    bool mine = side == player;
                    return Result(
                        (mine ? "아군" : "적") + ": " + data.Name + " 점령",
                        mine ? Good : Bad,
                        out notification);
                }
                case "objective_done":
                {
                    bool mine = side == player;
                    return Result(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} 임무 목표 달성: {1} (+{2})",
                            mine ? "아군" : "적",
                            data.Name,
                            data.VictoryPoints),
                        mine ? Good : Bad,
                        out notification);
                }
                case "battle_ended":
                    if (string.IsNullOrEmpty(data.Winner))
                        return Result("전투 종료 — 무승부", Warning, out notification);
                    return Result(
                        "전투 종료 — " + (data.Winner == player ? "승리" : "패배"),
                        data.Winner == player ? Good : Bad,
                        out notification);
                case "cas_shot_down":
                    if (side != player)
                        return Result("아군 방공이 적 항공기를 격추", Good, out notification);
                    break;
                case "mine_strike":
                    if (side == player)
                        return Result((data.HasUnit ? Name(data.UnitName) : "아군") + ": 지뢰를 밟음!", Bad, out notification);
                    return Result("적이 아군 지뢰 지대에 걸림", Good, out notification);
            }

            if (side != player && type != "order_waiting_capacity")
                return false;
            string unitName = Name(data.UnitName);
            switch (type)
            {
                case "arrived":
                    if (!data.HasUnit || data.AwaitingFollow || data.FollowsParent)
                        return false;
                    return Result(
                        unitName + ": 목표 도착 (" + BattleUiText.OrderName(data.OrderType) + ")",
                        Good,
                        out notification);
                case "order_received":
                    if (!data.HasUnit || data.OrderTypeName == "FOLLOW")
                        return false;
                    return Result(unitName + ": 명령 수신", Info, out notification);
                case "order_blocked":
                    return Result(unitName + ": 통신 두절 — 명령 대기 중", Warning, out notification);
                case "order_cancelled":
                    return Result(unitName + ": 명령 취소 (" + data.Reason + ")", Bad, out notification);
                case "order_waiting_capacity":
                    return side == player && Result(
                        "지휘 용량 부족 — 명령이 대기열에 들어감",
                        Warning,
                        out notification);
                case "comms_changed":
                    return Result(
                        unitName + ": " + (data.CommsState == BattleCommsState.Cut ? "통신 두절" : "통신 회복"),
                        data.CommsState == BattleCommsState.Cut ? Bad : Good,
                        out notification);
                case "commander_lost":
                    return Result(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}: 지휘관 손실 — 승계까지 {1:0}초",
                            unitName,
                            data.Seconds),
                        Bad,
                        out notification);
                case "succession_done":
                    return Result(unitName + ": 지휘권 승계 완료", Info, out notification);
                case "element_destroyed":
                    return Result(
                        unitName + ": " + (string.IsNullOrEmpty(data.ElementName) ? "엘리먼트" : data.ElementName) + " 격파됨",
                        Bad,
                        out notification);
                case "routed":
                    return Result(unitName + ": 패주!", Bad, out notification);
                case "rally":
                    return Result(unitName + ": 재집결", Info, out notification);
                case "order_refused":
                    if (data.Reason == "not_engineer")
                        return Result(unitName + ": 공병만 할 수 있는 작업입니다", Warning, out notification);
                    if (data.Reason == "no_capacity")
                        return Result(unitName + ": 남은 지뢰·진지 자재가 없습니다", Warning, out notification);
                    return Result(unitName + ": 사기 저하로 공격 명령 거부", Warning, out notification);
                case "minefield_laid":
                    return Result(unitName + ": 지뢰 지대 매설 완료", Good, out notification);
                case "minefield_cleared":
                    return data.None
                        ? Result(unitName + ": 근처에 지뢰 지대가 없습니다", Warning, out notification)
                        : Result(unitName + ": 지뢰 지대 제거 완료", Good, out notification);
                case "position_built":
                    return Result(unitName + ": 진지 구축 완료", Good, out notification);
                case "minefield_found":
                    return Result("적 지뢰 지대 발견", Warning, out notification);
                case "auto_withdraw":
                    return Result(unitName + ": 전투력 저하 — 스스로 철수", Warning, out notification);
                case "fire_mission":
                    if (data.Phase == BattleFirePhase.Effect)
                        return Result(BattleUiText.FireAssetName(data.Asset) + ": 효력사 시작", Info, out notification);
                    if (data.Phase == BattleFirePhase.Done)
                        return Result(BattleUiText.FireAssetName(data.Asset) + ": 사격 완료", Info, out notification);
                    return false;
                case "cas_shot_down":
                    return Result("근접항공지원: 적 방공에 격추됨", Bad, out notification);
                case "resupplied":
                    return Result(unitName + ": 보급 완료", Good, out notification);
                case "supply_dispatched":
                    return Result(
                        unitName + ": " + Name(data.TargetUnitName) + " 로 보급 출발",
                        Info,
                        out notification);
                case "supply_returning":
                    return Result(unitName + ": 보급소로 재보급 복귀", Info, out notification);
                case "repaired":
                    return Result(unitName + ": 손상 차량 수리 완료", Good, out notification);
                case "disperse":
                    return Result(unitName + ": 포격 — 분산 이동", Warning, out notification);
                case "reinforcement_arrived":
                    return Result(unitName + ": 증원 도착", Good, out notification);
                default:
                    return false;
            }
        }

        private static string Name(string value)
        {
            return string.IsNullOrEmpty(value) ? "?" : value;
        }

        private static bool Result(string text, Color color, out BattleNotification notification)
        {
            notification = new BattleNotification(text, color);
            return true;
        }
    }
}
