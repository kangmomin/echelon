using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements.Tests
{
    internal static class ElementVisualTestData
    {
        private static readonly string[] Equipment =
        {
            "us_sniper_team|inf_squad",
            "us_sapper_squad|inf_squad",
            "us_m7_fist|fo",
            "us_mshorad|aa",
            "us_m3a3|recon",
            "us_m2a3|ifv",
            "us_m1a2_sepv3_tusk|tank",
            "us_m1a2_sepv3|tank",
            "us_m1167|at",
            "us_m1296|ifv",
            "us_m1150_abv|eng",
            "us_m1134|at",
            "us_m1126|apc",
            "us_m10_booker|tank",
            "us_jtac|fo",
            "us_m1064|mortar",
            "us_m1068_cp|cp",
            "us_inf_squad|inf_squad",
            "us_inf_squad_jav2|inf_squad",
            "us_hq_team|inf_squad",
            "us_hemtt|truck",
            "us_avenger|aa",
            "us_ah64e|heli",
            "ru_ur77|eng",
            "ru_tunguska|aa",
            "ru_t90m|tank",
            "ru_tor_m2|aa",
            "ru_t80bvm|tank",
            "ru_t72b3|tank",
            "ru_sniper_team|inf_squad",
            "ru_shturm_s|at",
            "ru_r330zh|ew",
            "ru_sapper_squad|inf_squad",
            "ru_msr_squad|inf_squad",
            "ru_kamaz|truck",
            "ru_ka52|heli",
            "ru_fo_team|fo",
            "ru_hq_team|inf_squad",
            "ru_btr82a|apc",
            "ru_brm3k|recon",
            "ru_bmp3|ifv",
            "ru_bmpt|ifv",
            "ru_bmp1ksh_cp|cp",
            "ru_bmp2m|ifv",
            "ru_at_squad|inf_squad",
            "ru_2s9|mortar",
            "us_m109a7|artillery",
            "ru_2s19|artillery",
            "ru_zsu234|aa",
            "us_f16c|plane",
            "ru_su35s|plane",
            "us_a10c|plane",
            "ru_su25sm3|plane",
            "us_c130j|plane",
            "ru_il76md|plane",
            "us_uh60m|heli",
            "ru_mi8amtsh|heli"
        };

        internal static ElementVisualState State(
            int id = 1,
            string elementId = "us_m1a2_sepv3",
            string elementClass = "tank",
            string side = "blue")
        {
            return new ElementVisualState
            {
                Id = id,
                ElementId = elementId,
                ElementClass = elementClass,
                Side = side,
                OnMap = true,
                TargetId = ElementVisualState.NoTarget,
                LastFiredTick = ElementVisualState.NoTick,
                LastSeenTick = ElementVisualState.NoTick,
                TerrainNormal = Vector3.up
            };
        }

        internal static List<ElementVisualState> AllEquipment()
        {
            var states = new List<ElementVisualState>(Equipment.Length);
            for (int i = 0; i < Equipment.Length; i++)
            {
                string[] parts = Equipment[i].Split('|');
                string side = parts[0].StartsWith("us_") ? "blue" : "red";
                ElementVisualState state = State(i + 1, parts[0], parts[1], side);
                state.Position = new Vector2(i * 10.0f, 0.0f);
                state.PreviousPosition = state.Position;
                if (state.IsHelicopter)
                    state.Altitude = 25.0f;
                if (state.IsPlane)
                    state.Altitude = 120.0f;
                states.Add(state);
            }
            return states;
        }

        internal static ElementViewManager Manager(out GameObject owner)
        {
            owner = new GameObject("ElementViewManagerTests");
            return owner.AddComponent<ElementViewManager>();
        }

        internal static void AssertVector(Vector3 expected, Vector3 actual, float tolerance = 0.0001f)
        {
            NUnit.Framework.Assert.AreEqual(expected.x, actual.x, tolerance);
            NUnit.Framework.Assert.AreEqual(expected.y, actual.y, tolerance);
            NUnit.Framework.Assert.AreEqual(expected.z, actual.z, tolerance);
        }

        internal static void AssertColor(Color expected, Color actual, float tolerance = 0.0001f)
        {
            NUnit.Framework.Assert.AreEqual(expected.r, actual.r, tolerance);
            NUnit.Framework.Assert.AreEqual(expected.g, actual.g, tolerance);
            NUnit.Framework.Assert.AreEqual(expected.b, actual.b, tolerance);
            NUnit.Framework.Assert.AreEqual(expected.a, actual.a, tolerance);
        }
    }
}
