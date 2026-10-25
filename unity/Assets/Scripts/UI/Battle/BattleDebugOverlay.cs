using System;
using System.Collections.Generic;
using System.Globalization;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Battle
{
    [Serializable]
    public sealed class BattleDiagnostics
    {
        public int FramesPerSecond { get; set; }
        public long Tick { get; set; }
        public int TicksThisFrame { get; set; }
        public long AdvanceMicroseconds { get; set; }
        public long PeakTickMicroseconds { get; set; }
        public int ElementCount { get; set; }
        public int UnitCount { get; set; }
        public int PendingPathRequests { get; set; }
        public int ProcessedPathRequests { get; set; }
        public float CameraHeight { get; set; }
        public int SymbolLevel { get; set; }
    }

    public static class BattleDebugText
    {
        public static IReadOnlyList<string> Lines(BattleDiagnostics diagnostics)
        {
            if (diagnostics == null)
                return Array.Empty<string>();
            return new[]
            {
                string.Format(
                    CultureInfo.InvariantCulture,
                    "FPS {0}   tick {1}   ticks/frame {2}",
                    diagnostics.FramesPerSecond,
                    diagnostics.Tick,
                    diagnostics.TicksThisFrame),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "advance {0} us (틱당 최대 {1} us)",
                    diagnostics.AdvanceMicroseconds,
                    diagnostics.PeakTickMicroseconds),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "엘리먼트 {0}   제대 {1}   경로 요청 대기 {2} (누적 {3})",
                    diagnostics.ElementCount,
                    diagnostics.UnitCount,
                    diagnostics.PendingPathRequests,
                    diagnostics.ProcessedPathRequests),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "\uCE74\uBA54\uB77C 높이 {0:0} m   부호 단계 {1}",
                    diagnostics.CameraHeight,
                    diagnostics.SymbolLevel)
            };
        }

        public static string Format(BattleDiagnostics diagnostics)
        {
            return string.Join("\n", Lines(diagnostics));
        }
    }

    [DisallowMultipleComponent]
    public sealed class BattleDebugOverlay : MonoBehaviour
    {
        private RectTransform _panel;

        public bool IsBuilt { get; private set; }
        public bool IsVisible => _panel != null && _panel.gameObject.activeSelf;
        public TextMeshProUGUI Label { get; private set; }

        public static BattleDebugOverlay Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("BattleDebugOverlay", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var overlay = root.AddComponent<BattleDebugOverlay>();
            overlay.Build();
            return overlay;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;
            UiKit.Stretch((RectTransform)transform);
            _panel = UiKit.CreatePanel(transform, "Diagnostics", new Color(0f, 0f, 0f, 0.6f));
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(8f, -8f);
            _panel.sizeDelta = new Vector2(620f, 104f);
            UiKit.AddVerticalLayout(_panel, 0, 8);
            Label = UiKit.CreateLabel(_panel, string.Empty, 14, UiTheme.Text);
            Label.name = "Text";
            Label.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutElement layout = Label.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;
            _panel.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            SetVisible(!IsVisible);
        }

        public void SetVisible(bool visible)
        {
            if (!IsBuilt)
                return;
            _panel.gameObject.SetActive(visible);
            if (visible)
                transform.SetAsLastSibling();
        }

        public void SetDiagnostics(BattleDiagnostics diagnostics)
        {
            if (IsVisible)
                Label.text = BattleDebugText.Format(diagnostics);
        }

        public void SetLines(IEnumerable<string> lines)
        {
            if (!IsVisible || Label == null || lines == null)
                return;
            Label.text = string.Join("\n", lines);
        }

        public string GetText()
        {
            return Label != null ? Label.text : string.Empty;
        }

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
                Toggle();
        }
    }
}
