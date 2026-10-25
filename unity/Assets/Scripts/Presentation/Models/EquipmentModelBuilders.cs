using UnityEngine;

namespace Echelon.Presentation.Models
{
    public static partial class EquipmentModelLibrary
    {
        private static readonly float[] SideSigns = { -1.0f, 1.0f };

        private static Vector3 V(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        private static Matrix4x4 Xf(Vector3 position, Vector3 rotation)
        {
            return Matrix4x4.TRS(
                position * ModelScale,
                Quaternion.Euler(rotation * Mathf.Rad2Deg),
                Vector3.one);
        }

        private static void Box(EquipmentParts parts, EquipmentMeshRole role, Vector3 size, Vector3 position)
        {
            Box(parts, role, size, position, Vector3.zero);
        }

        private static void Box(
            EquipmentParts parts,
            EquipmentMeshRole role,
            Vector3 size,
            Vector3 position,
            Vector3 rotation)
        {
            parts.Add(role, EquipmentGeometry.Box(size * ModelScale), Xf(position, rotation));
        }

        private static void Wedge(
            EquipmentParts parts,
            EquipmentMeshRole role,
            float width,
            float height,
            float length,
            Vector3 position,
            bool front = true)
        {
            parts.Add(
                role,
                EquipmentGeometry.Wedge(width * ModelScale, height * ModelScale, length * ModelScale, front),
                Xf(position, Vector3.zero));
        }

        private static void Cylinder(
            EquipmentParts parts,
            EquipmentMeshRole role,
            float radius,
            float length,
            Vector3 position,
            Vector3 rotation,
            int segments = 8)
        {
            parts.Add(
                role,
                EquipmentGeometry.Cylinder(radius * ModelScale, length * ModelScale, segments),
                Xf(position, rotation));
        }

        private static void Sphere(EquipmentParts parts, EquipmentMeshRole role, Vector3 radius, Vector3 position)
        {
            parts.Add(
                role,
                EquipmentGeometry.Sphere(10, 5),
                Matrix4x4.TRS(position * ModelScale, Quaternion.identity, radius * ModelScale));
        }

        private static void Barrel(EquipmentParts parts, float radius, float length, Vector3 position)
        {
            Cylinder(
                parts,
                EquipmentMeshRole.Camo,
                radius,
                length,
                position + V(0.0f, 0.0f, -length * 0.5f),
                V(Mathf.PI * 0.5f, 0.0f, 0.0f));
        }

        private static void Tracks(EquipmentParts parts, float width, float length, int wheels, bool skirt)
        {
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                float x = sign * (width * 0.5f - 0.35f);
                Box(parts, EquipmentMeshRole.Dark, V(0.7f, 0.8f, length), V(x, 0.45f, 0.0f));
                for (int i = 0; i < wheels; i++)
                {
                    float z = Mathf.Lerp(-length * 0.42f, length * 0.42f, (float)i / Mathf.Max(wheels - 1, 1));
                    Cylinder(
                        parts,
                        EquipmentMeshRole.Dark,
                        0.38f,
                        0.5f,
                        V(x + sign * 0.12f, 0.42f, z),
                        V(0.0f, 0.0f, Mathf.PI * 0.5f));
                }
                if (skirt)
                {
                    Box(
                        parts,
                        EquipmentMeshRole.Camo,
                        V(0.12f, 0.55f, length * 0.85f),
                        V(x + sign * 0.42f, 0.75f, -length * 0.03f));
                }
            }
        }

