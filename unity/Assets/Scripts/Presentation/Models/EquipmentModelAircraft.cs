using UnityEngine;

namespace Echelon.Presentation.Models
{
    // 지원 중대(D-050) 장비: 자주포, 자주 대공포, 고정익 전투기·공격기·수송기, 수송헬기.
    public static partial class EquipmentModelLibrary
    {
        private static Vector3 Howitzer(EquipmentParts hull, EquipmentParts turret, bool russian)
        {
            Tracks(hull, 3.4f, 7.0f, russian ? 6 : 7, false);
            Box(hull, EquipmentMeshRole.Camo, V(3.3f, 0.8f, 6.2f), V(0.0f, 1.15f, 0.3f));
            Wedge(hull, EquipmentMeshRole.Camo, 3.3f, 0.8f, 1.0f, V(0.0f, 1.15f, -3.3f));
            var pivot = V(0.0f, 1.55f, 0.9f);
            Box(turret, EquipmentMeshRole.Camo, V(3.0f, 1.4f, 3.6f), V(0.0f, 0.7f, 0.2f));
            Wedge(turret, EquipmentMeshRole.Camo, 3.0f, 1.4f, 0.8f, V(0.0f, 0.7f, -1.9f));
            float length = russian ? 6.6f : 6.2f;
            Barrel(turret, russian ? 0.13f : 0.12f, length, V(0.0f, 0.75f, -2.2f));
            Cylinder(turret, EquipmentMeshRole.Dark, 0.2f, 0.5f, V(0.0f, 0.75f, -2.2f - length),
                V(Mathf.PI * 0.5f, 0.0f, 0.0f));
            Box(turret, EquipmentMeshRole.Dark, V(0.6f, 0.3f, 0.6f), V(0.9f, 1.55f, 0.6f));
            Box(turret, EquipmentMeshRole.Marker, V(0.9f, 0.05f, 0.9f), V(-0.4f, 1.42f, 0.8f));
            return pivot;
        }

        private static Vector3 Shilka(EquipmentParts hull, EquipmentParts turret)
        {
            Tracks(hull, 3.0f, 6.2f, 6, false);
            Box(hull, EquipmentMeshRole.Camo, V(2.9f, 0.8f, 5.6f), V(0.0f, 1.1f, 0.2f));
            Wedge(hull, EquipmentMeshRole.Camo, 2.9f, 0.8f, 0.8f, V(0.0f, 1.1f, -3.0f));
            var pivot = V(0.0f, 1.5f, 0.2f);
            Box(turret, EquipmentMeshRole.Camo, V(2.6f, 0.9f, 2.6f), V(0.0f, 0.45f, 0.0f));
            for (int row = 0; row < 2; row++)
            {
                for (int s = 0; s < SideSigns.Length; s++)
                {
                    Barrel(turret, 0.05f, 1.8f, V(SideSigns[s] * 0.25f, 0.55f + row * 0.25f, -1.3f));
                }
            }
            Box(turret, EquipmentMeshRole.Dark, V(1.2f, 0.5f, 0.1f), V(0.0f, 1.35f, 1.0f));
            Cylinder(turret, EquipmentMeshRole.Dark, 0.06f, 0.6f, V(0.0f, 1.0f, 1.0f), Vector3.zero, 6);
            Box(turret, EquipmentMeshRole.Marker, V(0.8f, 0.05f, 0.8f), V(0.0f, 0.92f, 0.3f));
            return pivot;
        }

