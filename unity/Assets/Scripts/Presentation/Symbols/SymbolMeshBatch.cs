using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.Presentation.Symbols
{
    public sealed class SymbolMeshBatch
    {
        private readonly VertexHelper _vertices;

        public SymbolMeshBatch(VertexHelper vertices)
        {
            _vertices = vertices;
        }

        public int VertexCount => _vertices.currentVertCount;
        public int IndexCount => _vertices.currentIndexCount;

        public void Clear()
        {
            _vertices.Clear();
        }

        public void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = VertexCount;
            AddVertex(a, color);
            AddVertex(b, color);
            AddVertex(c, color);
            _vertices.AddTriangle(start, start + 1, start + 2);
        }

        public void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = VertexCount;
            AddVertex(a, color);
            AddVertex(b, color);
            AddVertex(c, color);
            AddVertex(d, color);
            _vertices.AddTriangle(start, start + 1, start + 2);
            _vertices.AddTriangle(start, start + 2, start + 3);
        }

        public void AddRect(Rect rect, Color color)
        {
            AddQuad(
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMax, rect.yMin),
                color);
        }

        public void AddRectOutline(Rect rect, Color color, float width)
        {
            Vector2 a = new Vector2(rect.xMin, rect.yMin);
            Vector2 b = new Vector2(rect.xMin, rect.yMax);
            Vector2 c = new Vector2(rect.xMax, rect.yMax);
            Vector2 d = new Vector2(rect.xMax, rect.yMin);
            AddLine(a, b, color, width);
            AddLine(b, c, color, width);
            AddLine(c, d, color, width);
            AddLine(d, a, color, width);
        }

        public void AddLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.000001f || width <= 0f || color.a <= 0f)
                return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            AddQuad(a - normal, a + normal, b + normal, b - normal, color);
        }

        public void AddCircle(Vector2 center, float radius, Color color, int segments = 24)
        {
            if (radius <= 0f || color.a <= 0f)
                return;
            segments = Mathf.Max(3, segments);
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                float b = Mathf.PI * 2f * (i + 1) / segments;
                AddTriangle(
                    center,
                    center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius,
                    color);
            }
        }

        public void AddArc(
            Vector2 center,
            float radius,
            float startRadians,
            float endRadians,
            Color color,
            float width,
            int segments)
        {
            if (radius <= 0f || segments <= 0)
                return;
            Vector2 previous = center + PointOnCircle(startRadians, radius);
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startRadians, endRadians, i / (float)segments);
                Vector2 next = center + PointOnCircle(angle, radius);
                AddLine(previous, next, color, width);
                previous = next;
            }
        }

        public void AddCircleOutline(Vector2 center, float radius, Color color, float width, int segments = 32)
        {
            AddArc(center, radius, 0f, Mathf.PI * 2f, color, width, segments);
        }

        public void AddEllipseOutline(Vector2 center, float radiusX, float radiusY, Color color, float width, int segments = 16)
        {
            Vector2 previous = center + new Vector2(radiusX, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
                AddLine(previous, next, color, width);
                previous = next;
            }
        }

        public void AddPolygon(IList<Vector2> points, Color color)
        {
            if (points == null || points.Count < 3 || color.a <= 0f)
                return;
            for (int i = 1; i < points.Count - 1; i++)
                AddTriangle(points[0], points[i], points[i + 1], color);
        }

        public void AddCross(Vector2 center, float radius, Color color, float width)
        {
            AddLine(center + new Vector2(-radius, -radius), center + new Vector2(radius, radius), color, width);
            AddLine(center + new Vector2(-radius, radius), center + new Vector2(radius, -radius), color, width);
        }

        private static Vector2 PointOnCircle(float angle, float radius)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private void AddVertex(Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            _vertices.AddVert(vertex);
        }
    }
}
