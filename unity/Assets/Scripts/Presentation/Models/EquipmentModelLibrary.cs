using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Presentation.Models
{
    public static partial class EquipmentModelLibrary
    {
        public const float ModelScale = 1.3f;
        public static Vector3 Front { get { return Vector3.back; } }

        private static readonly Dictionary<string, Definition> SharedModels = new Dictionary<string, Definition>();
        private static readonly EquipmentMeshRole[] NoRoles = new EquipmentMeshRole[0];
        private static readonly EquipmentMeshRole[] RotorRoleSet = { EquipmentMeshRole.Dark };

        public static EquipmentModelHandle Build(
            string elementId,
            string elementClass,
            string side,
            string loadout = null)
        {
            if (string.IsNullOrWhiteSpace(elementId))
            {
                throw new ArgumentException("An equipment element id is required.", "elementId");
            }
            if (string.IsNullOrWhiteSpace(elementClass))
            {
                throw new ArgumentException("An equipment element class is required.", "elementClass");
            }

            string id = elementId.Trim().ToLowerInvariant();
            string elementType = elementClass.Trim().ToLowerInvariant();
            string faction = string.IsNullOrWhiteSpace(side) ? string.Empty : side.Trim().ToLowerInvariant();
            string selectedLoadout = string.IsNullOrWhiteSpace(loadout) ? string.Empty : loadout.Trim().ToLowerInvariant();
            string effectiveLoadout = id == "us_m1a2_sepv3" && selectedLoadout == "sepv3_tusk"
                ? "sepv3_tusk"
                : string.Empty;
            string cacheKey = id + "\u001f" + elementType + "\u001f" + effectiveLoadout;

            Definition definition;
            if (!SharedModels.TryGetValue(cacheKey, out definition))
            {
                definition = CreateDefinition(id, elementType, effectiveLoadout);
                SharedModels.Add(cacheKey, definition);
            }

            bool infantry = elementType == "inf_squad";
            var root = new GameObject("Equipment_" + id);
            Transform tilt = CreateEmpty("Tilt", root.transform, Vector3.zero);
            var handle = new EquipmentModelHandle
            {
                ElementId = id,
                ElementClass = elementType,
                Side = faction,
                Kind = definition.Kind,
                Root = root,
                Tilt = tilt,
                HullMesh = definition.Hull,
                TurretMesh = definition.Turret,
                RotorMesh = definition.Rotor,
                Rotor2Mesh = definition.Rotor2,
                HullRoles = Array.AsReadOnly(definition.HullRoles),
                TurretRoles = Array.AsReadOnly(definition.TurretRoles),
                RotorRoles = Array.AsReadOnly(definition.RotorRoles),
                Rotor2Roles = Array.AsReadOnly(definition.Rotor2Roles)
            };

            handle.Hull = CreateMeshPart(
                "Hull", tilt, definition.Hull, definition.HullRoles, Vector3.zero, faction, infantry, false);
            handle.HullRenderer = handle.Hull.GetComponent<MeshRenderer>();

            if (definition.Turret != null)
            {
                handle.Turret = CreateMeshPart(
                    "Turret", tilt, definition.Turret, definition.TurretRoles,
                    definition.TurretPivot, faction, infantry, false);
                handle.TurretRenderer = handle.Turret.GetComponent<MeshRenderer>();
            }
            if (definition.Rotor != null)
            {
                handle.Rotor = CreateMeshPart(
                    "Rotor", tilt, definition.Rotor, definition.RotorRoles,
                    definition.RotorPivot, faction, false, true);
                handle.RotorRenderer = handle.Rotor.GetComponent<MeshRenderer>();
            }
            if (definition.Rotor2 != null)
            {
                handle.Rotor2 = CreateMeshPart(
                    "Rotor2", tilt, definition.Rotor2, definition.Rotor2Roles,
                    definition.Rotor2Pivot, faction, false, true);
                handle.Rotor2Renderer = handle.Rotor2.GetComponent<MeshRenderer>();
            }
            return handle;
        }

        private static Transform CreateEmpty(string name, Transform parent, Vector3 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            return gameObject.transform;
        }

        private static Transform CreateMeshPart(
            string name,
            Transform parent,
            Mesh mesh,
            EquipmentMeshRole[] roles,
            Vector3 position,
            string side,
            bool infantry,
            bool rotor)
        {
            Transform transform = CreateEmpty(name, parent, position);
            var filter = transform.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = transform.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = MaterialsFor(roles, side, infantry, rotor);
            return transform;
        }

        private static Definition CreateDefinition(string id, string elementClass, string loadout)
        {
            var hull = new EquipmentParts();
            var turret = new EquipmentParts();
            var definition = new Definition();
            Vector3 turretPivot = Vector3.zero;

            if (id == "us_ah64e")
            {
                definition.Kind = EquipmentModelKind.Apache;
                turretPivot = Apache(hull, turret, definition, id);
            }
            else if (id == "ru_ka52")
            {
                definition.Kind = EquipmentModelKind.Ka52;
                turretPivot = Ka52(hull, turret, definition, id);
            }
            else if (id == "us_m1126" || id == "us_m1296" || id == "us_m1134" || id == "us_mshorad")
            {
                definition.Kind = StrykerKind(id);
                turretPivot = Stryker(hull, turret, id);
            }
            else if (id == "ru_btr82a")
            {
                definition.Kind = EquipmentModelKind.Btr82A;
                turretPivot = Btr(hull, turret);
            }
            else if (id == "us_m1167")
            {
                definition.Kind = EquipmentModelKind.HumveeTow;
                turretPivot = HumveeTow(hull, turret);
            }
            else if (id == "us_m10_booker")
            {
                definition.Kind = EquipmentModelKind.Booker;
                turretPivot = Booker(hull, turret);
            }
            else if (id == "us_m1150_abv")
            {
                definition.Kind = EquipmentModelKind.Abv;
                Abv(hull);
            }
            else if (id == "ru_bmpt")
            {
                definition.Kind = EquipmentModelKind.Bmpt;
                turretPivot = Bmpt(hull, turret);
            }
            else if (id == "ru_shturm_s" || id == "ru_ur77")
            {
                definition.Kind = id == "ru_ur77" ? EquipmentModelKind.Ur77 : EquipmentModelKind.ShturmS;
                turretPivot = Mtlb(hull, turret, id == "ru_ur77");
            }
            else if (id == "ru_tunguska")
            {
                definition.Kind = EquipmentModelKind.Tunguska;
                turretPivot = Tunguska(hull, turret);
            }
            else if (id == "ru_t80bvm")
            {
                definition.Kind = EquipmentModelKind.T80;
                turretPivot = T90(hull, turret, false);
                Box(hull, EquipmentMeshRole.Dark, V(2.4f, 0.3f, 0.6f), V(0.0f, 1.5f, 3.1f));
            }
            else if (elementClass == "inf_squad" && id.Contains("sniper"))
            {
                definition.Kind = EquipmentModelKind.InfantrySniper;
                Squad(hull, 2);
            }
            else if (elementClass == "inf_squad" && id == "ru_at_squad")
            {
                definition.Kind = EquipmentModelKind.InfantryAt;
                Squad(hull, 3);
                Cylinder(hull, EquipmentMeshRole.Dark, 0.12f, 1.4f, V(1.2f, 0.9f, -0.6f), V(Mathf.PI * 0.5f, 0.0f, 0.0f));
            }
            else if (elementClass == "inf_squad" && id.Contains("hq_team"))
            {
                definition.Kind = EquipmentModelKind.InfantryHq;
                Squad(hull, 5);
            }
            else if (id.StartsWith("us_m1a2", StringComparison.Ordinal))
            {
                bool tusk = id.EndsWith("tusk", StringComparison.Ordinal) || loadout == "sepv3_tusk";
                definition.Kind = tusk ? EquipmentModelKind.AbramsTusk : EquipmentModelKind.Abrams;
                turretPivot = Abrams(hull, turret, tusk);
            }
            else if (id.StartsWith("ru_t90", StringComparison.Ordinal) || id.StartsWith("ru_t72", StringComparison.Ordinal))
            {
                bool t72 = id.StartsWith("ru_t72", StringComparison.Ordinal);
                definition.Kind = t72 ? EquipmentModelKind.T72 : EquipmentModelKind.T90;
                turretPivot = T90(hull, turret, t72);
            }
            else if (id == "us_m2a3" || id == "us_m3a3" || id == "us_m7_fist")
            {
                definition.Kind = id == "us_m7_fist" ? EquipmentModelKind.BradleyFist : EquipmentModelKind.Bradley;
                turretPivot = Bradley(hull, turret, id == "us_m7_fist");
            }
            else if (id == "ru_bmp3" || id == "ru_brm3k")
            {
                definition.Kind = id == "ru_brm3k" ? EquipmentModelKind.Brm3K : EquipmentModelKind.Bmp3;
                turretPivot = Bmp3(hull, turret, id == "ru_brm3k");
            }
            else if (id == "ru_bmp2m")
            {
                definition.Kind = EquipmentModelKind.Bmp2;
                turretPivot = Bmp2(hull, turret);
            }
            else if (id == "ru_fo_team" || id == "ru_bmp1ksh_cp")
            {
                definition.Kind = id == "ru_bmp1ksh_cp" ? EquipmentModelKind.Bmp1Command : EquipmentModelKind.Bmp1Recon;
                turretPivot = Bmp1Variant(hull, turret, id == "ru_bmp1ksh_cp");
            }
            else if (id == "ru_2s9")
            {
                definition.Kind = EquipmentModelKind.Nona;
                turretPivot = Nona(hull, turret);
            }
            else if (id == "ru_tor_m2")
            {
                definition.Kind = EquipmentModelKind.Tor;
                turretPivot = Tor(hull, turret);
            }
            else if (id == "us_avenger" || id == "us_jtac")
            {
                definition.Kind = id == "us_avenger" ? EquipmentModelKind.HumveeAvenger : EquipmentModelKind.HumveeJtac;
                turretPivot = Humvee(hull, turret, id == "us_avenger");
            }
            else if (elementClass == "truck" || elementClass == "ew" || id == "ru_fo_team")
            {
                definition.Kind = Truck(hull, id);
            }
            else if (elementClass == "inf_squad")
            {
                definition.Kind = EquipmentModelKind.InfantrySquad;
                Squad(hull, 9);
            }
            else if (elementClass == "mortar" || elementClass == "cp")
            {
                definition.Kind = elementClass == "cp" ? EquipmentModelKind.M113Cp : EquipmentModelKind.M113Mortar;
                M113(hull, elementClass);
            }
            else
            {
                definition.Kind = EquipmentModelKind.M113Apc;
                M113(hull, "apc");
            }

            definition.Hull = EquipmentGeometry.Commit(hull, id + "_Hull", out definition.HullRoles);
            if (!turret.IsEmpty)
            {
                definition.Turret = EquipmentGeometry.Commit(turret, id + "_Turret", out definition.TurretRoles);
                definition.TurretPivot = turretPivot * ModelScale;
            }
            return definition;
        }

        private static EquipmentModelKind StrykerKind(string id)
        {
            switch (id)
            {
                case "us_m1296": return EquipmentModelKind.StrykerDragoon;
                case "us_m1134": return EquipmentModelKind.StrykerAtgm;
                case "us_mshorad": return EquipmentModelKind.StrykerMshorad;
                default: return EquipmentModelKind.StrykerIcv;
            }
        }

        private sealed class Definition
        {
            internal EquipmentModelKind Kind;
            internal Mesh Hull;
            internal EquipmentMeshRole[] HullRoles = NoRoles;
            internal Mesh Turret;
            internal EquipmentMeshRole[] TurretRoles = NoRoles;
            internal Vector3 TurretPivot;
            internal Mesh Rotor;
            internal EquipmentMeshRole[] RotorRoles = NoRoles;
            internal Vector3 RotorPivot;
            internal Mesh Rotor2;
            internal EquipmentMeshRole[] Rotor2Roles = NoRoles;
            internal Vector3 Rotor2Pivot;
        }
    }
}