        // 고정익 모델은 원점이 동체 중심이다(고도는 시각 제어가 올린다). 앞은 -Z.
        private static void Fighter(EquipmentParts hull, bool twinTail)
        {
            Box(hull, EquipmentMeshRole.Camo, V(1.1f, 1.0f, 9.0f), V(0.0f, 0.0f, 0.6f));
            Wedge(hull, EquipmentMeshRole.Camo, 1.1f, 1.0f, 2.6f, V(0.0f, 0.0f, -5.0f));
            Box(hull, EquipmentMeshRole.Dark, V(0.6f, 0.45f, 1.8f), V(0.0f, 0.65f, -3.2f));
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Box(hull, EquipmentMeshRole.Camo, V(4.2f, 0.12f, 2.6f), V(sign * 2.4f, 0.0f, 1.4f), V(0.0f, sign * 0.35f, 0.0f));
                Box(hull, EquipmentMeshRole.Camo, V(1.8f, 0.1f, 1.3f), V(sign * 1.3f, 0.0f, 4.6f), V(0.0f, sign * 0.3f, 0.0f));
                if (twinTail)
                {
                    Box(hull, EquipmentMeshRole.Camo, V(0.1f, 1.8f, 1.6f), V(sign * 0.8f, 1.1f, 4.2f), V(0.0f, 0.0f, sign * 0.25f));
                }
            }
            if (!twinTail)
            {
                Box(hull, EquipmentMeshRole.Camo, V(0.12f, 2.0f, 1.8f), V(0.0f, 1.2f, 4.2f));
            }
            Cylinder(hull, EquipmentMeshRole.Dark, 0.45f, 0.6f, V(0.0f, 0.0f, 5.3f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
            Box(hull, EquipmentMeshRole.Marker, V(0.6f, 0.05f, 1.2f), V(0.0f, 0.52f, 1.2f));
        }

        private static void AttackJet(EquipmentParts hull, bool a10)
        {
            Box(hull, EquipmentMeshRole.Camo, V(1.2f, 1.2f, 9.0f), V(0.0f, 0.0f, 0.5f));
            Wedge(hull, EquipmentMeshRole.Camo, 1.2f, 1.2f, 2.0f, V(0.0f, 0.0f, -4.7f));
            Box(hull, EquipmentMeshRole.Dark, V(0.7f, 0.5f, 1.4f), V(0.0f, 0.8f, -3.0f));
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Box(hull, EquipmentMeshRole.Camo, V(5.6f, 0.15f, 1.8f), V(sign * 3.4f, -0.2f, 0.0f));
                Cylinder(hull, EquipmentMeshRole.Dark, 0.15f, 1.6f, V(sign * 3.0f, -0.45f, 0.0f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
                if (a10)
                {
                    Cylinder(hull, EquipmentMeshRole.Dark, 0.55f, 2.4f, V(sign * 1.0f, 0.95f, 2.6f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
                    Box(hull, EquipmentMeshRole.Camo, V(0.12f, 1.6f, 1.2f), V(sign * 1.8f, 0.9f, 4.6f));
                }
                else
                {
                    Cylinder(hull, EquipmentMeshRole.Dark, 0.5f, 3.0f, V(sign * 0.85f, -0.1f, 1.8f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
                    Box(hull, EquipmentMeshRole.Camo, V(1.6f, 0.1f, 1.1f), V(sign * 1.1f, 0.2f, 4.4f));
                }
            }
            if (a10)
            {
                Box(hull, EquipmentMeshRole.Camo, V(4.0f, 0.12f, 1.1f), V(0.0f, 0.2f, 4.6f));
            }
            else
            {
                Box(hull, EquipmentMeshRole.Camo, V(0.12f, 1.8f, 1.6f), V(0.0f, 1.1f, 4.2f));
            }
            Box(hull, EquipmentMeshRole.Marker, V(0.7f, 0.05f, 1.2f), V(0.0f, 0.62f, 0.8f));
        }

        private static void Airlifter(EquipmentParts hull, bool jet)
        {
            Box(hull, EquipmentMeshRole.Camo, V(2.6f, 2.6f, 18.0f), V(0.0f, 0.0f, 0.5f));
            Wedge(hull, EquipmentMeshRole.Camo, 2.6f, 2.6f, 2.8f, V(0.0f, 0.0f, -9.9f));
            Box(hull, EquipmentMeshRole.Dark, V(1.6f, 0.5f, 1.0f), V(0.0f, 1.2f, -8.4f));
            float[] engines = { 3.5f, 7.5f };
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Box(hull, EquipmentMeshRole.Camo, V(11.5f, 0.25f, 3.0f), V(sign * 6.9f, 1.25f, -0.6f), V(0.0f, sign * 0.06f, 0.0f));
                for (int e = 0; e < engines.Length; e++)
                {
                    float x = sign * engines[e];
                    if (jet)
                    {
                        Cylinder(hull, EquipmentMeshRole.Dark, 0.55f, 2.6f, V(x, 0.4f, -1.4f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
                    }
                    else
                    {
                        Cylinder(hull, EquipmentMeshRole.Dark, 0.45f, 2.2f, V(x, 0.9f, -1.6f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
                        Cylinder(hull, EquipmentMeshRole.Dark, 1.5f, 0.08f, V(x, 0.9f, -2.8f), V(Mathf.PI * 0.5f, 0.0f, 0.0f), 12);
                    }
                }
            }
            Box(hull, EquipmentMeshRole.Camo, V(0.25f, 4.0f, 3.2f), V(0.0f, 2.8f, 8.4f));
            if (jet)
            {
                Box(hull, EquipmentMeshRole.Camo, V(8.0f, 0.2f, 2.0f), V(0.0f, 4.7f, 9.0f));
            }
            else
            {
                Box(hull, EquipmentMeshRole.Camo, V(8.5f, 0.2f, 2.2f), V(0.0f, 0.9f, 8.6f));
            }
            Box(hull, EquipmentMeshRole.Marker, V(1.2f, 0.05f, 2.0f), V(0.0f, 1.32f, 2.0f));
        }

        private static void TransportHeli(EquipmentParts hull, Definition definition, string id, bool mi8)
        {
            float width = mi8 ? 2.2f : 1.9f;
            float height = mi8 ? 2.0f : 1.7f;
            float length = mi8 ? 6.0f : 5.2f;
            Box(hull, EquipmentMeshRole.Camo, V(width, height, length), V(0.0f, 1.4f, -0.4f));
            Wedge(hull, EquipmentMeshRole.Camo, width, height * 0.8f, 1.4f, V(0.0f, 1.3f, -length * 0.5f - 1.1f));
            Box(hull, EquipmentMeshRole.Dark, V(width * 0.8f, 0.6f, 1.0f), V(0.0f, 2.0f, -length * 0.5f - 0.4f));
            Box(hull, EquipmentMeshRole.Camo, V(0.6f, 0.7f, 6.0f), V(0.0f, 1.8f, length * 0.5f + 2.4f));
            Box(hull, EquipmentMeshRole.Camo, V(0.15f, 1.6f, 1.2f), V(0.0f, 2.6f, length * 0.5f + 5.2f));
            Cylinder(hull, EquipmentMeshRole.Dark, 0.9f, 0.08f, V(0.3f, 2.8f, length * 0.5f + 5.4f), V(0.0f, 0.0f, Mathf.PI * 0.5f), 12);
            Box(hull, EquipmentMeshRole.Dark, V(1.4f, 0.5f, 1.6f), V(0.0f, 2.5f, -0.6f));
            Cylinder(hull, EquipmentMeshRole.Dark, 0.1f, 0.8f, V(0.0f, 3.0f, -0.4f), Vector3.zero, 6);
            for (int s = 0; s < SideSigns.Length; s++)
            {
                float sign = SideSigns[s];
                Box(hull, EquipmentMeshRole.Dark, V(0.12f, 0.4f, 2.4f), V(sign * 0.9f, 0.4f, -0.4f));
                Box(hull, EquipmentMeshRole.Dark, V(0.08f, 0.9f, 1.2f), V(sign * width * 0.5f, 1.3f, 0.4f));
                if (mi8)
                {
                    Box(hull, EquipmentMeshRole.Camo, V(1.2f, 0.12f, 0.6f), V(sign * 1.6f, 1.2f, 0.2f));
                }
            }
            Box(hull, EquipmentMeshRole.Marker, V(0.7f, 0.05f, 1.2f), V(0.0f, 2.27f, 1.0f));
            definition.Rotor = Rotor(id + "_Rotor", mi8 ? 5 : 4, mi8 ? 8.5f : 7.8f);
            definition.RotorRoles = RotorRoleSet;
            definition.RotorPivot = V(0.0f, 3.4f, -0.4f) * ModelScale;
        }

        private static EquipmentModelKind Aircraft(EquipmentParts hull, string id)
        {
            switch (id)
            {
                case "us_a10c":
                    AttackJet(hull, true);
                    return EquipmentModelKind.A10;
                case "ru_su25sm3":
                    AttackJet(hull, false);
                    return EquipmentModelKind.Su25;
                case "us_c130j":
                    Airlifter(hull, false);
                    return EquipmentModelKind.C130;
                case "ru_il76md":
                    Airlifter(hull, true);
                    return EquipmentModelKind.Il76;
                case "ru_su35s":
                    Fighter(hull, true);
                    return EquipmentModelKind.Su35;
                default:
                    Fighter(hull, false);
                    return EquipmentModelKind.F16;
            }
        }
    }
}
