using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Echelon.Unity.UI.Aar
{
    public sealed class AarViewData
    {
        public string Title { get; internal set; } = string.Empty;
        public Color TitleColor { get; internal set; }
        public string Summary { get; internal set; } = string.Empty;
        public string SidesText { get; internal set; } = string.Empty;
        public string ControlPointsText { get; internal set; } = string.Empty;
        public string FactorsText { get; internal set; } = string.Empty;
        public string LeftText { get; internal set; } = string.Empty;
        public string TimelineText { get; internal set; } = string.Empty;
    }

    public static class AarViewModel
    {
        private static readonly Color Friendly = new Color(0.55f, 0.75f, 1f);
        private static readonly Color Enemy = new Color(1f, 0.55f, 0.5f);
        private static readonly Color TextDim = new Color(0.65f, 0.67f, 0.72f);
        private static readonly Color Accent = new Color(0.95f, 0.80f, 0.35f);
        private static readonly Color Ok = new Color(0.45f, 1f, 0.50f);
        private static readonly Color Error = new Color(1f, 0.42f, 0.38f);

        public static AarViewData Build(AarData data, string playerSide)
        {
            data ??= new AarData();
            playerSide ??= "blue";
            string enemySide = playerSide == "blue" ? "red" : "blue";
            string winner = data.Result?.Winner ?? string.Empty;
            string reason = data.Result?.Reason ?? string.Empty;
            bool draw = string.IsNullOrEmpty(winner);
            bool victory = !draw && winner == playerSide;
            var view = new AarViewData
            {
                Title = "사후 검토 — " + (draw ? "무승부" : victory ? "승리" : "패배"),
                TitleColor = draw ? Accent : victory ? Ok : Error,
                Summary = string.Format(
                    CultureInfo.InvariantCulture,
                    "판정: {0}   ·   전투 시간 {1}   ·   첫 교전 {2}",
                    Escape(ReasonName(reason)),
                    Mmss(data.DurationSeconds),
                    data.FirstContactSeconds >= 0f ? Mmss(data.FirstContactSeconds) : "-")
            };
            view.SidesText = BuildSides(data, playerSide, enemySide);
            view.ControlPointsText = BuildPoints(data, playerSide);
            view.FactorsText = BuildFactors(data, playerSide, enemySide);
            view.LeftText = view.SidesText +
                            "<b>거점</b>\n" + view.ControlPointsText +
                            "\n<b>승패 요인</b>\n" + view.FactorsText;
            view.TimelineText = BuildTimeline(data, playerSide);
            return view;
        }

        public static string Mmss(float seconds)
        {
            int value = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", value / 60, value % 60);
        }

        public static string ClassName(string id)
        {
            switch (id)
            {
                case "tank": return "전차";
                case "ifv": return "\uBCF4\uBCD1\uC804\uD22C\uCC28";
                case "recon": return "정찰차";
                case "inf_squad": return "\uBCF4\uBCD1 \uBD84\uB300";
                case "mortar": return "박격포";
                case "cp": return "지휘소";
                case "truck": return "트럭";
                case "fo": return "관측반";
                case "aa": return "방공";
                case "ew": return "전자전";
                case "apc": return "장갑차";
                case "heli": return "공격헬기";
                case "eng": return "공병 장비";
                case "at": return "대전차 차량";
                default: return id ?? string.Empty;
            }
        }

        public static string ReasonName(string reason)
        {
            switch (reason)
            {
                case "collapse": return "전투력 붕괴";
                case "time": return "시간 종료(승점)";
                default: return reason ?? string.Empty;
            }
        }

        private static string BuildSides(AarData data, string playerSide, string enemySide)
        {
            var text = new StringBuilder();
            AppendSide(text, SideOrEmpty(data, playerSide), true);
            text.Append("\n");
            AppendSide(text, SideOrEmpty(data, enemySide), false);
            text.Append("\n");
            return text.ToString();
        }

        private static void AppendSide(StringBuilder text, AarSideData side, bool mine)
        {
            Color color = mine ? Friendly : Enemy;
            text.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append(">")
                .Append(mine ? "아군" : "적").Append(" — ").Append(Escape(side.Name))
                .Append("</color></b>\n");
            text.AppendFormat(
                CultureInfo.InvariantCulture,
                "승점 {0:0}   ·   남은 전투력 {1}%\n",
                side.VictoryPoints,
                Mathf.RoundToInt(side.ValueRatio * 100f));
            var losses = new List<string>();
            if (side.Losses != null)
            {
                foreach (AarLossData loss in side.Losses)
                {
                    if (loss != null)
                        losses.Add(Escape(ClassName(loss.ClassName)) + " " + loss.Count);
                }
            }
            text.AppendFormat(
                CultureInfo.InvariantCulture,
                "손실 {0} / {1}  ({2})   손상 {3}\n",
                side.LossesTotal,
                side.Elements,
                losses.Count > 0 ? string.Join(", ", losses) : "없음",
                side.Damaged);
            text.AppendFormat(
                CultureInfo.InvariantCulture,
                "명령 {0}건 — 평균 지연 {1:0}초, 최대 {2:0}초\n",
                side.Orders,
                side.OrderDelayMean,
                side.OrderDelayMaximum);
            text.AppendFormat(
                CultureInfo.InvariantCulture,
                "화력 요청 {0}   ·   통신 두절 {1}회   ·   보급 {2}회\n",
                side.FireMissions,
                side.CommsCuts,
                side.Resupplies);
            text.AppendFormat(CultureInfo.InvariantCulture, "사격 {0}발\n", side.Shots);
        }

        private static string BuildPoints(AarData data, string playerSide)
        {
            if (data.Points == null)
                return string.Empty;
            var lines = new List<string>();
            foreach (AarControlPointData point in data.Points)
            {
                if (point == null)
                    continue;
                string owner = string.IsNullOrEmpty(point.Owner)
                    ? "중립"
                    : point.Owner == playerSide ? "아군" : "적";
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} — {1}  (청 {2}, 적 {3} 보유)",
                    Escape(point.Name),
                    owner,
                    Mmss(point.HeldBlueSeconds),
                    Mmss(point.HeldRedSeconds)));
            }
            return string.Join("\n", lines);
        }

        private static string BuildFactors(AarData data, string playerSide, string enemySide)
        {
            AarSideData me = SideOrEmpty(data, playerSide);
            AarSideData them = SideOrEmpty(data, enemySide);
            var lines = new List<string>
            {
                string.Format(CultureInfo.InvariantCulture, "• 손실 교환비 {0} : {1}", me.LossesTotal, them.LossesTotal)
            };
            float delayDifference = me.OrderDelayMean - them.OrderDelayMean;
            if (Mathf.Abs(delayDifference) > 3f)
            {
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "• 명령 지연이 {0} 쪽이 {1:0}초 짧았다",
                    delayDifference < 0f ? "아군" : "적",
                    Mathf.Abs(delayDifference)));
            }
            if (me.CommsCuts >= 3)
                lines.Add(string.Format(CultureInfo.InvariantCulture, "• 통신 두절이 {0}회 — 무전 범위와 지휘소 위치를 점검", me.CommsCuts));
            if (me.FireMissions + them.FireMissions > 0)
            {
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "• 화력 요청 {0} : {1}",
                    me.FireMissions,
                    them.FireMissions));
            }
            return string.Join("\n", lines);
        }

        private static string BuildTimeline(AarData data, string playerSide)
        {
            if (data.Timeline == null)
                return string.Empty;
            float tickRate = data.TickRate > 0f ? data.TickRate : 10f;
            var lines = new List<string>();
            foreach (AarTimelineEntry entry in data.Timeline)
            {
                if (entry == null)
                    continue;
                Color color = TextDim;
                if (!string.IsNullOrEmpty(entry.Side))
                    color = entry.Side == playerSide ? new Color(0.6f, 0.8f, 1f) : new Color(1f, 0.6f, 0.55f);
                lines.Add(
                    "<color=#" + ColorUtility.ToHtmlStringRGB(TextDim) + ">" + Mmss(entry.Tick / tickRate) + "</color>  " +
                    "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + Escape(entry.Text) + "</color>");
            }
            return string.Join("\n", lines);
        }

        private static AarSideData SideOrEmpty(AarData data, string side)
        {
            return data.FindSide(side) ?? new AarSideData { Side = side };
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
