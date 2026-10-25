using System;
using System.Collections.Generic;
using Echelon.Unity.Presentation.Symbols;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Minimap
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MinimapView : MonoBehaviour
    {
        public const float RefreshSeconds = 0.25f;
        private const int PixelCount = MinimapMapping.SizePixels * MinimapMapping.SizePixels;

        [SerializeField] private Vector2 terrainMapSize = new Vector2(4000f, 4000f);
        [SerializeField] private RawImage terrainImage;
        [SerializeField] private MinimapOverlay overlay;

        private readonly Color32[] _pixels = new Color32[PixelCount];
        private MinimapSnapshot _snapshot;
        private Texture2D _terrainTexture;
        private Canvas _canvas;
        private float _timer;

        public MinimapSnapshot Snapshot => _snapshot;
        public Texture2D TerrainTexture => _terrainTexture;
        public int RefreshVersion { get; private set; }
        public Vector2 MapSize => _snapshot == null ? terrainMapSize : _snapshot.MapSize;

        public event Action<Vector2> NavigationRequested;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            EnsureVisuals();
        }

        private void OnDestroy()
        {
            if (_terrainTexture == null)
                return;
            if (Application.isPlaying)
                Destroy(_terrainTexture);
            else
                DestroyImmediate(_terrainTexture);
            _terrainTexture = null;
        }

        public void SetState(MinimapSnapshot snapshot)
        {
            _snapshot = snapshot;
            RefreshNow();
        }

        public void SetTerrain(Vector2 mapSize, Func<Vector2, MinimapTerrainSample> sampleTerrain)
        {
            if (sampleTerrain == null)
                throw new ArgumentNullException(nameof(sampleTerrain));
            ValidateMapSize(mapSize);
            EnsureVisuals();
            terrainMapSize = mapSize;
            for (int y = 0; y < MinimapMapping.SizePixels; y++)
            {
                int textureY = MinimapMapping.SizePixels - 1 - y;
                for (int x = 0; x < MinimapMapping.SizePixels; x++)
                {
                    Vector2 position = MinimapMapping.TerrainSamplePosition(x, y, mapSize);
                    MinimapTerrainSample sample = sampleTerrain(position);
                    _pixels[textureY * MinimapMapping.SizePixels + x] = MinimapTerrainPalette.Shade(sample);
                }
            }
            ApplyTerrainPixels();
        }

        public void SetTerrainPixels(Vector2 mapSize, IReadOnlyList<Color32> topDownPixels)
        {
            ValidateMapSize(mapSize);
            if (topDownPixels == null)
                throw new ArgumentNullException(nameof(topDownPixels));
            if (topDownPixels.Count != PixelCount)
                throw new ArgumentException("The minimap requires exactly 210 x 210 pixels.", nameof(topDownPixels));
            EnsureVisuals();
            terrainMapSize = mapSize;
            for (int y = 0; y < MinimapMapping.SizePixels; y++)
            {
                int textureY = MinimapMapping.SizePixels - 1 - y;
                for (int x = 0; x < MinimapMapping.SizePixels; x++)
                    _pixels[textureY * MinimapMapping.SizePixels + x] = topDownPixels[y * MinimapMapping.SizePixels + x];
            }
            ApplyTerrainPixels();
        }

        public void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        public void Advance(float deltaSeconds)
        {
            _timer += Mathf.Max(0f, deltaSeconds);
            if (_timer >= RefreshSeconds)
                RefreshNow();
        }

        public void RefreshNow()
        {
            _timer = 0f;
            RefreshVersion++;
            EnsureVisuals();
            overlay.SetVerticesDirty();
        }

        public bool Click(Vector2 screenPosition, Camera canvasCamera = null)
        {
            Camera resolvedCamera = ResolveUiCamera(canvasCamera);
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform(), screenPosition, resolvedCamera))
                return false;
            return Navigate(screenPosition, resolvedCamera);
        }

        public bool Drag(Vector2 screenPosition, Camera canvasCamera = null)
        {
            return Navigate(screenPosition, ResolveUiCamera(canvasCamera));
        }

        internal void PopulateOverlay(SymbolMeshBatch batch, Rect localRect)
        {
            if (_snapshot == null || !HasValidMapSize(_snapshot.MapSize))
                return;
            float scale = localRect.width / MinimapMapping.SizePixels;
            if (_snapshot.ControlPoints != null)
            {
                for (int i = 0; i < _snapshot.ControlPoints.Count; i++)
                {
                    SymbolControlPointSnapshot point = _snapshot.ControlPoints[i];
                    if (point == null)
                        continue;
                    Vector2 center = MinimapMapping.MapToLocal(point.Position, _snapshot.MapSize, localRect);
                    float radius = point.RadiusMeters / _snapshot.MapSize.x * localRect.width;
                    Color color = MinimapPresentation.ControlPointColor(point.OwnerSide, _snapshot.PlayerSide);
                    batch.AddCircleOutline(center, radius, color, 1.5f * scale, 24);
                }
            }

            if (_snapshot.Contacts != null)
            {
                for (int i = 0; i < _snapshot.Contacts.Count; i++)
                {
                    MinimapContactSnapshot contact = _snapshot.Contacts[i];
                    if (!MinimapPresentation.ShouldDrawContact(contact, _snapshot.PlayerSide, _snapshot.FogEnabled))
                        continue;
                    Vector2 center = MinimapMapping.MapToLocal(contact.Position, _snapshot.MapSize, localRect);
                    Color color = MinimapPresentation.ContactColor(contact, _snapshot.PlayerSide);
                    batch.AddRect(new Rect(center.x - 1.5f * scale, center.y - 1.5f * scale, 3f * scale, 3f * scale), color);
                }
            }

            if (_snapshot.Camera != null && _snapshot.Camera.Visible)
            {
                Rect cameraRect = MinimapMapping.CameraRectangle(_snapshot.Camera, _snapshot.MapSize, localRect);
                batch.AddRectOutline(cameraRect, new Color(1f, 1f, 1f, 0.8f), Mathf.Max(0.01f, scale));
            }
        }

        private bool Navigate(Vector2 screenPosition, Camera canvasCamera)
        {
            if (!HasValidMapSize(MapSize))
                return false;
            RectTransform rect = rectTransform();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, screenPosition, canvasCamera, out Vector2 localPosition))
                return false;
            Vector2 mapPosition = MinimapMapping.LocalToMap(localPosition, MapSize, rect.rect);
            NavigationRequested?.Invoke(mapPosition);
            return true;
        }

        private Camera ResolveUiCamera(Camera eventCamera)
        {
            if (eventCamera != null)
                return eventCamera;
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            return _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        }

        private void EnsureVisuals()
        {
            RectTransform root = rectTransform();
            if (root.anchorMin == root.anchorMax)
                root.sizeDelta = new Vector2(MinimapMapping.SizePixels, MinimapMapping.SizePixels);
            if (_terrainTexture == null)
            {
                _terrainTexture = new Texture2D(MinimapMapping.SizePixels, MinimapMapping.SizePixels,
                    TextureFormat.RGBA32, false, false)
                {
                    name = "Battlefield Minimap Terrain",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            if (terrainImage == null)
            {
                var imageObject = new GameObject("Terrain", typeof(RectTransform), typeof(RawImage));
                imageObject.transform.SetParent(transform, false);
                terrainImage = imageObject.GetComponent<RawImage>();
                Stretch(terrainImage.rectTransform);
                terrainImage.raycastTarget = false;
            }
            terrainImage.texture = _terrainTexture;
            terrainImage.color = Color.white;
            if (overlay == null)
            {
                var overlayObject = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(MinimapOverlay));
                overlayObject.transform.SetParent(transform, false);
                overlay = overlayObject.GetComponent<MinimapOverlay>();
                Stretch(overlay.rectTransform);
                overlay.Configure(this);
            }
            terrainImage.transform.SetSiblingIndex(0);
            overlay.transform.SetAsLastSibling();
        }

        private void ApplyTerrainPixels()
        {
            _terrainTexture.SetPixels32(_pixels);
            _terrainTexture.Apply(false, false);
            terrainImage.texture = _terrainTexture;
        }

        private RectTransform rectTransform()
        {
            return (RectTransform)transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool HasValidMapSize(Vector2 mapSize)
        {
            return mapSize.x > 0f && mapSize.y > 0f;
        }

        private static void ValidateMapSize(Vector2 mapSize)
        {
            if (!HasValidMapSize(mapSize))
                throw new ArgumentException("Map dimensions must be positive.", nameof(mapSize));
        }
    }
}
