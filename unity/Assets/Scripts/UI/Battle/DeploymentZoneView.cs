using System;
using System.Collections.Generic;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Battle
{
    public sealed class DeploymentZoneGraphic : MaskableGraphic
    {
        private readonly List<Vector2> _points = new List<Vector2>();
        public float BorderWidth { get; set; } = 2f;
        public Color FillColor { get; set; } = new Color(0.4f, 0.7f, 1f, 0.10f);
        public Color BorderColor { get; set; } = new Color(0.55f, 0.8f, 1f, 0.9f);

        public void SetPoints(IEnumerable<Vector2> points)
        {
            _points.Clear();
            if (points != null)
                _points.AddRange(points);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (_points.Count < 3)
                return;
            for (int i = 1; i < _points.Count - 1; i++)
            {
                AddVertex(vertexHelper, _points[0], FillColor);
                AddVertex(vertexHelper, _points[i], FillColor);
                AddVertex(vertexHelper, _points[i + 1], FillColor);
                vertexHelper.AddTriangle((i - 1) * 3, (i - 1) * 3 + 1, (i - 1) * 3 + 2);
            }

            for (int i = 0; i < _points.Count; i++)
            {
                Vector2 a = _points[i];
                Vector2 b = _points[(i + 1) % _points.Count];
                Vector2 direction = (b - a).normalized;
                if (direction.sqrMagnitude < 0.0001f)
                    continue;
                Vector2 normal = new Vector2(-direction.y, direction.x) * (BorderWidth * 0.5f);
                int index = vertexHelper.currentVertCount;
                AddVertex(vertexHelper, a - normal, BorderColor);
                AddVertex(vertexHelper, b - normal, BorderColor);
                AddVertex(vertexHelper, b + normal, BorderColor);
                AddVertex(vertexHelper, a + normal, BorderColor);
                vertexHelper.AddTriangle(index, index + 1, index + 2);
                vertexHelper.AddTriangle(index, index + 2, index + 3);
            }
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertexHelper.AddVert(vertex);
        }
    }

    [DisallowMultipleComponent]
    public sealed class DeploymentZoneView : MonoBehaviour
    {
        private DeploymentZoneGraphic _graphic;
        private RectTransform _markers;

        public bool IsBuilt { get; private set; }
        public int PointCount { get; private set; }

        public static DeploymentZoneView Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("DeploymentZoneView", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<DeploymentZoneView>();
            view.Build();
            return view;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;
            UiKit.Stretch((RectTransform)transform);
            RectTransform graphicRect = UiKit.CreateRect(transform, "ZoneGraphic");
            UiKit.Stretch(graphicRect);
            _graphic = graphicRect.gameObject.AddComponent<DeploymentZoneGraphic>();
            _graphic.raycastTarget = false;
            _markers = UiKit.CreateRect(transform, "Markers");
            UiKit.Stretch(_markers);
            PointCount = 0;
        }

        public void Render(
            DeploymentZoneData zone,
            Func<Vector2, Vector2> projectToScreen,
            Camera uiCamera = null)
        {
            if (zone == null || projectToScreen == null)
            {
                Clear();
                return;
            }
            IReadOnlyList<Vector2> corners = zone.Corners;
            var screenCorners = new List<Vector2>(corners.Count);
            foreach (Vector2 corner in corners)
                screenCorners.Add(projectToScreen(corner));
            var entries = new List<Vector2>();
            if (zone.EntryPoints != null)
            {
                foreach (Vector2 entry in zone.EntryPoints)
                    entries.Add(projectToScreen(entry));
            }
            Vector2 label = projectToScreen(zone.LabelPosition) + new Vector2(-40f, 8f);
            SetScreenGeometry(screenCorners, entries, label, uiCamera);
        }

        public void SetScreenGeometry(
            IReadOnlyList<Vector2> zoneCorners,
            IReadOnlyList<Vector2> entryPoints,
            Vector2 labelScreenPosition,
            Camera uiCamera = null)
        {
            if (!IsBuilt)
                return;
            var localCorners = new List<Vector2>();
            if (zoneCorners != null)
            {
                foreach (Vector2 point in zoneCorners)
                    localCorners.Add(ToLocal(point, uiCamera));
            }
            PointCount = localCorners.Count;
            _graphic.SetPoints(localCorners);
            ClearMarkers();
            if (localCorners.Count >= 3)
                CreateMarker("배치 구역", ToLocal(labelScreenPosition, uiCamera), 14, 150f);
            if (entryPoints != null)
            {
                for (int i = 0; i < entryPoints.Count; i++)
                {
                    CreateMarker(
                        "●  증원 진입 " + (i + 1),
                        ToLocal(entryPoints[i], uiCamera) + new Vector2(8f, 4f),
                        12,
                        140f);
                }
            }
        }

        public void Clear()
        {
            PointCount = 0;
            if (!IsBuilt)
                return;
            _graphic.SetPoints(null);
            ClearMarkers();
        }

        private void Awake()
        {
            Build();
        }

        private Vector2 ToLocal(Vector2 screenPoint, Camera uiCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform,
                screenPoint,
                uiCamera,
                out Vector2 local);
            return local;
        }

        private void CreateMarker(string text, Vector2 localPosition, int fontSize, float width)
        {
            TextMeshProUGUI label = UiKit.CreateLabel(
                _markers,
                text,
                fontSize,
                new Color(0.7f, 0.88f, 1f));
            label.name = text;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 26f);
            rect.anchoredPosition = localPosition;
        }

        private void ClearMarkers()
        {
            for (int i = _markers.childCount - 1; i >= 0; i--)
            {
                GameObject child = _markers.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}