        private static void Wheels(EquipmentParts parts, float width, float[] positions, float radius)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                float z = positions[i];
                for (int s = 0; s < SideSigns.Length; s++)
                {
                    float sign = SideSigns[s];
                    Cylinder(
                        parts,
                        EquipmentMeshRole.Dark,
                        radius,
                        0.45f,
                        V(sign * (width * 0.5f - 0.1f), radius, z),
                        V(0.0f, 0.0f, Mathf.PI * 0.5f),
                        10);
                }
            }
        }

        private static Vector3 Abrams(EquipmentParts hull, EquipmentParts turret, bool tusk)
        {
            Tracks(hull, 3.7f, 7.6f, 7, true);
            Box(hull, EquipmentMeshRole.Camo, V(3.5f, 0.75f, 6.6f), V(0.0f, 1.15f, 0.35f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.5f, 0.75f, 1.3f, V(0.0f, 1.15f, -3.6f));
            Box(hull, EquipmentMeshRole.Camo, V(3.3f, 0.35f, 1.2f), V(0.0f, 1.65f, 2.9f));
            if (tusk)
            {
                for (int s = 0; s < SideSigns.Length; s++)
                {
                    float sign = SideSigns[s];
                    Box(hull, EquipmentMeshRole.Camo, V(0.35f, 0.7f, 4.0f), V(sign * 2.05f, 1.0f, -0.6f));
                }
            }

            var pivot = V(0.0f, 1.55f, 0.3f);
            Box(turret, EquipmentMeshRole.Camo, V(3.3f, 0.85f, 3.2f), V(0.0f, 0.42f, 0.2f));
            Wedge(turret, EquipmentMeshRole.Camo, 3.0f, 0.85f, 1.1f, V(0.0f, 0.42f, -1.95f));
            Box(turret, EquipmentMeshRole.Dark, V(2.9f, 0.45f, 0.9f), V(0.0f, 0.45f, 2.25f));
            Box(turret, EquipmentMeshRole.Camo, V(0.5f, 0.5f, 0.6f), V(0.75f, 1.1f, 0.4f));
            Box(turret, EquipmentMeshRole.Camo, V(0.45f, 0.4f, 0.45f), V(-0.8f, 1.05f, 0.6f));
            Box(turret, EquipmentMeshRole.Camo, V(0.7f, 0.7f, 0.5f), V(0.0f, 0.45f, -2.3f));
            Barrel(turret, 0.12f, 5.4f, V(0.0f, 0.45f, -2.5f));
            Box(turret, EquipmentMeshRole.Marker, V(1.2f, 0.05f, 1.0f), V(0.0f, 0.88f, 0.8f));
            return pivot;
        }

        private static Vector3 T90(EquipmentParts hull, EquipmentParts turret, bool t72)
        {
            Tracks(hull, 3.5f, 6.8f, 6, true);
            Box(hull, EquipmentMeshRole.Camo, V(3.3f, 0.65f, 5.8f), V(0.0f, 1.1f, 0.4f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.3f, 0.65f, 1.3f, V(0.0f, 1.1f, -3.1f));
            Box(hull, EquipmentMeshRole.Dark, V(0.6f, 0.6f, 1.6f), V(-1.2f, 1.7f, 2.7f));
            var pivot = V(0.0f, 1.45f, 0.0f);
            Sphere(turret, EquipmentMeshRole.Camo, V(1.55f, 0.6f, 1.65f), V(0.0f, 0.25f, 0.1f));
            if (!t72)
            {
                Box(turret, EquipmentMeshRole.Camo, V(2.2f, 0.55f, 1.2f), V(0.0f, 0.35f, 1.6f));
            }
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Wedge(turret, EquipmentMeshRole.Camo, 0.9f, 0.45f, 1.0f, V(sign * 0.75f, 0.35f, -1.35f));
            }
            Box(turret, EquipmentMeshRole.Camo, V(0.45f, 0.45f, 0.45f), V(0.6f, 0.85f, 0.2f));
            Barrel(turret, 0.12f, 5.6f, V(0.0f, 0.4f, -1.4f));
            Box(turret, EquipmentMeshRole.Marker, V(1.0f, 0.05f, 0.9f), V(-0.3f, 0.86f, 0.4f));
            return pivot;
        }

        private static Vector3 Bradley(EquipmentParts hull, EquipmentParts turret, bool fist)
        {
            Tracks(hull, 3.4f, 6.2f, 6, true);
            Box(hull, EquipmentMeshRole.Camo, V(3.2f, 1.35f, 5.2f), V(0.0f, 1.45f, 0.5f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.2f, 1.35f, 1.2f, V(0.0f, 1.45f, -2.7f));
            Box(hull, EquipmentMeshRole.Dark, V(2.4f, 0.9f, 0.15f), V(0.0f, 1.4f, 3.15f));
            var pivot = V(0.35f, 2.12f, -0.2f);
            Box(turret, EquipmentMeshRole.Camo, V(1.9f, 0.7f, 2.2f), V(0.0f, 0.35f, 0.0f));
            Wedge(turret, EquipmentMeshRole.Camo, 1.9f, 0.7f, 0.6f, V(0.0f, 0.35f, -1.4f));
            if (fist)
            {
                Box(turret, EquipmentMeshRole.Camo, V(0.8f, 0.6f, 1.0f), V(0.0f, 0.95f, 0.3f));
                Box(turret, EquipmentMeshRole.Dark, V(0.6f, 0.35f, 0.1f), V(0.0f, 0.95f, -0.25f));
            }
            else
            {
                Barrel(turret, 0.07f, 2.6f, V(0.0f, 0.45f, -1.5f));
                Box(turret, EquipmentMeshRole.Camo, V(0.55f, 0.5f, 1.4f), V(-1.3f, 0.55f, 0.1f));
            }
            Box(turret, EquipmentMeshRole.Marker, V(0.9f, 0.05f, 0.9f), V(0.2f, 0.73f, 0.5f));
            return pivot;
        }

        private static Vector3 Bmp3(EquipmentParts hull, EquipmentParts turret, bool recon)
        {
            Tracks(hull, 3.3f, 6.6f, 6, false);
            Box(hull, EquipmentMeshRole.Camo, V(3.2f, 0.9f, 5.0f), V(0.0f, 1.15f, 0.7f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.2f, 0.9f, 1.8f, V(0.0f, 1.15f, -2.7f));
            var pivot = V(0.0f, 1.6f, 0.2f);
            Box(turret, EquipmentMeshRole.Camo, V(1.9f, 0.55f, 1.9f), V(0.0f, 0.28f, 0.0f));
            Wedge(turret, EquipmentMeshRole.Camo, 1.9f, 0.55f, 0.5f, V(0.0f, 0.28f, -1.2f));
            Barrel(turret, 0.11f, 2.4f, V(0.0f, 0.35f, -1.3f));
            Barrel(turret, 0.05f, 2.8f, V(0.35f, 0.3f, -1.3f));
            if (recon)
            {
                Box(turret, EquipmentMeshRole.Camo, V(0.9f, 0.6f, 0.15f), V(0.0f, 1.0f, 0.6f));
            }
            Box(turret, EquipmentMeshRole.Marker, V(0.8f, 0.05f, 0.8f), V(0.0f, 0.58f, 0.4f));
            return pivot;
        }

        private static Vector3 Bmp2(EquipmentParts hull, EquipmentParts turret)
        {
            Tracks(hull, 3.1f, 6.4f, 6, false);
            Box(hull, EquipmentMeshRole.Camo, V(3.0f, 0.8f, 4.6f), V(0.0f, 1.1f, 0.8f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.0f, 0.8f, 1.9f, V(0.0f, 1.1f, -2.5f));
            var pivot = V(0.0f, 1.5f, 0.0f);
            Sphere(turret, EquipmentMeshRole.Camo, V(1.0f, 0.4f, 1.0f), V(0.0f, 0.15f, 0.0f));
            Barrel(turret, 0.05f, 3.2f, V(0.0f, 0.35f, -0.8f));
            Box(turret, EquipmentMeshRole.Camo, V(0.3f, 0.3f, 1.2f), V(-0.8f, 0.45f, 0.0f));
            Box(turret, EquipmentMeshRole.Marker, V(0.7f, 0.05f, 0.7f), V(0.2f, 0.56f, 0.3f));
            return pivot;
        }

        private static Vector3 Bmp1Variant(EquipmentParts hull, EquipmentParts turret, bool command)
        {
            Tracks(hull, 3.0f, 6.4f, 6, false);
            Box(hull, EquipmentMeshRole.Camo, V(2.9f, 0.8f, 4.4f), V(0.0f, 1.1f, 0.9f));
            Wedge(hull, EquipmentMeshRole.Camo, 2.9f, 0.8f, 1.9f, V(0.0f, 1.1f, -2.3f));
            var pivot = V(0.0f, 1.5f, 0.1f);
            if (command)
            {
                Box(hull, EquipmentMeshRole.Camo, V(1.6f, 0.45f, 1.6f), V(0.0f, 1.72f, 1.6f));
                for (int i = 0; i < 2; i++)
                {
                    float x = i == 0 ? -0.9f : 0.9f;
                    Cylinder(hull, EquipmentMeshRole.Dark, 0.05f, 4.5f, V(x, 3.7f, 2.4f), Vector3.zero);
                }
                Cylinder(turret, EquipmentMeshRole.Dark, 0.04f, 9.0f, V(0.0f, 4.5f, 0.0f), Vector3.zero);
                Box(turret, EquipmentMeshRole.Marker, V(0.8f, 0.05f, 0.8f), V(0.0f, 0.02f, 0.0f));
            }
            else
            {
                Sphere(turret, EquipmentMeshRole.Camo, V(0.95f, 0.42f, 0.95f), V(0.0f, 0.15f, 0.0f));
                Box(turret, EquipmentMeshRole.Dark, V(0.7f, 0.35f, 0.15f), V(0.0f, 0.3f, -0.85f));
                Box(turret, EquipmentMeshRole.Dark, V(0.9f, 0.7f, 0.1f), V(0.0f, 0.95f, 0.4f));
                Box(turret, EquipmentMeshRole.Marker, V(0.6f, 0.05f, 0.6f), V(0.35f, 0.58f, 0.2f));
            }
            return pivot;
        }

        private static Vector3 Nona(EquipmentParts hull, EquipmentParts turret)
        {
            Tracks(hull, 2.8f, 5.8f, 5, false);
            Box(hull, EquipmentMeshRole.Camo, V(2.7f, 0.7f, 4.8f), V(0.0f, 1.0f, 0.3f));
            Wedge(hull, EquipmentMeshRole.Camo, 2.7f, 0.7f, 1.2f, V(0.0f, 1.0f, -2.5f));
            var pivot = V(0.0f, 1.35f, 0.3f);
            Box(turret, EquipmentMeshRole.Camo, V(1.7f, 0.6f, 1.8f), V(0.0f, 0.3f, 0.0f));
            Wedge(turret, EquipmentMeshRole.Camo, 1.7f, 0.6f, 0.6f, V(0.0f, 0.3f, -1.2f));
            Cylinder(turret, EquipmentMeshRole.Camo, 0.13f, 1.8f, V(0.0f, 0.75f, -1.2f), V(Mathf.PI * 0.42f, 0.0f, 0.0f));
            Box(turret, EquipmentMeshRole.Marker, V(0.8f, 0.05f, 0.8f), V(0.0f, 0.62f, 0.4f));
            return pivot;
        }

        private static Vector3 Tor(EquipmentParts hull, EquipmentParts turret)
        {
            Tracks(hull, 3.3f, 6.8f, 6, false);
            Box(hull, EquipmentMeshRole.Camo, V(3.2f, 1.1f, 6.0f), V(0.0f, 1.25f, 0.2f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.2f, 1.1f, 1.0f, V(0.0f, 1.25f, -3.3f));
            var pivot = V(0.0f, 1.8f, 0.4f);
            Box(turret, EquipmentMeshRole.Camo, V(2.6f, 1.9f, 2.6f), V(0.0f, 0.95f, 0.0f));
            Box(turret, EquipmentMeshRole.Dark, V(2.0f, 1.2f, 0.12f), V(0.0f, 2.4f, -0.9f));
            Box(turret, EquipmentMeshRole.Dark, V(1.2f, 0.9f, 0.12f), V(0.0f, 1.3f, -1.35f));
            Box(turret, EquipmentMeshRole.Marker, V(1.0f, 0.05f, 1.0f), V(0.0f, 1.92f, 0.6f));
            return pivot;
        }

        private static Vector3 Humvee(EquipmentParts hull, EquipmentParts turret, bool avenger)
        {
            Wheels(hull, 2.3f, new[] { -1.6f, 1.5f }, 0.48f);
            Box(hull, EquipmentMeshRole.Camo, V(2.2f, 0.75f, 4.6f), V(0.0f, 1.05f, 0.0f));
            Box(hull, EquipmentMeshRole.Camo, V(2.1f, 0.7f, 1.6f), V(0.0f, 1.75f, -0.2f));
            Box(hull, EquipmentMeshRole.Dark, V(1.9f, 0.5f, 0.08f), V(0.0f, 1.85f, -1.0f));
            var pivot = V(0.0f, 2.1f, 1.3f);
            if (avenger)
            {
                Box(turret, EquipmentMeshRole.Camo, V(1.0f, 0.8f, 1.0f), V(0.0f, 0.4f, 0.0f));
                for (int s = 0; s < SideSigns.Length; s++)
                {
                    float sign = SideSigns[s];
                    Box(turret, EquipmentMeshRole.Camo, V(0.5f, 0.6f, 1.3f), V(sign * 0.85f, 0.75f, -0.1f));
                }
                Box(turret, EquipmentMeshRole.Marker, V(0.6f, 0.05f, 0.6f), V(0.0f, 0.82f, 0.2f));
            }
            else
            {
                Cylinder(turret, EquipmentMeshRole.Camo, 0.05f, 2.5f, V(0.0f, 1.2f, 0.0f), Vector3.zero);
                Box(turret, EquipmentMeshRole.Marker, V(0.7f, 0.05f, 0.7f), V(0.0f, 0.05f, 0.0f));
            }
            return pivot;
        }

        private static EquipmentModelKind Truck(EquipmentParts hull, string id)
        {
            bool eight = id == "us_hemtt";
            float[] positions = eight
                ? new[] { -2.8f, -1.3f, 1.6f, 3.0f }
                : new[] { -2.6f, 1.4f, 2.8f };
            Wheels(hull, 2.5f, positions, 0.6f);
            Box(hull, EquipmentMeshRole.Camo, V(2.5f, 1.7f, 2.0f), V(0.0f, 2.05f, -2.9f));
            Box(hull, EquipmentMeshRole.Dark, V(2.2f, 0.6f, 0.08f), V(0.0f, 2.4f, -3.92f));
            Box(hull, EquipmentMeshRole.Dark, V(2.4f, 0.35f, 6.6f), V(0.0f, 1.1f, 0.2f));
            if (id == "ru_r330zh")
            {
                Box(hull, EquipmentMeshRole.Camo, V(2.5f, 2.2f, 4.4f), V(0.0f, 2.4f, 1.2f));
                Cylinder(hull, EquipmentMeshRole.Dark, 0.08f, 6.0f, V(0.0f, 6.5f, 2.4f), Vector3.zero);
                Box(hull, EquipmentMeshRole.Dark, V(1.8f, 1.4f, 0.1f), V(0.0f, 8.6f, 2.4f));
                Box(hull, EquipmentMeshRole.Marker, V(1.0f, 0.05f, 1.0f), V(0.0f, 3.53f, 0.4f));
                return EquipmentModelKind.Jammer;
            }
            if (id == "ru_fo_team")
            {
                Box(hull, EquipmentMeshRole.Camo, V(2.5f, 1.6f, 4.4f), V(0.0f, 2.1f, 1.2f));
                Cylinder(hull, EquipmentMeshRole.Dark, 0.08f, 4.0f, V(0.8f, 4.9f, 2.6f), Vector3.zero);
                Box(hull, EquipmentMeshRole.Dark, V(0.8f, 0.5f, 0.5f), V(0.8f, 7.0f, 2.6f));
                Box(hull, EquipmentMeshRole.Marker, V(1.0f, 0.05f, 1.0f), V(0.0f, 2.93f, 0.4f));
                return EquipmentModelKind.TruckFo;
            }

            Box(hull, EquipmentMeshRole.Camo, V(2.5f, 1.5f, 4.6f), V(0.0f, 2.0f, 1.3f));
            Box(hull, EquipmentMeshRole.Marker, V(1.0f, 0.05f, 1.0f), V(0.0f, 2.93f, -2.9f));
            return eight ? EquipmentModelKind.Hemtt : EquipmentModelKind.Truck;
        }

        private static void M113(EquipmentParts hull, string elementClass)
        {
            Tracks(hull, 3.0f, 5.2f, 5, false);
            Box(hull, EquipmentMeshRole.Camo, V(2.8f, 1.5f, 4.4f), V(0.0f, 1.45f, 0.3f));
            Wedge(hull, EquipmentMeshRole.Camo, 2.8f, 1.5f, 0.8f, V(0.0f, 1.45f, -2.3f));
            if (elementClass == "cp")
            {
                Box(hull, EquipmentMeshRole.Camo, V(2.4f, 1.7f, 2.2f), V(0.0f, 1.25f, 3.6f));
                for (int i = 0; i < 2; i++)
                {
                    float x = i == 0 ? 1.0f : -1.0f;
                    Cylinder(hull, EquipmentMeshRole.Dark, 0.05f, 5.0f, V(x, 4.7f, 1.8f), Vector3.zero);
                }
            }
            else if (elementClass == "mortar")
            {
                Cylinder(hull, EquipmentMeshRole.Camo, 0.2f, 2.0f, V(0.0f, 2.5f, 1.2f), V(-0.55f, 0.0f, 0.0f));
            }
            Box(hull, EquipmentMeshRole.Marker, V(1.0f, 0.05f, 1.0f), V(0.0f, 2.23f, -0.6f));
        }

        private static void Squad(EquipmentParts hull, int count)
        {
            var offsets = new[]
            {
                new Vector2(0.0f, 0.0f), new Vector2(-3.5f, 1.5f), new Vector2(3.5f, 1.5f),
                new Vector2(-7.0f, 3.0f), new Vector2(7.0f, 3.0f), new Vector2(-1.8f, 4.0f),
                new Vector2(1.8f, 4.2f), new Vector2(-5.0f, -1.0f), new Vector2(5.0f, -1.0f)
            };
            int soldiers = Mathf.Min(count, offsets.Length);
            for (int i = 0; i < soldiers; i++)
            {
                Vector2 offset = offsets[i];
                Box(hull, EquipmentMeshRole.Camo, V(0.55f, 0.9f, 0.35f), V(offset.x, 1.15f, offset.y));
                Box(hull, EquipmentMeshRole.Dark, V(0.5f, 0.75f, 0.3f), V(offset.x, 0.38f, offset.y));
                Sphere(hull, EquipmentMeshRole.Camo, V(0.24f, 0.2f, 0.26f), V(offset.x, 1.78f, offset.y));
                Box(hull, EquipmentMeshRole.Dark, V(0.08f, 0.08f, 0.9f), V(offset.x + 0.25f, 1.25f, offset.y - 0.4f));
            }
            Box(hull, EquipmentMeshRole.Marker, V(0.9f, 0.05f, 0.9f), V(0.0f, 2.3f, 0.0f));
        }

        private static Vector3 Stryker(EquipmentParts hull, EquipmentParts turret, string id)
        {
            Wheels(hull, 2.9f, new[] { -2.4f, -1.0f, 0.9f, 2.3f }, 0.55f);
            Box(hull, EquipmentMeshRole.Camo, V(2.8f, 1.5f, 6.0f), V(0.0f, 1.55f, 0.4f));
            Wedge(hull, EquipmentMeshRole.Camo, 2.8f, 1.0f, 1.2f, V(0.0f, 1.8f, -3.2f));
            Box(hull, EquipmentMeshRole.Camo, V(2.7f, 0.5f, 1.2f), V(0.0f, 1.05f, -3.2f));
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Box(hull, EquipmentMeshRole.Dark, V(0.06f, 1.0f, 5.2f), V(sign * 1.45f, 1.6f, 0.4f));
            }

            var pivot = V(0.2f, 2.3f, -0.4f);
            switch (id)
            {
                case "us_m1296":
                    Box(turret, EquipmentMeshRole.Camo, V(1.6f, 0.6f, 1.8f), V(0.0f, 0.3f, 0.0f));
                    Wedge(turret, EquipmentMeshRole.Camo, 1.6f, 0.6f, 0.5f, V(0.0f, 0.3f, -1.15f));
                    Barrel(turret, 0.08f, 2.6f, V(0.0f, 0.35f, -1.3f));
                    break;
                case "us_m1134":
                    Box(turret, EquipmentMeshRole.Camo, V(0.9f, 0.5f, 0.9f), V(0.0f, 0.25f, 0.0f));
                    Box(turret, EquipmentMeshRole.Camo, V(1.6f, 0.45f, 1.3f), V(0.0f, 0.75f, -0.2f));
                    break;
                case "us_mshorad":
                    Box(turret, EquipmentMeshRole.Camo, V(1.7f, 0.7f, 1.7f), V(0.0f, 0.35f, 0.0f));
                    Barrel(turret, 0.08f, 2.2f, V(0.3f, 0.4f, -0.9f));
                    Box(turret, EquipmentMeshRole.Camo, V(0.45f, 0.5f, 1.3f), V(-1.05f, 0.55f, -0.1f));
                    Box(turret, EquipmentMeshRole.Dark, V(0.9f, 0.5f, 0.08f), V(0.0f, 0.75f, 0.9f));
                    break;
                default:
                    Box(turret, EquipmentMeshRole.Camo, V(0.6f, 0.4f, 0.6f), V(0.0f, 0.2f, 0.0f));
                    Barrel(turret, 0.05f, 1.6f, V(0.0f, 0.3f, -0.3f));
                    break;
            }
            Box(turret, EquipmentMeshRole.Marker, V(0.6f, 0.05f, 0.6f), V(0.0f, 0.02f, 0.6f));
            return pivot;
        }

        private static Vector3 Btr(EquipmentParts hull, EquipmentParts turret)
        {
            Wheels(hull, 2.8f, new[] { -2.5f, -1.2f, 0.9f, 2.2f }, 0.55f);
            Box(hull, EquipmentMeshRole.Camo, V(2.7f, 1.3f, 5.2f), V(0.0f, 1.5f, 0.6f));
            Wedge(hull, EquipmentMeshRole.Camo, 2.7f, 1.3f, 1.8f, V(0.0f, 1.5f, -2.9f));
            var pivot = V(0.0f, 2.2f, -0.6f);
            Sphere(turret, EquipmentMeshRole.Camo, V(0.75f, 0.45f, 0.8f), V(0.0f, 0.15f, 0.0f));
            Barrel(turret, 0.06f, 2.4f, V(0.0f, 0.3f, -0.6f));
            Box(turret, EquipmentMeshRole.Marker, V(0.6f, 0.05f, 0.6f), V(0.0f, 0.6f, 0.2f));
            return pivot;
        }

        private static Vector3 HumveeTow(EquipmentParts hull, EquipmentParts turret)
        {
            Wheels(hull, 2.3f, new[] { -1.6f, 1.5f }, 0.48f);
            Box(hull, EquipmentMeshRole.Camo, V(2.2f, 0.75f, 4.6f), V(0.0f, 1.05f, 0.0f));
            Box(hull, EquipmentMeshRole.Camo, V(2.1f, 0.7f, 1.6f), V(0.0f, 1.75f, -0.2f));
            Box(hull, EquipmentMeshRole.Dark, V(1.9f, 0.5f, 0.08f), V(0.0f, 1.85f, -1.0f));
            var pivot = V(0.0f, 2.1f, 0.3f);
            Cylinder(turret, EquipmentMeshRole.Camo, 0.45f, 0.4f, V(0.0f, 0.2f, 0.0f), Vector3.zero, 10);
            Cylinder(turret, EquipmentMeshRole.Camo, 0.14f, 1.9f, V(0.0f, 0.75f, -0.3f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
            Box(turret, EquipmentMeshRole.Dark, V(0.4f, 0.35f, 0.4f), V(0.35f, 0.75f, 0.1f));
            Box(turret, EquipmentMeshRole.Marker, V(0.6f, 0.05f, 0.6f), V(0.0f, 0.42f, 0.5f));
            return pivot;
        }

        private static Vector3 Booker(EquipmentParts hull, EquipmentParts turret)
        {
            Tracks(hull, 3.5f, 6.6f, 6, true);
            Box(hull, EquipmentMeshRole.Camo, V(3.3f, 0.8f, 5.6f), V(0.0f, 1.2f, 0.4f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.3f, 0.8f, 1.2f, V(0.0f, 1.2f, -3.0f));
            var pivot = V(0.0f, 1.62f, 0.4f);
            Box(turret, EquipmentMeshRole.Camo, V(2.8f, 0.8f, 3.0f), V(0.0f, 0.4f, 0.2f));
            Wedge(turret, EquipmentMeshRole.Camo, 2.6f, 0.8f, 0.9f, V(0.0f, 0.4f, -1.75f));
            Box(turret, EquipmentMeshRole.Dark, V(2.4f, 0.4f, 0.8f), V(0.0f, 0.4f, 2.1f));
            Barrel(turret, 0.1f, 4.6f, V(0.0f, 0.45f, -2.1f));
            Box(turret, EquipmentMeshRole.Camo, V(0.5f, 0.4f, 0.5f), V(0.7f, 1.0f, 0.4f));
            Box(turret, EquipmentMeshRole.Marker, V(1.0f, 0.05f, 0.9f), V(-0.4f, 0.83f, 0.8f));
            return pivot;
        }

        private static void Abv(EquipmentParts hull)
        {
            Tracks(hull, 3.7f, 7.6f, 7, true);
            Box(hull, EquipmentMeshRole.Camo, V(3.5f, 0.75f, 6.6f), V(0.0f, 1.15f, 0.35f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.5f, 0.75f, 1.3f, V(0.0f, 1.15f, -3.6f));
            Box(hull, EquipmentMeshRole.Dark, V(4.2f, 0.9f, 0.4f), V(0.0f, 0.55f, -4.6f), V(0.4f, 0.0f, 0.0f));
            Box(hull, EquipmentMeshRole.Camo, V(2.4f, 1.0f, 2.6f), V(0.0f, 2.0f, 1.2f));
            Box(hull, EquipmentMeshRole.Camo, V(1.2f, 0.8f, 1.5f), V(0.0f, 1.9f, -1.0f));
            Box(hull, EquipmentMeshRole.Marker, V(1.0f, 0.05f, 1.0f), V(0.0f, 2.53f, 1.2f));
        }

        private static Vector3 Bmpt(EquipmentParts hull, EquipmentParts turret)
        {
            Tracks(hull, 3.5f, 6.8f, 6, true);
            Box(hull, EquipmentMeshRole.Camo, V(3.3f, 0.65f, 5.8f), V(0.0f, 1.1f, 0.4f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.3f, 0.65f, 1.3f, V(0.0f, 1.1f, -3.1f));
            var pivot = V(0.0f, 1.45f, 0.2f);
            Box(turret, EquipmentMeshRole.Camo, V(2.2f, 0.55f, 2.4f), V(0.0f, 0.27f, 0.0f));
            Wedge(turret, EquipmentMeshRole.Camo, 2.2f, 0.55f, 0.6f, V(0.0f, 0.27f, -1.5f));
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Barrel(turret, 0.06f, 2.4f, V(sign * 0.3f, 0.45f, -1.4f));
                Box(turret, EquipmentMeshRole.Camo, V(0.4f, 0.4f, 1.8f), V(sign * 1.35f, 0.55f, -0.1f));
            }
            Box(turret, EquipmentMeshRole.Marker, V(0.8f, 0.05f, 0.8f), V(0.0f, 0.57f, 0.5f));
            return pivot;
        }

        private static Vector3 Mtlb(EquipmentParts hull, EquipmentParts turret, bool ur77)
        {
            Tracks(hull, 3.0f, 6.4f, 6, false);
            Box(hull, EquipmentMeshRole.Camo, V(2.8f, 1.0f, 5.4f), V(0.0f, 1.1f, 0.3f));
            Wedge(hull, EquipmentMeshRole.Camo, 2.8f, 1.0f, 1.0f, V(0.0f, 1.1f, -2.9f));
            Box(hull, EquipmentMeshRole.Dark, V(2.0f, 0.4f, 0.08f), V(0.0f, 1.4f, -2.42f));
            var pivot = V(0.0f, 1.6f, 0.9f);
            if (ur77)
            {
                Box(turret, EquipmentMeshRole.Camo, V(2.2f, 0.9f, 2.6f), V(0.0f, 0.6f, 0.0f));
                Box(turret, EquipmentMeshRole.Dark, V(1.8f, 0.3f, 0.1f), V(0.0f, 1.0f, -1.3f));
            }
            else
            {
                Box(turret, EquipmentMeshRole.Camo, V(0.8f, 0.5f, 0.8f), V(0.0f, 0.25f, 0.0f));
                Cylinder(turret, EquipmentMeshRole.Camo, 0.16f, 2.0f, V(0.0f, 0.9f, -0.5f), V(Mathf.PI * 0.45f, 0.0f, 0.0f));
                Box(turret, EquipmentMeshRole.Dark, V(0.45f, 0.4f, 0.4f), V(0.45f, 0.6f, 0.2f));
            }
            Box(turret, EquipmentMeshRole.Marker, V(0.7f, 0.05f, 0.7f), V(0.0f, 0.02f, 0.8f));
            return pivot;
        }

        private static Vector3 Tunguska(EquipmentParts hull, EquipmentParts turret)
        {
            Tracks(hull, 3.3f, 7.0f, 6, false);
            Box(hull, EquipmentMeshRole.Camo, V(3.2f, 1.0f, 6.2f), V(0.0f, 1.2f, 0.2f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.2f, 1.0f, 0.9f, V(0.0f, 1.2f, -3.35f));
            var pivot = V(0.0f, 1.7f, 0.5f);
            Box(turret, EquipmentMeshRole.Camo, V(2.4f, 1.2f, 2.6f), V(0.0f, 0.6f, 0.0f));
            Box(turret, EquipmentMeshRole.Dark, V(1.8f, 0.9f, 0.12f), V(0.0f, 1.8f, 0.9f), V(-0.3f, 0.0f, 0.0f));
            Sphere(turret, EquipmentMeshRole.Dark, V(0.5f, 0.5f, 0.3f), V(0.0f, 0.8f, -1.4f));
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Barrel(turret, 0.07f, 2.2f, V(sign * 1.45f, 0.7f, -0.9f));
                Box(turret, EquipmentMeshRole.Camo, V(0.35f, 0.5f, 1.6f), V(sign * 1.45f, 1.15f, 0.2f));
            }
            Box(turret, EquipmentMeshRole.Marker, V(0.9f, 0.05f, 0.9f), V(0.0f, 1.22f, -0.3f));
            return pivot;
        }

        private static Vector3 Apache(EquipmentParts hull, EquipmentParts turret, Definition definition, string id)
        {
            Box(hull, EquipmentMeshRole.Camo, V(1.2f, 1.4f, 5.0f), V(0.0f, 1.3f, -0.4f));
            Wedge(hull, EquipmentMeshRole.Camo, 1.2f, 1.0f, 1.4f, V(0.0f, 1.1f, -3.6f));
            Box(hull, EquipmentMeshRole.Dark, V(0.9f, 0.7f, 2.2f), V(0.0f, 2.3f, -1.4f));
            Box(hull, EquipmentMeshRole.Camo, V(0.5f, 0.6f, 6.0f), V(0.0f, 1.6f, 4.6f));
            Box(hull, EquipmentMeshRole.Camo, V(0.15f, 1.6f, 1.1f), V(0.0f, 2.4f, 7.4f));
            Box(hull, EquipmentMeshRole.Camo, V(1.8f, 0.1f, 0.7f), V(0.0f, 1.3f, 7.2f));
            Cylinder(hull, EquipmentMeshRole.Dark, 0.9f, 0.08f, V(0.3f, 2.6f, 7.6f), V(0.0f, 0.0f, Mathf.PI * 0.5f), 12);
            Box(hull, EquipmentMeshRole.Camo, V(3.6f, 0.15f, 0.9f), V(0.0f, 1.4f, 0.2f));
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Box(hull, EquipmentMeshRole.Camo, V(0.6f, 0.9f, 1.0f), V(sign * 0.85f, 1.9f, 0.6f));
                Box(hull, EquipmentMeshRole.Dark, V(0.35f, 0.35f, 1.4f), V(sign * 1.2f, 1.05f, 0.1f));
                Cylinder(hull, EquipmentMeshRole.Dark, 0.25f, 1.4f, V(sign * 1.65f, 1.05f, 0.1f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
            }
            Sphere(hull, EquipmentMeshRole.Dark, V(0.35f, 0.35f, 0.35f), V(0.0f, 1.1f, -4.0f));
            Cylinder(hull, EquipmentMeshRole.Dark, 0.1f, 2.2f, V(0.0f, 2.75f, -0.2f), Vector3.zero, 6);
            Box(hull, EquipmentMeshRole.Marker, V(0.6f, 0.05f, 1.2f), V(0.0f, 2.02f, 1.4f));
            Box(hull, EquipmentMeshRole.Dark, V(0.3f, 0.6f, 0.2f), V(-0.4f, 0.3f, -0.6f));
            Box(hull, EquipmentMeshRole.Dark, V(0.3f, 0.6f, 0.2f), V(0.4f, 0.3f, -0.6f));
            Box(turret, EquipmentMeshRole.Dark, V(0.35f, 0.35f, 0.35f), Vector3.zero);
            Barrel(turret, 0.06f, 1.1f, V(0.0f, -0.05f, -0.1f));
            definition.Rotor = Rotor(id + "_Rotor", 4, 7.5f);
            definition.RotorRoles = RotorRoleSet;
            definition.RotorPivot = V(0.0f, 3.85f, -0.2f) * ModelScale;
            return V(0.0f, 0.75f, -2.6f);
        }

        private static Vector3 Ka52(EquipmentParts hull, EquipmentParts turret, Definition definition, string id)
        {
            Box(hull, EquipmentMeshRole.Camo, V(1.8f, 1.6f, 4.6f), V(0.0f, 1.4f, -0.3f));
            Wedge(hull, EquipmentMeshRole.Camo, 1.8f, 1.3f, 1.6f, V(0.0f, 1.25f, -3.4f));
            Box(hull, EquipmentMeshRole.Dark, V(1.6f, 0.7f, 1.6f), V(0.0f, 2.4f, -1.6f));
            Box(hull, EquipmentMeshRole.Camo, V(0.6f, 0.7f, 5.4f), V(0.0f, 1.7f, 4.3f));
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Box(hull, EquipmentMeshRole.Camo, V(0.12f, 1.4f, 1.0f), V(sign * 1.0f, 2.0f, 6.8f));
                Box(hull, EquipmentMeshRole.Camo, V(0.6f, 0.8f, 1.2f), V(sign * 0.95f, 2.1f, 0.4f));
                Box(hull, EquipmentMeshRole.Dark, V(0.4f, 0.4f, 1.6f), V(sign * 1.4f, 1.1f, 0.0f));
                Cylinder(hull, EquipmentMeshRole.Dark, 0.28f, 1.5f, V(sign * 1.9f, 1.1f, 0.0f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
            }
            Box(hull, EquipmentMeshRole.Camo, V(1.9f, 0.1f, 0.6f), V(0.0f, 1.5f, 6.8f));
            Box(hull, EquipmentMeshRole.Camo, V(4.2f, 0.15f, 0.9f), V(0.0f, 1.45f, 0.0f));
            Cylinder(hull, EquipmentMeshRole.Dark, 0.12f, 2.6f, V(0.0f, 3.0f, 0.0f), Vector3.zero, 6);
            Box(hull, EquipmentMeshRole.Marker, V(0.7f, 0.05f, 1.0f), V(0.0f, 2.22f, 1.2f));
            Box(turret, EquipmentMeshRole.Dark, V(0.5f, 0.4f, 0.8f), V(0.6f, 0.0f, 0.0f));
            Barrel(turret, 0.06f, 1.6f, V(0.6f, 0.0f, -0.4f));
            definition.Rotor = Rotor(id + "_Rotor1", 3, 7.2f);
            definition.RotorRoles = RotorRoleSet;
            definition.RotorPivot = V(0.0f, 3.6f, 0.0f) * ModelScale;
            definition.Rotor2 = Rotor(id + "_Rotor2", 3, 7.2f);
            definition.Rotor2Roles = RotorRoleSet;
            definition.Rotor2Pivot = V(0.0f, 4.3f, 0.0f) * ModelScale;
            return V(0.0f, 0.9f, -0.8f);
        }

        private static Mesh Rotor(string name, int blades, float radius)
        {
            var parts = new EquipmentParts();
            for (int i = 0; i < blades; i++)
            {
                float angle = Mathf.PI * 2.0f * i / blades;
                var direction = V(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));
                parts.Add(
                    EquipmentMeshRole.Dark,
                    EquipmentGeometry.Box(V(0.45f, 0.06f, radius) * ModelScale),
                    Matrix4x4.TRS(
                        direction * radius * 0.5f * ModelScale,
                        Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up),
                        Vector3.one));
            }
            Cylinder(parts, EquipmentMeshRole.Dark, 0.35f, 0.3f, Vector3.zero, Vector3.zero);
            EquipmentMeshRole[] roles;
            return EquipmentGeometry.Commit(parts, name, out roles);
        }
    }
}
