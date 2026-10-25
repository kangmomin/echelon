using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Presentation.Models
{
    public static partial class EquipmentModelLibrary
    {
        private static readonly Dictionary<string, Material> SharedMaterials = new Dictionary<string, Material>();
        private static Shader litShader;

        private static Material[] MaterialsFor(EquipmentMeshRole[] roles, string side, bool infantry, bool rotor)
        {
            var materials = new Material[roles.Length];
            for (int i = 0; i < roles.Length; i++)
            {
                materials[i] = rotor ? RotorMaterial() : RoleMaterial(roles[i], side, infantry);
            }
            return materials;
        }

        private static Material RoleMaterial(EquipmentMeshRole role, string side, bool infantry)
        {
            string key = role == EquipmentMeshRole.Camo
                ? "camo|" + side + "|" + (infantry ? "infantry" : "vehicle")
                : role == EquipmentMeshRole.Dark
                    ? "dark"
                    : "marker|" + side;
            Material material;
            if (SharedMaterials.TryGetValue(key, out material))
            {
                return material;
            }

            Color sideColor = SideColor(side);
            float roughness;
            switch (role)
            {
                case EquipmentMeshRole.Camo:
                    Color camo = side == "blue"
                        ? new Color(0.33f, 0.30f, 0.15f)
                        : side == "red"
                            ? new Color(0.17f, 0.26f, 0.11f)
                            : new Color(0.4f, 0.4f, 0.35f);
                    Color color = Color.Lerp(camo, sideColor, 0.15f);
                    if (infantry)
                    {
                        color = Color.Lerp(color, new Color(0.33f, 0.36f, 0.24f), 0.4f);
                    }
                    material = LitMaterial("Camo_" + side + (infantry ? "_Infantry" : string.Empty), color, 0.9f);
                    roughness = 0.9f;
                    break;
                case EquipmentMeshRole.Dark:
                    material = LitMaterial("Dark", new Color(0.07f, 0.07f, 0.06f), 0.85f);
                    roughness = 0.85f;
                    break;
                default:
                    material = LitMaterial("Marker_" + side, sideColor, 0.6f);
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(
                        sideColor.r * 0.35f,
                        sideColor.g * 0.35f,
                        sideColor.b * 0.35f,
                        1.0f));
                    roughness = 0.6f;
                    break;
            }

            material.SetFloat("_Smoothness", 1.0f - roughness);
            SharedMaterials.Add(key, material);
            return material;
        }

        private static Material RotorMaterial()
        {
            const string key = "rotor";
            Material material;
            if (SharedMaterials.TryGetValue(key, out material))
            {
                return material;
            }

            material = LitMaterial("Rotor", new Color(0.08f, 0.08f, 0.08f, 0.85f), 0.7f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);
            material.SetFloat("_AlphaClip", 0.0f);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            SharedMaterials.Add(key, material);
            return material;
        }

        private static Color SideColor(string side)
        {
            if (side == "blue")
            {
                return new Color(0.32f, 0.47f, 0.78f);
            }
            if (side == "red")
            {
                return new Color(0.78f, 0.30f, 0.26f);
            }
            return Color.white;
        }

        private static Material LitMaterial(string name, Color color, float roughness)
        {
            if (litShader == null)
            {
                litShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (litShader == null)
            {
                throw new System.InvalidOperationException(
                    "Universal Render Pipeline/Lit is required to build procedural equipment materials.");
            }

            var material = new Material(litShader);
            material.name = name;
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 1.0f - roughness);
            material.SetFloat("_Metallic", 0.0f);
            return material;
        }
    }
}
