using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Common
{
    public enum UiButtonStyle
    {
        /// <summary>기본 조작 버튼.</summary>
        Secondary,
        /// <summary>화면의 주 행동(전투 시작, 배치로 등) — 호박색.</summary>
        Primary,
        /// <summary>배경 없는 버튼(목록 행, 접기 등).</summary>
        Ghost,
        /// <summary>되돌리기 어려운 행동(종료, 빼기).</summary>
        Danger
    }

    /// <summary>
    /// uGUI 화면을 코드로 만드는 공통 부품. 색·크기는 UiTheme, 모양은 UiSprites 를 쓴다.
    /// 버튼 Image 는 흰색으로 두고 ColorBlock 이 실제 색을 정한다(ColorTint 는 두 색을 곱한다).
    /// </summary>
    public static class UiKit
    {
        public static TMP_FontAsset DefaultFont { get; set; }

        // ── 캔버스·기본 ────────────────────────────────────────

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

        /// <summary>둥근 모서리 패널. color 를 주지 않으면 기본 표면색.</summary>
        public static RectTransform CreatePanel(Transform parent, string name = "Panel", Color? color = null)
        {
            var rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            SetRounded(image);
            image.color = color ?? UiTheme.Panel;
            image.raycastTarget = true;
            return rect;
        }

        /// <summary>표면 + 1px 테두리. HUD 패널·카드에 쓴다.</summary>
        public static RectTransform CreateCard(Transform parent, string name = "Card", Color? color = null)
        {
            RectTransform rect = CreatePanel(parent, name, color);
            AddBorder(rect);
            return rect;
        }

        /// <summary>요소 위에 둥근 1px 테두리를 얹는다(레이아웃에 참여하지 않음).</summary>
        public static Image AddBorder(RectTransform target, Color? color = null)
        {
            var rect = CreateRect(target, "Border");
            Stretch(rect);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Outline;
            image.type = Image.Type.Sliced;
            image.color = color ?? UiTheme.Border;
            image.raycastTarget = false;
            return image;
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

        public static void SetRounded(Image image, bool pill = false)
        {
            image.sprite = pill ? UiSprites.Pill : UiSprites.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        // ── 글자 ──────────────────────────────────────────────

        public static TextMeshProUGUI CreateLabel(
            Transform parent,
            string text,
            float fontSize = UiTheme.FontBody,
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
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>패널 안 소제목: 작은 글자, 자간을 벌린 흐린 색.</summary>
        public static TextMeshProUGUI CreateHeading(Transform parent, string text)
        {
            TextMeshProUGUI label = CreateLabel(parent, text, UiTheme.FontCaption, UiTheme.TextDim);
            label.name = "Heading";
            label.characterSpacing = 6f;
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        public static LayoutElement SetSize(Component target, float width = -1f, float height = -1f, float flexibleWidth = -1f)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f)
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
            }
            if (height >= 0f)
            {
                layout.minHeight = height;
                layout.preferredHeight = height;
            }
            if (flexibleWidth >= 0f)
                layout.flexibleWidth = flexibleWidth;
            return layout;
        }

        // ── 버튼 ──────────────────────────────────────────────

        public static Button CreateButton(
            Transform parent,
            string text,
            UnityAction onClick,
            float minWidth = 0f,
            UiButtonStyle style = UiButtonStyle.Secondary)
        {
            var rect = CreateRect(parent, "Button_" + text);
            var image = rect.gameObject.AddComponent<Image>();
            SetRounded(image);
            image.color = Color.white;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var label = CreateLabel(rect, text, UiTheme.FontBody, UiTheme.Text);
            label.name = "Text";
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(UiTheme.SpaceS, 0f);
            label.rectTransform.offsetMax = new Vector2(-UiTheme.SpaceS, 0f);

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = Mathf.Max(0f, minWidth);
            layout.minHeight = UiTheme.ControlHeight;
            layout.preferredHeight = UiTheme.ControlHeight;
            SetButtonStyle(button, style);
            return button;
        }

        /// <summary>버튼 이름 + 오른쪽 단축키 칩. HUD 명령 버튼에 쓴다.</summary>
        public static Button CreateCommandButton(
            Transform parent,
            string text,
            string key,
            UnityAction onClick,
            UiButtonStyle style = UiButtonStyle.Secondary)
        {
            Button button = CreateButton(parent, text, onClick, 0f, style);
            if (string.IsNullOrEmpty(key))
                return button;
            TextMeshProUGUI label = ButtonLabel(button);
            label.alignment = TextAlignmentOptions.Left;
            RectTransform chip = CreateKeyHint(button.transform, key);
            chip.anchorMin = new Vector2(1f, 0.5f);
            chip.anchorMax = new Vector2(1f, 0.5f);
            chip.pivot = new Vector2(1f, 0.5f);
            chip.anchoredPosition = new Vector2(-6f, 0f);
            float chipWidth = chip.sizeDelta.x;
            label.rectTransform.offsetMax = new Vector2(-chipWidth - 10f, 0f);
            return button;
        }

        /// <summary>키캡 모양의 작은 단축키 표시.</summary>
        public static RectTransform CreateKeyHint(Transform parent, string key)
        {
            RectTransform rect = CreateRect(parent, "Key_" + key);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Outline;
            image.type = Image.Type.Sliced;
            image.color = UiTheme.BorderStrong;
            image.raycastTarget = false;
            TextMeshProUGUI label = CreateLabel(rect, key, UiTheme.FontCaption, UiTheme.TextDim);
            label.name = "Text";
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(label.rectTransform);
            float width = Mathf.Max(18f, label.GetPreferredValues(key).x + 8f);
            rect.sizeDelta = new Vector2(width, 18f);
            return rect;
        }

        public static void SetButtonStyle(Button button, UiButtonStyle style)
        {
            Color normal, highlighted, pressed, text;
            switch (style)
            {
                case UiButtonStyle.Primary:
                    normal = UiTheme.Accent;
                    highlighted = UiTheme.AccentHover;
                    pressed = UiTheme.AccentPressed;
                    text = UiTheme.TextOnAccent;
                    break;
                case UiButtonStyle.Ghost:
                    normal = Color.clear;
                    highlighted = UiTheme.WithAlpha(UiTheme.ButtonHighlighted, 0.8f);
                    pressed = UiTheme.ButtonPressed;
                    text = UiTheme.Text;
                    break;
                case UiButtonStyle.Danger:
                    normal = UiTheme.ButtonNormal;
                    highlighted = UiTheme.WithAlpha(UiTheme.Error, 0.35f);
                    pressed = UiTheme.WithAlpha(UiTheme.Error, 0.5f);
                    text = UiTheme.Error;
                    break;
                default:
                    normal = UiTheme.ButtonNormal;
                    highlighted = UiTheme.ButtonHighlighted;
                    pressed = UiTheme.ButtonPressed;
                    text = UiTheme.Text;
                    break;
            }
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = highlighted,
                pressedColor = pressed,
                selectedColor = normal,
                disabledColor = UiTheme.ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            TextMeshProUGUI label = ButtonLabel(button);
            if (label != null)
            {
                label.color = text;
                label.fontStyle = style == UiButtonStyle.Primary ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        /// <summary>토글형 버튼(현재 명령 모드·대형 등)의 켜짐 표시. 꺼지면 baseStyle 로 돌아간다.</summary>
        public static void SetSelected(Button button, bool selected, UiButtonStyle baseStyle = UiButtonStyle.Secondary)
        {
            SetButtonStyle(button, baseStyle);
            if (!selected)
                return;
            ColorBlock colors = button.colors;
            colors.normalColor = UiTheme.ButtonSelected;
            colors.selectedColor = UiTheme.ButtonSelected;
            colors.highlightedColor = UiTheme.WithAlpha(UiTheme.Accent, 0.3f);
            button.colors = colors;
            TextMeshProUGUI label = ButtonLabel(button);
            if (label != null)
                label.color = UiTheme.Accent;
        }

        public static TextMeshProUGUI ButtonLabel(Button button)
        {
            Transform text = button.transform.Find("Text");
            return text != null ? text.GetComponent<TextMeshProUGUI>() : button.GetComponentInChildren<TextMeshProUGUI>();
        }

        // ── 작은 표시 ──────────────────────────────────────────

        /// <summary>색 바탕의 작은 꼬리표(진영, 상태 등).</summary>
        public static TextMeshProUGUI CreateBadge(Transform parent, string text, Color color)
        {
            RectTransform rect = CreateRect(parent, "Badge");
            var image = rect.gameObject.AddComponent<Image>();
            SetRounded(image);
            image.color = UiTheme.WithAlpha(color, 0.2f);
            image.raycastTarget = false;
            var fitter = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            fitter.padding = new RectOffset(6, 6, 1, 1);
            fitter.childAlignment = TextAnchor.MiddleCenter;
            fitter.childControlWidth = true;
            fitter.childControlHeight = true;
            TextMeshProUGUI label = CreateLabel(rect, text, UiTheme.FontCaption, color);
            label.name = "Text";
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        /// <summary>얇은 진행 막대. 반환한 UiBar 로 값을 바꾼다.</summary>
        public static UiBar CreateBar(Transform parent, string name = "Bar", float height = 4f)
        {
            RectTransform track = CreateRect(parent, name);
            var trackImage = track.gameObject.AddComponent<Image>();
            SetRounded(trackImage, true);
            trackImage.color = UiTheme.WithAlpha(UiTheme.Border, 0.9f);
            trackImage.raycastTarget = false;
            SetSize(track, -1f, height);
            RectTransform fill = CreateRect(track, "Fill");
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            SetRounded(fillImage, true);
            fillImage.raycastTarget = false;
            var bar = new UiBar(track, fill, fillImage);
            bar.SetValue(1f, UiTheme.Ok);
            return bar;
        }

        // ── 모달 ──────────────────────────────────────────────

        /// <summary>
        /// 화면 전체를 어둡게 덮는 막(scrim)과 그 가운데 패널. 막이 아래 클릭을 막는다.
        /// 반환값은 패널, 막은 패널의 부모다.
        /// </summary>
        public static RectTransform CreateModal(Transform parent, string name, Vector2 size)
        {
            RectTransform scrim = CreateRect(parent, name);
            Stretch(scrim);
            var scrimImage = scrim.gameObject.AddComponent<Image>();
            scrimImage.color = UiTheme.Scrim;
            scrimImage.raycastTarget = true;
            RectTransform panel = CreateCard(scrim, "Panel");
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = size;
            return panel;
        }

        // ── 선택 상자·스크롤 ────────────────────────────────────

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
            SetRounded(background);
            background.color = Color.white;
            var dropdown = rect.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.colors = new ColorBlock
            {
                normalColor = UiTheme.ButtonNormal,
                highlightedColor = UiTheme.ButtonHighlighted,
                pressedColor = UiTheme.ButtonPressed,
                selectedColor = UiTheme.ButtonNormal,
                disabledColor = UiTheme.ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            var caption = CreateLabel(rect, string.Empty, UiTheme.FontSmall, UiTheme.Text);
            caption.name = "Label";
            caption.alignment = TextAlignmentOptions.Left;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(10f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-24f, 0f);

            var arrow = CreateLabel(rect, "▾", UiTheme.FontSmall, UiTheme.TextDim);
            arrow.name = "Arrow";
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(22f, 0f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;

            var template = CreateCard(rect, "Template", UiTheme.PanelRaised);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.sizeDelta = new Vector2(0f, 200f);
            template.anchoredPosition = new Vector2(0f, -2f);
            var templateScroll = template.gameObject.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.vertical = true;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;
            templateScroll.scrollSensitivity = 30f;

            var viewport = CreateRect(template, "Viewport");
            Stretch(viewport);
            viewport.offsetMin = new Vector2(2f, 2f);
            viewport.offsetMax = new Vector2(-2f, -2f);
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
            SetRounded(itemBackground);
            itemBackground.color = Color.white;
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemBackground;
            toggle.colors = new ColorBlock
            {
                normalColor = Color.clear,
                highlightedColor = UiTheme.ButtonHighlighted,
                pressedColor = UiTheme.ButtonPressed,
                selectedColor = UiTheme.ButtonHighlighted,
                disabledColor = Color.clear,
                colorMultiplier = 1f,
                fadeDuration = 0.05f
            };
            toggle.isOn = false;

            var checkmark = CreatePanel(item, "Checkmark", UiTheme.Accent);
            checkmark.anchorMin = new Vector2(0f, 0.5f);
            checkmark.anchorMax = new Vector2(0f, 0.5f);
            checkmark.pivot = new Vector2(0f, 0.5f);
            checkmark.sizeDelta = new Vector2(3f, 16f);
            checkmark.anchoredPosition = new Vector2(4f, 0f);
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.graphic.canvasRenderer.SetAlpha(0f);

            var itemLabel = CreateLabel(item, string.Empty, UiTheme.FontSmall, UiTheme.Text);
            itemLabel.name = "ItemLabel";
            itemLabel.alignment = TextAlignmentOptions.Left;
            itemLabel.textWrappingMode = TextWrappingModes.NoWrap;
            itemLabel.overflowMode = TextOverflowModes.Ellipsis;
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
            layout.minHeight = UiTheme.ControlHeight;
            layout.preferredHeight = UiTheme.ControlHeight;
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
            viewport.offsetMax = new Vector2(-10f, 0f);
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
            scrollbarRect.sizeDelta = new Vector2(6f, 0f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            var scrollbarBackground = scrollbarRect.gameObject.AddComponent<Image>();
            SetRounded(scrollbarBackground, true);
            scrollbarBackground.color = UiTheme.WithAlpha(UiTheme.Border, 0.4f);
            var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();

            var slidingArea = CreateRect(scrollbarRect, "SlidingArea");
            Stretch(slidingArea);
            var handle = CreateRect(slidingArea, "Handle");
            Stretch(handle);
            var handleImage = handle.gameObject.AddComponent<Image>();
            SetRounded(handleImage, true);
            handleImage.color = Color.white;
            scrollbar.targetGraphic = handleImage;
            scrollbar.colors = new ColorBlock
            {
                normalColor = UiTheme.BorderStrong,
                highlightedColor = UiTheme.TextMuted,
                pressedColor = UiTheme.TextDim,
                selectedColor = UiTheme.BorderStrong,
                disabledColor = Color.clear,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.value = 1f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            return scroll;
        }

        // ── 배치 ──────────────────────────────────────────────

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

        public static VerticalLayoutGroup AddVerticalLayout(RectTransform target, int spacing = 0, int padding = 0)
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

        public static HorizontalLayoutGroup AddHorizontalLayout(RectTransform target, int spacing = 0, int padding = 0)
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

        /// <summary>부모 전체를 채운다.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>한 점(anchor)에 고정하고 크기를 정한다. anchor 와 pivot 은 같은 값이다.</summary>
        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void AddInputModule(GameObject eventSystemObject)
        {
            var module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }

    /// <summary>UiKit.CreateBar 가 만든 진행 막대.</summary>
    public sealed class UiBar
    {
        private readonly RectTransform _fill;
        private readonly Image _fillImage;

        internal UiBar(RectTransform root, RectTransform fill, Image fillImage)
        {
            Root = root;
            _fill = fill;
            _fillImage = fillImage;
        }

        public RectTransform Root { get; }
        public float Value { get; private set; }

        public void SetValue(float value, Color color)
        {
            Value = Mathf.Clamp01(value);
            _fill.anchorMax = new Vector2(Value, 1f);
            _fillImage.color = color;
            _fillImage.enabled = Value > 0.001f;
        }
    }
}
