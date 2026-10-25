using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Common
{
    public static class UiKit
    {
        public static TMP_FontAsset DefaultFont { get; set; }

        public static Canvas CreateCanvas(string name = "UI", int sortingOrder = 0)
        {
            var canvasObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            return canvas;
        }

        public static EventSystem EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                if (eventSystem.GetComponent<BaseInputModule>() == null)
                    AddInputModule(eventSystem.gameObject);
                return eventSystem;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
            AddInputModule(eventSystemObject);
            return eventSystem;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform CreatePanel(
            Transform parent,
            string name = "Panel",
            Color? color = null)
        {
            var rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color ?? UiTheme.Panel;
            image.raycastTarget = true;
            return rect;
        }

        public static Image CreateBackground(Transform parent)
        {
            var rect = CreateRect(parent, "Background");
            Stretch(rect);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = UiTheme.Background;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateSeparator(Transform parent, bool vertical = false)
        {
            var rect = CreateRect(parent, vertical ? "VerticalSeparator" : "HorizontalSeparator");
            var image = rect.gameObject.AddComponent<Image>();
            image.color = UiTheme.Separator;
            image.raycastTarget = false;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            if (vertical)
            {
                layout.minWidth = 1f;
                layout.preferredWidth = 1f;
                layout.flexibleHeight = 1f;
            }
            else
            {
                layout.minHeight = 1f;
                layout.preferredHeight = 1f;
                layout.flexibleWidth = 1f;
            }
            return image;
        }

        public static TextMeshProUGUI CreateLabel(
            Transform parent,
            string text,
            int fontSize = 14,
            Color? color = null)
        {
            var rect = CreateRect(parent, "Label");
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = DefaultFont != null ? DefaultFont : TMP_Settings.defaultFontAsset;
            if (font != null)
                label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color ?? UiTheme.Text;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.richText = true;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button CreateButton(
            Transform parent,
            string text,
            UnityAction onClick,
            float minWidth = 0f)
        {
            var rect = CreateRect(parent, "Button_" + text);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = UiTheme.ButtonNormal;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColors(UiTheme.ButtonNormal);
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var label = CreateLabel(rect, text, 14, UiTheme.Text);
            label.name = "Text";
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = Mathf.Max(0f, minWidth);
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;
            return button;
        }

        public static TMP_Dropdown CreateOption<T>(
            Transform parent,
            IEnumerable<UiOption<T>> items,
            T selected,
            Action<T> onChange,
            float minWidth = 0f)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var values = new List<T>();
            var optionData = new List<TMP_Dropdown.OptionData>();
            var selectedIndex = 0;
            var comparer = EqualityComparer<T>.Default;
            foreach (var option in items)
            {
                if (comparer.Equals(option.Value, selected))
                    selectedIndex = values.Count;
                values.Add(option.Value);
                optionData.Add(new TMP_Dropdown.OptionData(option.Label));
            }
            if (values.Count == 0)
                throw new ArgumentException("At least one option is required.", nameof(items));

            var rect = CreateRect(parent, "Option");
            var background = rect.gameObject.AddComponent<Image>();
            background.color = UiTheme.ButtonNormal;
            var dropdown = rect.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.colors = ButtonColors(UiTheme.ButtonNormal);

            var caption = CreateLabel(rect, string.Empty, 14, UiTheme.Text);
            caption.name = "Label";
            caption.alignment = TextAlignmentOptions.Left;
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(10f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-30f, 0f);

            var arrow = CreateLabel(rect, "v", 14, UiTheme.TextDim);
            arrow.name = "Arrow";
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(26f, 0f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;

            var template = CreatePanel(rect, "Template", UiTheme.Panel);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.sizeDelta = new Vector2(0f, 160f);
            template.anchoredPosition = Vector2.zero;
            var templateScroll = template.gameObject.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.vertical = true;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateRect(template, "Viewport");
            Stretch(viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            templateScroll.viewport = viewport;

            var content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = new Vector2(0f, 28f);
            content.anchoredPosition = Vector2.zero;
            templateScroll.content = content;

            var item = CreateRect(content, "Item");
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(0.5f, 1f);
            item.sizeDelta = new Vector2(0f, 28f);
            var itemBackground = item.gameObject.AddComponent<Image>();
            itemBackground.color = UiTheme.ButtonNormal;
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemBackground;
            toggle.isOn = false;

            var checkmark = CreatePanel(item, "Checkmark", UiTheme.Accent);
            checkmark.anchorMin = new Vector2(0f, 0.5f);
            checkmark.anchorMax = new Vector2(0f, 0.5f);
            checkmark.pivot = new Vector2(0f, 0.5f);
            checkmark.sizeDelta = new Vector2(4f, 18f);
            checkmark.anchoredPosition = new Vector2(5f, 0f);
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.graphic.canvasRenderer.SetAlpha(0f);

            var itemLabel = CreateLabel(item, string.Empty, 14, UiTheme.Text);
            itemLabel.name = "ItemLabel";
            itemLabel.alignment = TextAlignmentOptions.Left;
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
            itemLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.template = template;
            dropdown.options = optionData;
            dropdown.SetValueWithoutNotify(selectedIndex);
            dropdown.RefreshShownValue();
            template.gameObject.SetActive(false);

            if (onChange != null)
            {
                dropdown.onValueChanged.AddListener(index =>
                {
                    if (index >= 0 && index < values.Count)
                        onChange(values[index]);
                });
            }

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = Mathf.Max(0f, minWidth);
            layout.minHeight = 32f;
            layout.preferredHeight = 32f;
            return dropdown;
        }

        public static ScrollRect CreateScrollView(Transform parent, string name = "ScrollView")
        {
            var rect = CreateRect(parent, name);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = true;
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 35f;

            var viewport = CreateRect(rect, "Viewport");
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-18f, 0f);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            scroll.content = content;
            var contentLayout = AddVerticalLayout(content);
            contentLayout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbarRect = CreateRect(rect, "Scrollbar");
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(14f, 0f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            var scrollbarBackground = scrollbarRect.gameObject.AddComponent<Image>();
            scrollbarBackground.color = new Color(0f, 0f, 0f, 0.25f);
            var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();

            var slidingArea = CreateRect(scrollbarRect, "SlidingArea");
            Stretch(slidingArea);
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);
            var handle = CreatePanel(slidingArea, "Handle", UiTheme.ButtonHighlighted);
            Stretch(handle);
            var handleImage = handle.GetComponent<Image>();
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.value = 1f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return scroll;
        }

        public static VerticalLayoutGroup CreateVerticalLayout(
            Transform parent,
            string name = "VerticalLayout",
            int spacing = 0,
            int padding = 0)
        {
            return AddVerticalLayout(CreateRect(parent, name), spacing, padding);
        }

        public static HorizontalLayoutGroup CreateHorizontalLayout(
            Transform parent,
            string name = "HorizontalLayout",
            int spacing = 0,
            int padding = 0)
        {
            return AddHorizontalLayout(CreateRect(parent, name), spacing, padding);
        }

        public static VerticalLayoutGroup AddVerticalLayout(
            RectTransform target,
            int spacing = 0,
            int padding = 0)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(
            RectTransform target,
            int spacing = 0,
            int padding = 0)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static ColorBlock ButtonColors(Color normal)
        {
            return new ColorBlock
            {
                normalColor = normal,
                highlightedColor = UiTheme.ButtonHighlighted,
                pressedColor = UiTheme.ButtonPressed,
                selectedColor = UiTheme.ButtonHighlighted,
                disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static void AddInputModule(GameObject eventSystemObject)
        {
            var module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
