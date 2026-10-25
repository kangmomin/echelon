using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class Terrain
    {
        public enum Type
        {
            OPEN,
            ROAD,
            FOREST,
            URBAN,
            WATER
        }

        public static readonly string[] TypeNames = { "open", "road", "forest", "urban", "water" };
        public static readonly double[] CoverHeight = { 0.0, 0.0, 12.0, 10.0, 0.0 };
        public static readonly double[] SpeedFactorVehicle = { 1.0, 1.0, 0.5, 0.6, 0.0 };
        public static readonly double[] SpeedFactorFoot = { 1.0, 1.0, 0.8, 0.9, 0.0 };

        public const double VehicleMaxSlopeDeg = 35.0;
        public const double FootMaxSlopeDeg = 50.0;
        public const double RiverDepthM = 6.0;

        public const string MobilityVehicle = "vehicle";
        public const string MobilityFoot = "foot";
        public const string MobilityAir = "air";

        private Terrain(MapDef map)
        {
            Map = map;
            SizeM = map.SizeM;
            CellM = map.CellM;
            Width = (int)Math.Ceiling(SizeM.X / CellM);
            HeightCells = (int)Math.Ceiling(SizeM.Y / CellM);
            int count = Width * HeightCells;
            Types = new byte[count];
            Heights = new float[count];
            Slopes = new float[count];
            PassableVehicle = Array.Empty<byte>();
            PassableFoot = Array.Empty<byte>();
            ClearanceVehicle = Array.Empty<byte>();
            BridgeMask = Array.Empty<byte>();
        }

        public MapDef Map { get; }
        public SimVec2 SizeM { get; }
        public double CellM { get; }
        public int Width { get; }
        public int HeightCells { get; }
        public byte[] Types { get; private set; }
        public float[] Heights { get; private set; }
        public float[] Slopes { get; private set; }
        public byte[] PassableVehicle { get; private set; }
        public byte[] PassableFoot { get; private set; }
        public byte[] ClearanceVehicle { get; private set; }
        public byte[] BridgeMask { get; private set; }

        public static Terrain Build(MapDef map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            Terrain terrain = new Terrain(map);
            terrain.BuildHeights();
            foreach (MapDef.Feature feature in map.Forests)
            {
                terrain.FillPolygon(feature.Points, (int)Type.FOREST);
            }

            foreach (MapDef.Feature feature in map.Towns)
            {
                terrain.FillPolygon(feature.Points, (int)Type.URBAN);
            }

            foreach (MapDef.Feature feature in map.Rivers)
            {
                terrain.FillPolyline(feature.Points, feature.WidthM, (int)Type.WATER);
                terrain.CarveRiver(feature);
            }

            foreach (MapDef.Feature feature in map.Roads)
            {
                terrain.FillPolyline(feature.Points, feature.WidthM, (int)Type.ROAD);
            }

            terrain.BuildSlopes();
            terrain.BuildMasks();
            terrain.BuildClearance();
            return terrain;
        }

        public SimVec2I CellOf(SimVec2 point)
        {
            return new SimVec2I(
                (int)Math.Floor(point.X / CellM),
                (int)Math.Floor(point.Y / CellM));
        }

        public SimVec2 CellCenter(SimVec2I cell)
        {
            return new SimVec2((cell.X + 0.5) * CellM, (cell.Y + 0.5) * CellM);
        }

        public bool InBounds(SimVec2I cell)
        {
            return cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < HeightCells;
        }

        public bool Contains(SimVec2 point)
        {
            return point.X >= 0.0f && point.Y >= 0.0f && point.X < SizeM.X && point.Y < SizeM.Y;
        }

        public int IndexOf(SimVec2I cell)
        {
            return cell.Y * Width + cell.X;
        }

        public SimVec2 ClampToMap(SimVec2 point, double margin = 1.0)
        {
            return new SimVec2(
                SimMath.Clamp(point.X, margin, SizeM.X - margin),
                SimMath.Clamp(point.Y, margin, SizeM.Y - margin));
        }

        public int TypeAtCell(SimVec2I cell)
        {
            return InBounds(cell) ? Types[IndexOf(cell)] : (int)Type.WATER;
        }

        public int TypeAt(SimVec2 point)
        {
            return TypeAtCell(CellOf(point));
        }

        public string TypeNameAt(SimVec2 point)
        {
            return TypeNames[TypeAt(point)];
        }

        public double CoverHeightAt(SimVec2 point)
        {
            return CoverHeight[TypeAt(point)];
        }

        public double SlopeAtCell(SimVec2I cell)
        {
            return InBounds(cell) ? Slopes[IndexOf(cell)] : 90.0;
        }

        public double GroundHeight(SimVec2 point)
        {
            double fx = point.X / CellM - 0.5;
            double fy = point.Y / CellM - 0.5;
            int x0 = SimMath.Clamp((int)Math.Floor(fx), 0, Width - 1);
            int y0 = SimMath.Clamp((int)Math.Floor(fy), 0, HeightCells - 1);
            int x1 = Math.Min(x0 + 1, Width - 1);
            int y1 = Math.Min(y0 + 1, HeightCells - 1);
            double tx = SimMath.Clamp(fx - x0, 0.0, 1.0);
            double ty = SimMath.Clamp(fy - y0, 0.0, 1.0);
            double h00 = Heights[y0 * Width + x0];
            double h10 = Heights[y0 * Width + x1];
            double h01 = Heights[y1 * Width + x0];
            double h11 = Heights[y1 * Width + x1];
            return SimMath.Lerp(SimMath.Lerp(h00, h10, tx), SimMath.Lerp(h01, h11, tx), ty);
        }

        public bool IsPassableCell(SimVec2I cell, string mobility)
        {
            if (!InBounds(cell))
            {
                return false;
            }

            if (mobility == MobilityAir)
            {
                return true;
            }

            byte[] mask = mobility == MobilityVehicle ? PassableVehicle : PassableFoot;
            return mask[IndexOf(cell)] == 1;
        }

        public bool IsPassable(SimVec2 point, string mobility)
        {
            return IsPassableCell(CellOf(point), mobility);
        }

        public bool IsBridge(SimVec2 point)
        {
            SimVec2I cell = CellOf(point);
            return InBounds(cell) && BridgeMask[IndexOf(cell)] == 1;
        }

        public double SpeedFactor(int type, string mobility)
        {
            if (mobility == MobilityAir)
            {
                return 1.0;
            }

            return mobility == MobilityVehicle ? SpeedFactorVehicle[type] : SpeedFactorFoot[type];
        }

        public bool LinePassable(SimVec2 a, SimVec2 b, string mobility)
        {
            if (mobility == MobilityAir)
            {
                return Contains(a) && Contains(b);
            }

            byte[] mask = mobility == MobilityVehicle ? PassableVehicle : PassableFoot;
            double distance = a.DistanceTo(b);
            SimVec2I startCell = CellOf(a);
            if (InBounds(startCell) && ClearanceVehicle.Length != 0 &&
                distance / CellM + 1.0 < ClearanceVehicle[IndexOf(startCell)])
            {
                return true;
            }

            int steps = Math.Max(1, (int)Math.Ceiling(distance / (CellM * 0.5)));
            double inverseCell = 1.0 / CellM;
            double dx = (b.X - a.X) / steps;
            double dy = (b.Y - a.Y) / steps;
            double x = a.X;
            double y = a.Y;
            int last = -1;
            for (int i = 0; i < steps + 1; i++)
            {
                int cx = (int)Math.Floor(x * inverseCell);
                int cy = (int)Math.Floor(y * inverseCell);
                if (cx < 0 || cy < 0 || cx >= Width || cy >= HeightCells)
                {
                    return false;
                }

                int index = cy * Width + cx;
                if (index != last)
                {
                    if (mask[index] == 0)
                    {
                        return false;
                    }

                    last = index;
                }

                x += dx;
                y += dy;
            }

            return true;
        }

        public SimVec2 NearestPassable(SimVec2 point, string mobility, int maxRings = 30)
        {
            SimVec2I cell = CellOf(ClampToMap(point));
            if (IsPassableCell(cell, mobility))
            {
                return ClampToMap(point);
            }

            for (int ring = 1; ring <= maxRings; ring++)
            {
                SimVec2 best = SimVec2.Inf;
                double bestDistance = double.PositiveInfinity;
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        if (Math.Abs(dx) != ring && Math.Abs(dy) != ring)
                        {
                            continue;
                        }

                        SimVec2I candidate = new SimVec2I(cell.X + dx, cell.Y + dy);
                        if (IsPassableCell(candidate, mobility))
                        {
                            SimVec2 center = CellCenter(candidate);
                            double distance = center.DistanceSquaredTo(point);
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                best = center;
                            }
                        }
                    }
                }

                if (bestDistance < double.PositiveInfinity)
                {
                    return best;
                }
            }

            return point;
        }

        public bool HasTypeNear(SimVec2 point, int type, double radiusM)
        {
            SimVec2I cell = CellOf(point);
            int radius = (int)Math.Ceiling(radiusM / CellM);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (TypeAtCell(new SimVec2I(cell.X + dx, cell.Y + dy)) == type)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void BuildHeights()
        {
            GodotFastNoiseLite noise = new GodotFastNoiseLite(Map.HeightSeed, (float)Map.Frequency, Map.Octaves);
            for (int cy = 0; cy < HeightCells; cy++)
            {
                for (int cx = 0; cx < Width; cx++)
                {
                    SimVec2 point = CellCenter(new SimVec2I(cx, cy));
                    Heights[cy * Width + cx] = (float)(Map.BaseM + Map.AmplitudeM * noise.GetNoise2D(point.X, point.Y));
                }
            }

            float[] ridgeBump = new float[Heights.Length];
            foreach (MapDef.Feature ridge in Map.Ridges)
            {
                AddRidge(ridge, ridgeBump);
            }

            for (int i = 0; i < Heights.Length; i++)
            {
                Heights[i] += ridgeBump[i];
            }
        }

        private void AddRidge(MapDef.Feature ridge, float[] ridgeBump)
        {
            double half = ridge.WidthM * 0.5;
            for (int i = 0; i < ridge.Points.Count - 1; i++)
            {
                ForCellsNearSegment(ridge.Points[i], ridge.Points[i + 1], half, (index, distance) =>
                {
                    double bump = ridge.HeightM * 0.5 * (1.0 + Math.Cos(Math.PI * distance / half));
                    ridgeBump[index] = (float)Math.Max(ridgeBump[index], bump);
                });
            }
        }

        private void CarveRiver(MapDef.Feature river)
        {
            double half = river.WidthM * 1.5;
            for (int i = 0; i < river.Points.Count - 1; i++)
            {
                ForCellsNearSegment(river.Points[i], river.Points[i + 1], half, (index, distance) =>
                {
                    double depth = RiverDepthM * (1.0 - SimMath.Clamp(distance / half, 0.0, 1.0));
                    Heights[index] = (float)(Heights[index] - depth * 0.5);
                });
            }
        }

        private void BuildClearance()
        {
            int count = Width * HeightCells;
            int[] distances = new int[count];
            for (int i = 0; i < count; i++)
            {
                distances[i] = PassableVehicle[i] == 0 ? 0 : 255;
            }

            for (int cy = 0; cy < HeightCells; cy++)
            {
                for (int cx = 0; cx < Width; cx++)
                {
                    int index = cy * Width + cx;
                    if (distances[index] == 0)
                    {
                        continue;
                    }

                    int value = distances[index];
                    if (cx == 0 || cy == 0)
                    {
                        value = 1;
                    }
                    else
                    {
                        value = Math.Min(value, Math.Min(
                            Math.Min(distances[index - 1], distances[index - Width]),
                            Math.Min(distances[index - Width - 1], cx + 1 < Width ? distances[index - Width + 1] : 0)) + 1);
                    }

                    distances[index] = value;
                }
            }

            for (int cy = HeightCells - 1; cy >= 0; cy--)
            {
                for (int cx = Width - 1; cx >= 0; cx--)
                {
                    int index = cy * Width + cx;
                    if (distances[index] == 0)
                    {
                        continue;
                    }

                    int value = distances[index];
                    if (cx == Width - 1 || cy == HeightCells - 1)
                    {
                        value = 1;
                    }
                    else
                    {
                        value = Math.Min(value, Math.Min(
                            Math.Min(distances[index + 1], distances[index + Width]),
                            Math.Min(distances[index + Width + 1], cx > 0 ? distances[index + Width - 1] : 0)) + 1);
                    }

                    distances[index] = value;
                }
            }

            ClearanceVehicle = new byte[count];
            for (int i = 0; i < count; i++)
            {
                ClearanceVehicle[i] = (byte)Math.Min(distances[i], 255);
            }
        }

        private void BuildMasks()
        {
            int count = Width * HeightCells;
            PassableVehicle = new byte[count];
            PassableFoot = new byte[count];
            BridgeMask = new byte[count];
            for (int i = 0; i < count; i++)
            {
                int type = Types[i];
                if (type == (int)Type.WATER)
                {
                    continue;
                }

                PassableVehicle[i] = type == (int)Type.ROAD || Slopes[i] <= VehicleMaxSlopeDeg ? (byte)1 : (byte)0;
                PassableFoot[i] = type == (int)Type.ROAD || Slopes[i] <= FootMaxSlopeDeg ? (byte)1 : (byte)0;
            }

            int radius = (int)Math.Ceiling(30.0 / CellM);
            for (int cy = 0; cy < HeightCells; cy++)
            {
                for (int cx = 0; cx < Width; cx++)
                {
                    int index = cy * Width + cx;
                    if (Types[index] != (int)Type.ROAD)
                    {
                        continue;
                    }

                    bool nearWater = false;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int x = cx + dx;
                            int y = cy + dy;
                            if (x >= 0 && y >= 0 && x < Width && y < HeightCells &&
                                Types[y * Width + x] == (int)Type.WATER)
                            {
                                nearWater = true;
                                break;
                            }
                        }

                        if (nearWater)
                        {
                            break;
                        }
                    }

                    if (nearWater)
                    {
                        BridgeMask[index] = 1;
                    }
                }
            }
        }

        private void BuildSlopes()
        {
            for (int cy = 0; cy < HeightCells; cy++)
            {
                for (int cx = 0; cx < Width; cx++)
                {
                    double height = Heights[cy * Width + cx];
                    double heightX = Heights[cy * Width + Math.Min(cx + 1, Width - 1)];
                    double heightY = Heights[Math.Min(cy + 1, HeightCells - 1) * Width + cx];
                    double gradient = Math.Max(Math.Abs(heightX - height), Math.Abs(heightY - height)) / CellM;
                    Slopes[cy * Width + cx] = (float)(Math.Atan(gradient) * (180.0 / Math.PI));
                }
            }
        }

        private void FillPolygon(IReadOnlyList<SimVec2> points, int type)
        {
            MapRect bounds = new MapRect(points[0], SimVec2.Zero);
            for (int i = 0; i < points.Count; i++)
            {
                bounds = Expand(bounds, points[i]);
            }

            SimVec2I c0 = CellOf(bounds.Position);
            SimVec2I c1 = CellOf(bounds.End);
            for (int cy = Math.Max(c0.Y, 0); cy <= Math.Min(c1.Y, HeightCells - 1); cy++)
            {
                for (int cx = Math.Max(c0.X, 0); cx <= Math.Min(c1.X, Width - 1); cx++)
                {
                    if (Geometry2D.IsPointInPolygon(CellCenter(new SimVec2I(cx, cy)), points))
                    {
                        Types[cy * Width + cx] = (byte)type;
                    }
                }
            }
        }

        private void FillPolyline(IReadOnlyList<SimVec2> points, double widthM, int type)
        {
            double half = Math.Max(widthM * 0.5, CellM * 0.75);
            for (int i = 0; i < points.Count - 1; i++)
            {
                ForCellsNearSegment(points[i], points[i + 1], half, (index, distance) =>
                {
                    Types[index] = (byte)type;
                });
            }
        }

        private void ForCellsNearSegment(SimVec2 a, SimVec2 b, double radius, Action<int, double> action)
        {
            MapRect bounds = MapRect.FromCorners(a, b).Grow(radius);
            SimVec2I c0 = CellOf(bounds.Position);
            SimVec2I c1 = CellOf(bounds.End);
            for (int cy = Math.Max(c0.Y, 0); cy <= Math.Min(c1.Y, HeightCells - 1); cy++)
            {
                for (int cx = Math.Max(c0.X, 0); cx <= Math.Min(c1.X, Width - 1); cx++)
                {
                    SimVec2 center = CellCenter(new SimVec2I(cx, cy));
                    double distance = Geometry2D.GetClosestPointToSegment(center, a, b).DistanceTo(center);
                    if (distance <= radius)
                    {
                        action(cy * Width + cx, distance);
                    }
                }
            }
        }

        private static MapRect Expand(MapRect rectangle, SimVec2 point)
        {
            SimVec2 end = rectangle.End;
            SimVec2 position = new SimVec2(Math.Min(rectangle.Position.X, point.X), Math.Min(rectangle.Position.Y, point.Y));
            SimVec2 newEnd = new SimVec2(Math.Max(end.X, point.X), Math.Max(end.Y, point.Y));
            return new MapRect(position, newEnd - position);
        }
    }
}
