using Echelon.Unity.UI.Common;
using UnityEngine;

namespace Echelon.Unity.UI.Battle
{
    /// <summary>
    /// 전투 화면 배치(기준 1600×900). 위: 상태 바 / 왼쪽: ORBAT / 오른쪽 위: 미니맵·알림 /
    /// 아래: 선택 부대 카드 · 명령 바 · 화력 지원. 패널끼리 겹치지 않게 한곳에서 정한다.
    /// </summary>
    public static class BattleHudLayout
    {
        public const float Margin = 8f;
        public const float TopBarHeight = 40f;
        public const float BottomHeight = 184f;
        public const float UnitCardWidth = 340f;
        public const float FirePanelWidth = 292f;
        public const float OrbatWidth = 300f;
        public const float MinimapSize = 220f;
        public const float NotificationWidth = 340f;

        /// <summary>상단 바 아래 첫 줄의 y(위에서부터, 음수).</summary>
        public const float BelowTopBar = -(TopBarHeight + Margin);

        /// <summary>아래 카드들 위쪽 경계(바닥에서부터).</summary>
        public const float AboveBottom = BottomHeight + Margin * 2f;

        /// <summary>왼쪽 위. 높이는 내용(contentHeight)만큼, 아래 카드 위쪽까지를 넘지 않는다.</summary>
        public static void PlaceOrbat(RectTransform rect, float contentHeight)
        {
            var parent = rect.parent as RectTransform;
            float available = (parent != null && parent.rect.height > 0f ? parent.rect.height : 900f)
                - (TopBarHeight + Margin + AboveBottom);
            float height = Mathf.Clamp(contentHeight, 80f, Mathf.Max(80f, available));
            UiKit.Anchor(rect, new Vector2(0f, 1f), new Vector2(Margin, BelowTopBar), new Vector2(OrbatWidth, height));
        }

        public static void PlaceMinimap(RectTransform rect)
        {
            UiKit.Anchor(rect, new Vector2(1f, 1f), new Vector2(-Margin, BelowTopBar), new Vector2(MinimapSize, MinimapSize));
        }

        public static void PlaceNotifications(RectTransform rect)
        {
            float top = TopBarHeight + Margin + MinimapSize + Margin;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-Margin, -top);
            rect.sizeDelta = new Vector2(NotificationWidth, -(top + AboveBottom));
        }

        public static void PlaceDeploymentBanner(RectTransform rect)
        {
            UiKit.Anchor(rect, new Vector2(0.5f, 1f), new Vector2(0f, BelowTopBar), new Vector2(640f, 56f));
        }
    }
}
