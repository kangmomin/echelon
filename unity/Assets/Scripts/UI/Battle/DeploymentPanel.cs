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
            "배치 단계 — 부대를 고르고 우클릭(드래그로 방향)으로 배치 구역 안에 옮긴다.";

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
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -96f);
            rect.sizeDelta = new Vector2(700f, 58f);
            Image background = gameObject.AddComponent<Image>();
            background.color = UiTheme.Panel;
            background.raycastTarget = true;
            HorizontalLayoutGroup layout = UiKit.AddHorizontalLayout(rect, 12, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            InstructionLabel = UiKit.CreateLabel(transform, Instructions, 13, UiTheme.Text);
            InstructionLabel.name = "Instructions";
            InstructionLabel.alignment = TextAlignmentOptions.Left;
            LayoutElement labelLayout = InstructionLabel.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = 0f;
            labelLayout.flexibleWidth = 1f;
            StartButton = UiKit.CreateButton(transform, "전투 시작", RequestStart, 120f);
            StartButton.name = "StartBattle";
            LayoutElement startLayout = StartButton.GetComponent<LayoutElement>();
            startLayout.minWidth = 120f;
            startLayout.preferredWidth = 120f;
            startLayout.flexibleWidth = 0f;
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
