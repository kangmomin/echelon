using System;
using System.Collections.Generic;
using Echelon.Unity.Presentation;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialWorldMarkerLayer : MonoBehaviour
    {
        private readonly List<MarkerVisual> _visuals = new List<MarkerVisual>();
        private TutorialController _controller;
        private UnityEngine.Camera _camera;
        private Func<Vector2, float> _groundHeight;
        private RectTransform _labelRoot;

        public int ActiveMarkerCount => _visuals.Count;
        public RectTransform LabelRoot => _labelRoot;

        public static TutorialWorldMarkerLayer Create(
            Transform worldParent,
            RectTransform labelParent,
            UnityEngine.Camera camera,
            TutorialController controller,
            Func<Vector2, float> groundHeight = null)
        {
            if (worldParent == null)
                throw new ArgumentNullException(nameof(worldParent));
            if (labelParent == null)
                throw new ArgumentNullException(nameof(labelParent));
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));

            var root = new GameObject("TutorialWorldMarkers");
            root.transform.SetParent(worldParent, false);
            var layer = root.AddComponent<TutorialWorldMarkerLayer>();
            layer._controller = controller;
            layer._camera = camera;
            layer._groundHeight = groundHeight ?? (_ => 0f);
            layer._labelRoot = UiKit.CreateRect(labelParent, "TutorialMarkerLabels");
            UiKit.Stretch(layer._labelRoot);
            layer._controller.MarkersChanged += layer.Rebuild;
            layer.Rebuild();
            return layer;
        }

        public void SetCamera(UnityEngine.Camera camera)
        {
            _camera = camera;
        }

        private void Rebuild()
        {
            ClearVisuals();
            for (int i = 0; i < _controller.Markers.Count; i++)
                _visuals.Add(CreateVisual(_controller.Markers[i]));
        }

        private MarkerVisual CreateVisual(TutorialWorldMarker marker)
        {
            var visual = new MarkerVisual(marker);
            visual.Root = new GameObject("Marker_" + marker.Text);
            visual.Root.transform.SetParent(transform, false);
            visual.Root.transform.position = ViewCoords.ToWorld(
                marker.SimPosition,
                _groundHeight(marker.SimPosition));

            CreateCylinder(
                visual,
                "Disc",
                new Color(marker.Color.r, marker.Color.g, marker.Color.b, 0.28f),
                new Vector3(marker.Radius * 2f, 1.5f, marker.Radius * 2f),
                new Vector3(0f, 2f, 0f));
            CreateCylinder(
                visual,
                "Beam",
                new Color(marker.Color.r, marker.Color.g, marker.Color.b, 0.6f),
                new Vector3(5f, marker.BeamHeight * 0.5f, 5f),
                new Vector3(0f, marker.BeamHeight * 0.5f, 0f));

            visual.Label = UiKit.CreatePanel(
                _labelRoot,
                "Label_" + marker.Text,
                new Color(0f, 0f, 0f, 0.65f));
            visual.Label.GetComponent<Image>().raycastTarget = false;
            visual.Label.anchorMin = new Vector2(0.5f, 0.5f);
            visual.Label.anchorMax = new Vector2(0.5f, 0.5f);
            visual.Label.pivot = new Vector2(0.5f, 0.5f);
            visual.Text = UiKit.CreateLabel(visual.Label, marker.Text, 15, marker.Color);
            UiKit.Stretch(visual.Text.rectTransform);
            visual.Text.rectTransform.offsetMin = new Vector2(6f, 0f);
            visual.Text.rectTransform.offsetMax = new Vector2(-6f, 0f);
            visual.Text.alignment = TextAlignmentOptions.Center;
            visual.Text.textWrappingMode = TextWrappingModes.NoWrap;
            visual.Label.sizeDelta = new Vector2(visual.Text.preferredWidth + 12f, 22f);
            return visual;
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _visuals.Count; i++)
                UpdateVisual(_visuals[i]);
        }

        private void UpdateVisual(MarkerVisual visual)
        {
            float ground = _groundHeight(visual.Marker.SimPosition);
            visual.Root.transform.position = ViewCoords.ToWorld(visual.Marker.SimPosition, ground);
            if (_camera == null)
            {
                visual.Label.gameObject.SetActive(false);
                return;
            }

            Vector3 world = ViewCoords.ToWorld(
                visual.Marker.SimPosition,
                ground + visual.Marker.BeamHeight);
            Vector3 screen = _camera.WorldToScreenPoint(world);
            bool visible = screen.z >= 0f;
            visual.Label.gameObject.SetActive(visible);
            if (!visible)
                return;

            Canvas canvas = _labelRoot.GetComponentInParent<Canvas>();
            UnityEngine.Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _labelRoot,
                screen,
                uiCamera,
                out Vector2 local))
            {
                visual.Label.anchoredPosition = local + new Vector2(0f, -24f);
            }
        }

        private void CreateCylinder(
            MarkerVisual visual,
            string name,
            Color color,
            Vector3 scale,
            Vector3 localPosition)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(visual.Root.transform, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localScale = scale;
            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                DestroyUnityObject(collider);
            }

            var renderer = cylinder.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Material material = CreateMarkerMaterial(name, color);
            visual.Materials.Add(material);
            renderer.sharedMaterial = material;
        }

        private static Material CreateMarkerMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new InvalidOperationException("No unlit transparent shader is available for tutorial markers.");

            var material = new Material(shader)
            {
                name = "TutorialMarker_" + name,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Surface"))
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            return material;
        }

        private void ClearVisuals()
        {
            for (int i = 0; i < _visuals.Count; i++)
            {
                MarkerVisual visual = _visuals[i];
                if (visual.Root != null)
                {
                    visual.Root.SetActive(false);
                    DestroyUnityObject(visual.Root);
                }
                if (visual.Label != null)
                {
                    visual.Label.gameObject.SetActive(false);
                    DestroyUnityObject(visual.Label.gameObject);
                }
                for (int j = 0; j < visual.Materials.Count; j++)
                {
                    if (visual.Materials[j] != null)
                        DestroyUnityObject(visual.Materials[j]);
                }
            }
            _visuals.Clear();
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.MarkersChanged -= Rebuild;
            ClearVisuals();
            if (_labelRoot != null)
                DestroyUnityObject(_labelRoot.gameObject);
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private sealed class MarkerVisual
        {
            internal MarkerVisual(TutorialWorldMarker marker)
            {
                Marker = marker;
            }

            internal TutorialWorldMarker Marker { get; }
            internal GameObject Root { get; set; }
            internal RectTransform Label { get; set; }
            internal TextMeshProUGUI Text { get; set; }
            internal List<Material> Materials { get; } = new List<Material>();
        }
    }
}
