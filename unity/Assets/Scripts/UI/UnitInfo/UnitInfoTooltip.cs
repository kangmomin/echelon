using System;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Echelon.Unity.UI.UnitInfo
{
    [DisallowMultipleComponent]
    public sealed class UnitInfoTooltip : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _parent;
        private Canvas _canvas;

        public bool IsVisible { get; private set; }
        public TextMeshProUGUI Text { get; private set; }

        public static UnitInfoTooltip Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            RectTransform root = UiKit.CreatePanel(parent, "UnitInfoTooltip", new Color(0.025f, 0.03f, 0.04f, 0.98f));
            UnitInfoTooltip tooltip = root.gameObject.AddComponent<UnitInfoTooltip>();
            tooltip.Build();
            return tooltip;
        }

        public void Build()
        {
            if (_root != null)
                return;
            _root = (RectTransform)transform;
            _parent = transform.parent as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0f, 1f);
            _root.sizeDelta = new Vector2(460f, 220f);
            Image background = GetComponent<Image>();
            background.raycastTarget = false;
            CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Text = UiKit.CreateLabel(transform, string.Empty, 13, UiTheme.Text);
            Text.name = "TooltipText";
            Text.lineSpacing = 5f;
            UiKit.Stretch(Text.rectTransform);
            Text.rectTransform.offsetMin = new Vector2(12f, 10f);
            Text.rectTransform.offsetMax = new Vector2(-12f, -10f);
            gameObject.SetActive(false);
        }

        public void Show(string value, Vector2 screenPoint)
        {
            Build();
            if (string.IsNullOrEmpty(value))
            {
                Hide();
                return;
            }
            Text.text = value;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsVisible = true;
            Position(screenPoint);
        }

        public void Hide()
        {
            IsVisible = false;
            if (_root != null)
                gameObject.SetActive(false);
        }

        public void Position(Vector2 screenPoint)
        {
            if (_parent == null)
                _parent = transform.parent as RectTransform;
            if (_parent == null || _parent.rect.width <= 0f || _parent.rect.height <= 0f)
                return;
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            Camera camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screenPoint, camera, out Vector2 local))
                return;
            Vector2 size = new Vector2(
                Mathf.Min(460f, Mathf.Max(0f, _parent.rect.width - 24f)),
                Mathf.Min(220f, Mathf.Max(0f, _parent.rect.height - 24f)));
            _root.sizeDelta = size;
            float x = Mathf.Clamp(local.x + 16f, _parent.rect.xMin + 12f, _parent.rect.xMax - size.x - 12f);
            float y = Mathf.Clamp(local.y - 16f, _parent.rect.yMin + size.y + 12f, _parent.rect.yMax - 12f);
            _root.anchoredPosition = new Vector2(x, y);
        }

        private void Awake()
        {
            Build();
        }

        private void LateUpdate()
        {
            if (IsVisible && Mouse.current != null)
                Position(Mouse.current.position.ReadValue());
        }

        private void OnDisable()
        {
            IsVisible = false;
        }
    }
}
