using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.Presentation.Symbols
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattlefieldSymbolLayer : MaskableGraphic
    {
        private const float ProjectionHeightOffset = 2f;
        private const float DefendArrowLengthMeters = 120f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Camera uiCamera;
        [SerializeField] private float cameraHeight;
        [SerializeField] private Font labelFont;

        private readonly List<PickEntry> _picks = new List<PickEntry>();
        private readonly List<SymbolLabelSpec> _labels = new List<SymbolLabelSpec>();
        private readonly List<Vector2> _projectedPath = new List<Vector2>();
        private BattlefieldSymbolSnapshot _snapshot;
        private Func<Vector2, float> _groundHeight;
        private SymbolLabelPool _labelPool;
        private Canvas _canvas;
        private string _previewText = string.Empty;
        private Vector2 _previewScreenPosition;
        private bool _previewForce;
        private bool _hasDirectionDrag;
        private Vector2 _dragFrom;
        private Vector2 _dragTo;

        public override Texture mainTexture => Texture2D.whiteTexture;

        public BattlefieldSymbolSnapshot Snapshot => _snapshot;
        public SymbolAggregationLevel CurrentLevel => SymbolAggregation.LevelForCameraHeight(cameraHeight);
        public int DrawnPickCount => _picks.Count;
        public int VisibleLabelCount => _labelPool == null ? 0 : _labelPool.VisibleCount;

        public Camera TargetCamera
        {
            get => targetCamera;
            set
            {
                targetCamera = value;
                SetVerticesDirty();
            }
        }

        public Camera UiCamera
        {
            get => uiCamera;
            set => uiCamera = value;
        }

        public float CameraHeight
        {
            get => cameraHeight;
            set
            {
                cameraHeight = value;
                SetVerticesDirty();
            }
        }

        public Font LabelFont
        {
            get => labelFont;
            set
            {
                labelFont = value;
                SetVerticesDirty();
            }
        }

        public event Action<SymbolPickResult> UnitClicked;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = Color.white;
            _canvas = GetComponentInParent<Canvas>();
            _labelPool = new SymbolLabelPool();
            if (labelFont == null)
            {
                Font source = TMPro.TMP_Settings.defaultFontAsset != null
                    ? TMPro.TMP_Settings.defaultFontAsset.sourceFontFile
                    : null;
                labelFont = source != null ? source : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        public void SetView(
            Camera worldCamera,
            float heightAboveGround,
            Func<Vector2, float> groundHeight,
            Camera canvasCamera = null)
        {
            targetCamera = worldCamera;
            cameraHeight = heightAboveGround;
            _groundHeight = groundHeight;
            uiCamera = canvasCamera;
            SetVerticesDirty();
            SyncLabels();
        }

        public void SetGroundHeightSampler(Func<Vector2, float> groundHeight)
        {
            _groundHeight = groundHeight;
            SetVerticesDirty();
            SyncLabels();
        }

        public void SetState(BattlefieldSymbolSnapshot snapshot)
        {
            _snapshot = snapshot;
            SetVerticesDirty();
            SyncLabels();
        }

        public void SetCursorPreview(string text, Vector2 screenPosition, bool forceWithoutSelection = false)
        {
            _previewText = text ?? string.Empty;
            _previewScreenPosition = screenPosition;
            _previewForce = forceWithoutSelection;
            SetVerticesDirty();
        }

        public void ClearCursorPreview()
        {
            SetCursorPreview(string.Empty, Vector2.zero);
        }

        public void SetDirectionDrag(Vector2 fromSimPosition, Vector2 toSimPosition)
        {
            _dragFrom = fromSimPosition;
            _dragTo = toSimPosition;
            _hasDirectionDrag = true;
            SetVerticesDirty();
        }

        public void ClearDirectionDrag()
        {
            _hasDirectionDrag = false;
            SetVerticesDirty();
        }

        public bool TryPickUnit(Vector2 screenPosition, string side, out SymbolPickResult result)
        {
            float bestDistance = SymbolStyle.PickRadiusPixels;
            int bestIndex = -1;
            for (int i = 0; i < _picks.Count; i++)
            {
                PickEntry entry = _picks[i];
                if (!string.IsNullOrEmpty(side) && entry.Unit.Side != side)
                    continue;
                float distance = Vector2.Distance(entry.ScreenPosition, screenPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                result = default;
                return false;
            }
            PickEntry best = _picks[bestIndex];
            result = new SymbolPickResult(
                best.Unit.Id,
                best.Unit.ParentId,
                best.Unit.Side,
                best.ScreenPosition,
                bestDistance);
            return true;
        }

        public bool TryPickUnit(Vector2 screenPosition, out SymbolPickResult result)
        {
            return TryPickUnit(screenPosition, null, out result);
        }

        public string PickUnit(Vector2 screenPosition, string side = null)
        {
            return TryPickUnit(screenPosition, side, out SymbolPickResult result) ? result.UnitId : null;
        }

        public bool Click(Vector2 screenPosition, string side = null)
        {
            if (!TryPickUnit(screenPosition, side, out SymbolPickResult result))
                return false;
            UnitClicked?.Invoke(result);
            return true;
        }

        public void Update()
        {
            if (_snapshot != null)
                SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            var batch = new SymbolMeshBatch(vertices);
            batch.Clear();
            _picks.Clear();
            _labels.Clear();

            if (_snapshot != null && targetCamera != null)
            {
                DrawControlPoints(batch);
                DrawEngineering(batch);
                DrawDeployment(batch);
                DrawUnits(batch);
                DrawSelectionOverlays(batch);
                DrawCursorPreview();
                DrawDirectionDrag(batch);
            }

        }

        private void SyncLabels()
        {
            using (VertexHelper vertices = new VertexHelper())
            {
                var batch = new SymbolMeshBatch(vertices);
                batch.Clear();
                _picks.Clear();
                _labels.Clear();
                if (_snapshot != null && targetCamera != null)
                {
                    DrawControlPoints(batch);
                    DrawEngineering(batch);
                    DrawDeployment(batch);
                    DrawUnits(batch);
                    DrawSelectionOverlays(batch);
                    DrawCursorPreview();
                    DrawDirectionDrag(batch);
                }
            }
            if (_labelPool == null)
                _labelPool = new SymbolLabelPool();
            _labelPool.Sync(transform, rectTransform.rect, _labels, labelFont);
        }

        private void DrawControlPoints(SymbolMeshBatch batch)
        {
            if (_snapshot.ControlPoints == null)
                return;
            for (int i = 0; i < _snapshot.ControlPoints.Count; i++)
            {
                SymbolControlPointSnapshot point = _snapshot.ControlPoints[i];
                if (point == null || !TryProject(point.Position, out Vector2 center))
                    continue;
                float radius = ProjectedRadius(point.Position, point.RadiusMeters, center, 20f);
                Color color = SymbolStyle.OwnerColor(point.OwnerSide, _snapshot.PlayerSide);
                if (point.Contested && Time.frameCount % 40 < 20)
                    color = SymbolStyle.SelectColor;
                batch.AddCircleOutline(center, radius, SymbolStyle.WithAlpha(color, 0.85f), 2f, 48);
                batch.AddCircle(center, radius, SymbolStyle.WithAlpha(color, 0.06f), 48);

                var flag = new Rect(center.x - 7f, center.y - 4f, 14f, 14f);
                batch.AddRect(flag, SymbolStyle.WithAlpha(color, 0.95f));
                batch.AddRectOutline(flag, SymbolStyle.FrameColor, 1.5f);
                string label = point.DisplayName + (point.Contested ? " (경합)" : string.Empty);
                AddText(center + new Vector2(-40f, -20f), label, 13, SymbolStyle.TextColor);
            }
        }

        private void DrawEngineering(SymbolMeshBatch batch)
        {
            if (_snapshot.KnownMinefields != null)
            {
                for (int i = 0; i < _snapshot.KnownMinefields.Count; i++)
                {
                    SymbolMinefieldSnapshot minefield = _snapshot.KnownMinefields[i];
                    if (minefield == null || !TryProject(minefield.Position, out Vector2 center))
                        continue;
                    float radius = ProjectedRadius(minefield.Position, minefield.RadiusMeters, center, 14f);
                    Color color = SymbolStyle.SideColor(minefield.Side, _snapshot.PlayerSide);
                    for (int segment = 0; segment < 16; segment += 2)
                    {
                        batch.AddArc(
                            center,
                            radius,
                            Mathf.PI * 2f * segment / 16f,
                            Mathf.PI * 2f * (segment + 1) / 16f,
                            color,
                            2f,
                            4);
                    }
                    batch.AddCircle(center, radius, SymbolStyle.WithAlpha(color, 0.08f), 32);
                    Vector2[] offsets =
                    {
                        new Vector2(-0.4f, 0.2f), new Vector2(0.3f, 0.35f),
                        new Vector2(0f, -0.3f), new Vector2(-0.35f, -0.35f),
                        new Vector2(0.4f, -0.2f)
                    };
                    for (int cross = 0; cross < offsets.Length; cross++)
                        batch.AddCross(center + offsets[cross] * radius, 3f, color, 1.6f);
                    AddText(
                        center + new Vector2(-30f, radius + 6f),
                        "지뢰 " + minefield.Charges.ToString(CultureInfo.InvariantCulture),
                        12,
                        color);
                }
            }

            if (_snapshot.Fortifications == null)
                return;
            Color fortificationColor = new Color(0.85f, 0.75f, 0.45f, 0.9f);
            for (int i = 0; i < _snapshot.Fortifications.Count; i++)
            {
                SymbolFortificationSnapshot fortification = _snapshot.Fortifications[i];
                if (fortification == null || fortification.Side != _snapshot.PlayerSide
                    || !TryProject(fortification.Position, out Vector2 center))
                    continue;
                float radius = ProjectedRadius(fortification.Position, fortification.RadiusMeters, center, 16f);
                Vector2 previous = center + new Vector2(radius + 3f, 0f);
                for (int point = 1; point <= 32; point++)
                {
                    float angle = Mathf.PI * 2f * point / 32f;
                    float r = radius + (point % 2 == 0 ? 3f : -2f);
                    Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    batch.AddLine(previous, next, fortificationColor, 1.6f);
                    previous = next;
                }
                AddText(center + new Vector2(-14f, -4f), "진지", 12, new Color(0.95f, 0.85f, 0.55f, 1f));
            }
        }

        private void DrawDeployment(SymbolMeshBatch batch)
        {
            if (!_snapshot.IsDeploying || _snapshot.Deployment == null)
                return;
            Rect zone = _snapshot.Deployment.Zone;
            Vector2[] corners =
            {
                new Vector2(zone.xMin, zone.yMin), new Vector2(zone.xMin, zone.yMax),
                new Vector2(zone.xMax, zone.yMax), new Vector2(zone.xMax, zone.yMin)
            };
            var projectedCorners = new Vector2[4];
            for (int i = 0; i < corners.Length; i++)
            {
                if (!TryProject(corners[i], out projectedCorners[i]))
                    return;
            }
            batch.AddQuad(
                projectedCorners[0], projectedCorners[1], projectedCorners[2], projectedCorners[3],
                new Color(0.4f, 0.7f, 1.0f, 0.10f));

            Color edgeColor = new Color(0.55f, 0.8f, 1.0f, 0.9f);
            for (int edge = 0; edge < 4; edge++)
            {
                Vector2 a = corners[edge];
                Vector2 b = corners[(edge + 1) % 4];
                Vector2 previous = projectedCorners[edge];
                for (int step = 1; step <= 16; step++)
                {
                    if (TryProject(Vector2.Lerp(a, b, step / 16f), out Vector2 next))
                    {
                        batch.AddLine(previous, next, edgeColor, 2f);
                        previous = next;
                    }
                }
            }

            float labelY = zone.center.y < _snapshot.MapSize.y * 0.5f ? zone.yMax : zone.yMin;
            if (TryProject(new Vector2(zone.center.x, labelY), out Vector2 labelPosition))
            {
                AddText(labelPosition + new Vector2(-40f, 8f), "배치 구역", 14, new Color(0.7f, 0.88f, 1f, 1f));
            }

            for (int i = 0; i < _snapshot.Deployment.EntryPoints.Count; i++)
            {
                Vector2 entry = ClampToMap(_snapshot.Deployment.EntryPoints[i], 20f);
                if (!TryProject(entry, out Vector2 position))
                    continue;
                batch.AddCircle(position, 6f, edgeColor, 16);
                AddText(position + new Vector2(8f, 4f), "증원 진입 " + (i + 1).ToString(CultureInfo.InvariantCulture),
                    12, new Color(0.7f, 0.88f, 1f, 1f));
            }
        }

        private void DrawUnits(SymbolMeshBatch batch)
        {
            SymbolAggregationLevel level = CurrentLevel;
            if (_snapshot.Units == null)
                return;
            for (int i = 0; i < _snapshot.Units.Count; i++)
            {
                SymbolUnitSnapshot unit = _snapshot.Units[i];
                if (SymbolAggregation.ShouldDrawElements(unit, level, _snapshot.PlayerSide, _snapshot.FogEnabled))
                    DrawElements(batch, unit);
                if (SymbolAggregation.ShouldDrawUnitFrame(unit, level, _snapshot.PlayerSide, _snapshot.FogEnabled))
                    DrawUnitFrame(batch, unit);
            }
        }

        private void DrawElements(SymbolMeshBatch batch, SymbolUnitSnapshot unit)
        {
            bool selected = SymbolAggregation.IsSelected(unit, _snapshot.SelectedUnitIds);
            for (int i = 0; i < unit.Elements.Count; i++)
            {
                SymbolElementSnapshot element = unit.Elements[i];
                if (!SymbolAggregation.ShouldDrawElement(unit, element, _snapshot.PlayerSide, _snapshot.FogEnabled)
                    || !TryProject(element.Position, element.Altitude, out Vector2 position, out Vector2 screenPosition))
                    continue;
                Color color = element.IsAlive
                    ? SymbolStyle.SideColor(unit.Side, _snapshot.PlayerSide)
                    : SymbolStyle.DeadElementColor;
                color = SymbolStyle.ApplyContact(color, element.Contact);
                Color outline = selected ? SymbolStyle.SelectColor : SymbolStyle.FrameColor;
                if (element.IsInfantry)
                {
                    batch.AddCircle(position, 5f, color, 16);
                    batch.AddCircleOutline(position, 5f, outline, 1.5f, 16);
                }
                else
                {
                    Vector2 forward = element.Forward.sqrMagnitude > 0.000001f
                        ? element.Forward.normalized
                        : Vector2.up;
                    Vector2 direction = Vector2.up;
                    if (TryProject(element.Position + forward * 12f, out Vector2 tip)
                        && (tip - position).sqrMagnitude > 0.0001f)
                        direction = (tip - position).normalized;
                    Vector2 right = new Vector2(-direction.y, direction.x);
                    Vector2 a = position + direction * 8f;
                    Vector2 b = position - direction * 5f + right * 5f;
                    Vector2 c = position - direction * 5f - right * 5f;
                    batch.AddTriangle(a, b, c, color);
                    batch.AddLine(a, b, outline, 1.5f);
                    batch.AddLine(b, c, outline, 1.5f);
                    batch.AddLine(c, a, outline, 1.5f);
                }
                if (element.IsLeader)
                    AddText(position + new Vector2(9f, 6f), unit.Label, 12, SymbolStyle.TextColor);
                _picks.Add(new PickEntry(unit, screenPosition));
            }
        }

        private void DrawUnitFrame(SymbolMeshBatch batch, SymbolUnitSnapshot unit)
        {
            if (!TryProject(unit.Position, unit.Altitude, out Vector2 position, out Vector2 screenPosition))
                return;
            bool selected = SymbolAggregation.IsSelected(unit, _snapshot.SelectedUnitIds);
            bool hostile = SymbolStyle.IsHostile(unit.Side, _snapshot.PlayerSide);
            Color fill = SymbolStyle.ApplyContact(
                SymbolStyle.SideColor(unit.Side, _snapshot.PlayerSide), unit.Contact);
            Color outline = selected ? SymbolStyle.SelectColor : SymbolStyle.FrameColor;
            float width = selected ? 2.5f : 1.5f;
            if (unit.Detached)
                width += 1.5f;

            Rect glyphRect = new Rect(position.x - 15f, position.y - 10f, 30f, 20f);
            if (hostile)
            {
                Vector2 top = position + new Vector2(0f, 15f);
                Vector2 right = position + new Vector2(17f, 0f);
                Vector2 bottom = position + new Vector2(0f, -15f);
                Vector2 left = position + new Vector2(-17f, 0f);
                batch.AddQuad(top, right, bottom, left, fill);
                batch.AddLine(top, right, outline, width);
                batch.AddLine(right, bottom, outline, width);
                batch.AddLine(bottom, left, outline, width);
                batch.AddLine(left, top, outline, width);
                glyphRect = new Rect(position.x - 10f, position.y - 7f, 20f, 14f);
            }
            else
            {
                batch.AddRect(glyphRect, fill);
                batch.AddRectOutline(glyphRect, outline, width);
            }

            if (!SymbolGlyphRenderer.Draw(batch, glyphRect, unit.Branch, SymbolStyle.FrameColor))
            {
                AddText(new Vector2(glyphRect.xMin, position.y - 4f),
                    SymbolGlyphRenderer.GetFallbackText(unit.Branch), 10, SymbolStyle.FrameColor,
                    glyphRect.width, TextAnchor.LowerCenter);
            }
            DrawEchelonMark(batch, new Vector2(position.x, glyphRect.yMax + (hostile ? 10f : 6f)), unit.Level);

            Color nameColor = new Color(1f, 1f, 1f, unit.Contact == SymbolContactState.Stale ? 0.5f : 0.95f);
            AddText(new Vector2(position.x - 50f, glyphRect.yMin - 13f), unit.Label, 12, nameColor,
                100f, TextAnchor.LowerCenter);
            if (!hostile)
            {
                float strength = Mathf.Clamp01(unit.Strength);
                var bar = new Rect(position.x - 15f, glyphRect.yMin - 19f, 30f, 3f);
                batch.AddRect(bar, new Color(0f, 0f, 0f, 0.6f));
                batch.AddRect(new Rect(bar.xMin, bar.yMin, 30f * strength, 3f), SymbolStyle.StrengthColor(strength));
                DrawComms(batch, new Vector2(glyphRect.xMax + 4f, glyphRect.yMax - 10f), unit.Comms);
            }
            _picks.Add(new PickEntry(unit, screenPosition));
        }

        private void DrawEchelonMark(SymbolMeshBatch batch, Vector2 position, SymbolUnitLevel level)
        {
            switch (level)
            {
                case SymbolUnitLevel.Platoon:
                    for (int i = 0; i < 3; i++)
                        batch.AddCircle(position + new Vector2((i - 1) * 5f, 0f), 1.6f, Color.white, 8);
                    break;
                case SymbolUnitLevel.Company:
                    batch.AddLine(position + new Vector2(0f, -3f), position + new Vector2(0f, 3f), Color.white, 2f);
                    break;
                case SymbolUnitLevel.TaskForce:
                    batch.AddLine(position + new Vector2(-2f, -3f), position + new Vector2(-2f, 3f), Color.white, 2f);
                    batch.AddLine(position + new Vector2(2f, -3f), position + new Vector2(2f, 3f), Color.white, 2f);
                    break;
            }
        }

        private void DrawComms(SymbolMeshBatch batch, Vector2 position, SymbolCommsState state)
        {
            int bars = SymbolStyle.CommsBars(state);
            for (int i = 0; i < 3; i++)
            {
                float height = 3f + i * 2.5f;
                Color color = i < bars ? SymbolStyle.CommsColor(state) : new Color(1f, 1f, 1f, 0.2f);
                batch.AddRect(new Rect(position.x + i * 3.5f, position.y, 2.5f, height), color);
            }
        }

        private void DrawSelectionOverlays(SymbolMeshBatch batch)
        {
            if (_snapshot.Units == null)
                return;
            for (int i = 0; i < _snapshot.Units.Count; i++)
            {
                SymbolUnitSnapshot unit = _snapshot.Units[i];
                if (!SymbolAggregation.IsDirectlySelected(unit, _snapshot.SelectedUnitIds)
                    || !SymbolAggregation.IsUnitEligible(unit)
                    || !SymbolAggregation.IsContactVisible(unit.Side, unit.Contact, _snapshot.PlayerSide, _snapshot.FogEnabled))
                    continue;
                DrawMovement(batch, unit);
                DrawPendingOrders(batch, unit);
            }
        }

        private void DrawMovement(SymbolMeshBatch batch, SymbolUnitSnapshot unit)
        {
            SymbolMovementSnapshot movement = unit.Movement;
            if (movement == null)
                return;
            _projectedPath.Clear();
            if (movement.Path != null)
            {
                for (int i = 0; i < movement.Path.Count; i++)
                {
                    if (TryProject(movement.Path[i], out Vector2 projected))
                        _projectedPath.Add(projected);
                }
                for (int i = 1; i < _projectedPath.Count; i++)
                    batch.AddLine(_projectedPath[i - 1], _projectedPath[i], SymbolStyle.PathColor, 2f);
                if (_projectedPath.Count >= 2)
                    batch.AddCircle(_projectedPath[_projectedPath.Count - 1], 5f, SymbolStyle.PathColor, 16);
            }

            if (CurrentLevel == SymbolAggregationLevel.Element && unit.Level == SymbolUnitLevel.Platoon
                && movement.FormationGhosts != null)
            {
                Color ghostColor = new Color(1f, 1f, 1f, 0.5f);
                for (int i = 0; i < movement.FormationGhosts.Count; i++)
                {
                    if (TryProject(movement.FormationGhosts[i], out Vector2 ghost))
                        batch.AddCross(ghost, 3f, ghostColor, 1f);
                }
            }

            if (movement.IsDefending && movement.DefendDirection.sqrMagnitude > 0.000001f
                && TryProject(unit.GuidePosition, out Vector2 from)
                && TryProject(unit.GuidePosition + movement.DefendDirection.normalized * DefendArrowLengthMeters,
                    out Vector2 to))
            {
                batch.AddLine(from, to, SymbolStyle.DefendColor, 2f);
                AddArrowHead(batch, from, to, SymbolStyle.DefendColor);
            }
        }

        private void DrawPendingOrders(SymbolMeshBatch batch, SymbolUnitSnapshot unit)
        {
            if (unit.PendingOrders == null)
                return;
            for (int i = 0; i < unit.PendingOrders.Count; i++)
            {
                SymbolPendingOrderSnapshot order = unit.PendingOrders[i];
                if (order == null || !TryProject(unit.GuidePosition, out Vector2 from)
                    || !TryProject(order.TargetPosition, out Vector2 to))
                    continue;
                AddDashedLine(batch, from, to, SymbolStyle.PropagatingColor);
                string delay = order.WaitingForComms
                    ? "통신 대기"
                    : Mathf.Max(0f, order.RemainingSeconds).ToString("0", CultureInfo.InvariantCulture) + "초";
                AddText(to + new Vector2(8f, 8f), order.TypeName + "  " + delay, 13, SymbolStyle.PropagatingColor);
                batch.AddCircleOutline(to, 6f, SymbolStyle.PropagatingColor, 2f, 12);
            }
        }

        private void DrawCursorPreview()
        {
            if (string.IsNullOrEmpty(_previewText)
                || ((_snapshot.SelectedUnitIds == null || _snapshot.SelectedUnitIds.Count == 0) && !_previewForce)
                || !TryScreenToLocal(_previewScreenPosition, out Vector2 position))
                return;
            AddText(position + new Vector2(18f, -26f), _previewText, 13, SymbolStyle.PreviewColor);
        }

        private void DrawDirectionDrag(SymbolMeshBatch batch)
        {
            if (!_hasDirectionDrag || !TryProject(_dragFrom, out Vector2 from) || !TryProject(_dragTo, out Vector2 to))
                return;
            batch.AddLine(from, to, SymbolStyle.SelectColor, 2f);
            AddArrowHead(batch, from, to, SymbolStyle.SelectColor);
        }

        private static void AddDashedLine(SymbolMeshBatch batch, Vector2 from, Vector2 to, Color color)
        {
            float length = Vector2.Distance(from, to);
            if (length < 1f)
                return;
            Vector2 direction = (to - from) / length;
            for (float distance = 0f; distance < length; distance += 14f)
            {
                batch.AddLine(from + direction * distance,
                    from + direction * Mathf.Min(distance + 8f, length), color, 2f);
            }
        }

        private static void AddArrowHead(SymbolMeshBatch batch, Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.000001f)
                return;
            Vector2 direction = delta.normalized;
            Vector2 right = new Vector2(-direction.y, direction.x);
            batch.AddTriangle(to, to - direction * 10f + right * 5f, to - direction * 10f - right * 5f, color);
        }

        private float ProjectedRadius(Vector2 centerSim, float radiusMeters, Vector2 centerLocal, float fallback)
        {
            if (!TryProject(centerSim + Vector2.right * radiusMeters, out Vector2 edge))
                return fallback;
            return Vector2.Distance(centerLocal, edge);
        }

        private bool TryProject(Vector2 simPosition, out Vector2 localPosition)
        {
            return TryProject(simPosition, out localPosition, out _);
        }

        private bool TryProject(Vector2 simPosition, out Vector2 localPosition, out Vector2 screenPosition)
        {
            return TryProject(simPosition, 0f, out localPosition, out screenPosition);
        }

        // 항공기 부호는 지면이 아니라 기체 높이에 띄운다(altitude 는 화면용 고도).
        private bool TryProject(Vector2 simPosition, float altitude, out Vector2 localPosition, out Vector2 screenPosition)
        {
            localPosition = Vector2.zero;
            screenPosition = Vector2.zero;
            if (targetCamera == null)
                return false;
            float height = _groundHeight == null ? 0f : _groundHeight(ClampToMap(simPosition, 0f));
            Vector3 world = new Vector3(simPosition.x, height + altitude + ProjectionHeightOffset, simPosition.y);
            Vector3 screen = targetCamera.WorldToScreenPoint(world);
            if (screen.z <= 0f || float.IsNaN(screen.x) || float.IsInfinity(screen.x)
                || float.IsNaN(screen.y) || float.IsInfinity(screen.y))
                return false;
            screenPosition = screen;
            return TryScreenToLocal(screenPosition, out localPosition);
        }

        private bool TryScreenToLocal(Vector2 screenPosition, out Vector2 localPosition)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPosition, ResolveUiCamera(), out localPosition);
        }

        private Camera ResolveUiCamera()
        {
            if (uiCamera != null)
                return uiCamera;
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            return _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        }

        private Vector2 ClampToMap(Vector2 position, float margin)
        {
            Vector2 size = _snapshot == null ? Vector2.zero : _snapshot.MapSize;
            return new Vector2(
                Mathf.Clamp(position.x, margin, Mathf.Max(margin, size.x - margin)),
                Mathf.Clamp(position.y, margin, Mathf.Max(margin, size.y - margin)));
        }

        private void AddText(
            Vector2 position,
            string text,
            int fontSize,
            Color color,
            float width = -1f,
            TextAnchor alignment = TextAnchor.LowerLeft)
        {
            _labels.Add(new SymbolLabelSpec(position, text, fontSize, color, width, alignment));
        }

        private readonly struct PickEntry
        {
            internal readonly SymbolUnitSnapshot Unit;
            internal readonly Vector2 ScreenPosition;

            internal PickEntry(SymbolUnitSnapshot unit, Vector2 screenPosition)
            {
                Unit = unit;
                ScreenPosition = screenPosition;
            }
        }
    }
}
