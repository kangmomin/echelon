using System;
using System.Collections.Generic;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class DeploymentPanel : MonoBehaviour
    {
        public const string Instructions =
            "부대를 고르고 우클릭(드래그: 방향)으로 배치 구역 안에 옮긴다.";

        public event Action StartRequested;
        public event Action<IReadOnlyList<DeploymentPlacementIntent>> PlacementRequested;

        public bool IsBuilt { get; private set; }
        public bool IsDeploying { get; private set; }
        public bool CanStart { get; private set; } = true;
        public Button StartButton { get; private set; }
        public TextMeshProUGUI InstructionLabel { get; private set; }

        public static DeploymentPanel Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("DeploymentPanel", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var panel = root.AddComponent<DeploymentPanel>();
            panel.Build();
            return panel;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;
            RectTransform rect = (RectTransform)transform;
            BattleHudLayout.PlaceDeploymentBanner(rect);
            Image background = gameObject.AddComponent<Image>();
            UiKit.SetRounded(background);
            background.color = UiTheme.PanelRaised;
            background.raycastTarget = true;
            UiKit.AddBorder(rect, UiTheme.WithAlpha(UiTheme.Accent, 0.5f));
            HorizontalLayoutGroup layout = UiKit.AddHorizontalLayout(rect, UiTheme.SpaceM, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            TextMeshProUGUI badge = UiKit.CreateBadge(transform, "배치 단계", UiTheme.Accent);
            badge.fontStyle = FontStyles.Bold;
            InstructionLabel = UiKit.CreateLabel(transform, Instructions, UiTheme.FontSmall, UiTheme.Text);
            InstructionLabel.name = "Instructions";
            InstructionLabel.alignment = TextAlignmentOptions.MidlineLeft;
            UiKit.SetSize(InstructionLabel, 0f, -1f, 1f);
            StartButton = UiKit.CreateButton(transform, "전투 시작", RequestStart, 120f, UiButtonStyle.Primary);
            StartButton.name = "StartBattle";
            UiKit.SetSize(StartButton, 120f, UiTheme.ControlHeight, 0f);
            UiKit.EnsureEventSystem();
            gameObject.SetActive(false);
        }

        public void SetDeploymentState(bool deploying, bool canStart = true, string status = null)
        {
            IsDeploying = deploying;
            CanStart = canStart;
            if (!IsBuilt)
                return;
            gameObject.SetActive(deploying);
            StartButton.interactable = deploying && canStart;
            InstructionLabel.text = string.IsNullOrEmpty(status) ? Instructions : status;
        }

        public void RequestStart()
        {
            if (IsDeploying && CanStart)
                StartRequested?.Invoke();
        }

        public void RequestPlacement(
            IReadOnlyList<DeploymentUnitState> units,
            Vector2 target,
            float? facingRadians = null)
        {
            if (!IsDeploying || units == null || units.Count == 0)
                return;
            IReadOnlyList<DeploymentPlacementIntent> intents =
                DeploymentPlacement.CreateIntents(units, target, facingRadians);
            if (intents.Count > 0)
                PlacementRequested?.Invoke(intents);
        }

        private void Awake()
        {
            Build();
        }
    }
}
