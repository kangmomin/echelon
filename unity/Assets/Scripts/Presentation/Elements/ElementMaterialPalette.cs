using System;
using System.Collections.Generic;
using Echelon.Presentation.Models;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements
{
    internal enum ElementAppearance
    {
        Unknown = -1,
        Normal = 0,
        Damaged = 1,
        Dead = 2
    }

    internal static class ElementMaterialPalette
    {
        private static readonly Color DeadColor = new Color(0.10f, 0.09f, 0.08f);
        private static readonly Color DamagedTint = new Color(0.55f, 0.45f, 0.2f);
        private static readonly Color InfantryTint = new Color(0.33f, 0.36f, 0.24f);
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Shader litShader;

        internal static Material Get(EquipmentMeshRole role, string side, bool infantry, ElementAppearance appearance)
        {
            string normalizedSide = string.IsNullOrWhiteSpace(side) ? string.Empty : side.Trim().ToLowerInvariant();
            string key = ((int)appearance).ToString() + "|" + ((int)role).ToString() + "|"
                + normalizedSide + "|" + (infantry ? "infantry" : "vehicle");
            Material material;
            if (Materials.TryGetValue(key, out material))
                return material;

            float roughness;
            switch (role)
            {
                case EquipmentMeshRole.Camo:
                    Color color = DeadColor;
                    if (appearance == ElementAppearance.Damaged)
                    {
                        Color camo = normalizedSide == "blue"
                            ? new Color(0.33f, 0.30f, 0.15f)
                            : normalizedSide == "red"
                                ? new Color(0.17f, 0.26f, 0.11f)
                                : new Color(0.4f, 0.4f, 0.35f);
                        color = Darken(Color.Lerp(camo, DamagedTint, 0.4f), 0.25f);
                    }
                    if (infantry)
                        color = Color.Lerp(color, InfantryTint, 0.4f);
                    material = Create("Camo_" + appearance, color, 0.9f);
                    roughness = 0.9f;
                    break;
                case EquipmentMeshRole.Dark:
                    material = Create("Dark_Dead", Darken(DeadColor, 0.4f), 0.85f);
                    roughness = 0.85f;
                    break;
                default:
                    material = Create("Marker_Dead", DeadColor, 0.6f);
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                    roughness = 0.6f;
                    break;
            }

            material.SetFloat("_Smoothness", 1.0f - roughness);
            Materials.Add(key, material);
            return material;
        }

        private static Color Darken(Color color, float amount)
        {
            float scale = 1.0f - amount;
            return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
        }

        private static Material Create(string name, Color color, float roughness)
        {
            if (litShader == null)
                litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
                throw new InvalidOperationException("Universal Render Pipeline/Lit is required for element condition materials.");

            var material = new Material(litShader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 1.0f - roughness);
            material.SetFloat("_Metallic", 0.0f);
            return material;
        }
    }
}
