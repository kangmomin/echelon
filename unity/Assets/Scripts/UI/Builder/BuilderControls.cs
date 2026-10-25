using System;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Builder
{
    internal static class BuilderControls
    {
        internal static TMP_InputField CreateInput(Transform parent, string value, Action<string> onChange)
        {
            RectTransform root = UiKit.CreateRect(parent, "ForceNameInput");
            Image background = root.gameObject.AddComponent<Image>();
            background.color = UiTheme.ButtonNormal;
            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.caretColor = UiTheme.Text;
            input.selectionColor = UiTheme.ButtonSelected;

            RectTransform viewport = UiKit.CreateRect(root, "TextArea");
            UiKit.Stretch(viewport);
            viewport.offsetMin = new Vector2(8f, 2f);
            viewport.offsetMax = new Vector2(-8f, -2f);
            viewport.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI text = UiKit.CreateLabel(viewport, string.Empty, 14, UiTheme.Text);
            text.name = "Text";
            text.richText = false;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.Left;
            UiKit.Stretch(text.rectTransform);
            input.textViewport = viewport;
            input.textComponent = text;
            input.text = value ?? string.Empty;
            if (onChange != null)
                input.onValueChanged.AddListener(valueChanged => onChange(valueChanged));

            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 80f;
            layout.minHeight = 32f;
            layout.preferredHeight = 32f;
            layout.flexibleWidth = 1f;
            return input;
        }

        internal static Toggle CreateToggle(Transform parent, bool value, Action<bool> onChange)
        {
            RectTransform root = UiKit.CreateRect(parent, "IncludedToggle");
            Image background = root.gameObject.AddComponent<Image>();
            background.color = UiTheme.ButtonNormal;
            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            RectTransform checkmark = UiKit.CreatePanel(root, "Checkmark", UiTheme.Accent);
            UiKit.Stretch(checkmark);
            checkmark.offsetMin = new Vector2(4f, 4f);
            checkmark.offsetMax = new Vector2(-4f, -4f);
            checkmark.GetComponent<Image>().raycastTarget = false;
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = value;
            if (onChange != null)
                toggle.onValueChanged.AddListener(valueChanged => onChange(valueChanged));

            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 24f;
            layout.preferredWidth = 24f;
            layout.minHeight = 24f;
            layout.preferredHeight = 24f;
            layout.flexibleWidth = 0f;
            return toggle;
        }
    }
}
