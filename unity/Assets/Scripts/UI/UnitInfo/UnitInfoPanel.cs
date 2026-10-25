using System;
using Echelon.Core;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.UnitInfo
{
    [DisallowMultipleComponent]
    public sealed class UnitInfoPanel : MonoBehaviour
    {
        public const string DefaultHint =
            "중대·소대·장비 템플릿·배속 자산에 마우스를 올리면 여기에 역할, 장점·약점, 능력치, " +
            "장비 제원(장갑·속도·무장)이 나옵니다.\n\n선택 상자를 열고 항목 위로 움직여도 바뀝니다.";

        public bool IsBuilt { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public TextMeshProUGUI Text { get; private set; }
        public string CurrentText => Text != null ? Text.text : string.Empty;

        public static UnitInfoPanel Create(Transform parent, string name = "UnitInfoPanel")
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            RectTransform root = UiKit.CreatePanel(parent, name);
            UnitInfoPanel panel = root.gameObject.AddComponent<UnitInfoPanel>();
            panel.Build();
            return panel;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;
            Scroll = UiKit.CreateScrollView(transform, "InformationScroll");
            UiKit.Stretch(Scroll.GetComponent<RectTransform>());
            VerticalLayoutGroup contentLayout = Scroll.content.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(12, 12, 12, 12);
            Text = UiKit.CreateLabel(Scroll.content, DefaultHint, 13, UiTheme.Text);
            Text.name = "InformationText";
            Text.lineSpacing = 8f;
            LayoutElement layout = Text.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
        }

        public void ShowPlatoon(DataCatalog catalog, string templateId, string loadout = "", int cost = -1)
        {
            ShowText(UnitInfo.PlatoonCard(catalog, templateId, loadout, cost));
        }

        public void ShowCompany(DataCatalog catalog, string templateId)
        {
            ShowText(UnitInfo.CompanyCard(catalog, templateId));
        }

        public void ShowText(string value)
        {
            Build();
            if (string.IsNullOrEmpty(value))
                return;
            Text.text = value;
            Scroll.StopMovement();
            Scroll.verticalNormalizedPosition = 1f;
        }

        public void Clear()
        {
            ShowText(DefaultHint);
        }

        private void Awake()
        {
            Build();
        }
    }
}
