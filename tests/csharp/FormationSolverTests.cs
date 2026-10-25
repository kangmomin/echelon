using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class FormationSolverTests
    {
        private const double Epsilon = 0.001;

        [Test]
        public void WedgeFourSlotsAndHeadingsMatchGodot()
        {
            FormationDef wedge = Wedge();
            AssertPoints(FormationSolver.LocalOffsets(wedge, 4), new[]
            {
                new SimVec2(0.0, 0.0), new SimVec2(-60.0, -50.0),
                new SimVec2(60.0, -50.0), new SimVec2(0.0, -100.0)
            });
            AssertPoints(FormationSolver.WorldSlots(wedge, 4, new SimVec2(1000.0, 2000.0), 0.0), new[]
            {
                new SimVec2(1000.0, 2000.0), new SimVec2(940.0, 1950.0),
                new SimVec2(1060.0, 1950.0), new SimVec2(1000.0, 1900.0)
            });
            AssertPoints(FormationSolver.WorldSlots(wedge, 4, SimVec2.Zero, SimMath.Pi / 2.0), new[]
            {
                new SimVec2(0.0, 0.0), new SimVec2(-50.0, 60.0),
                new SimVec2(-50.0, -60.0), new SimVec2(-100.0, 0.0)
            });
        }

        [Test]
        public void EveryDefinitionUsesItsThreeAndFourSlotSets()
        {
            foreach (FormationDef formation in DataTestAssets.Catalog.Formations.Values)
            {
                foreach (int count in new[] { 3, 4 })
                {
                    Assert.That(formation.Slots.ContainsKey(count), Is.True, formation.Id + " slot set " + count);
                    Assert.That(formation.Slots[count].Count, Is.EqualTo(count), formation.Id);
                    AssertPoints(FormationSolver.LocalOffsets(formation, count), formation.Slots[count], formation.Id);
                }

                Assert.That(formation.MaxDefinedCount(), Is.GreaterThanOrEqualTo(4), formation.Id);
                if (formation.AllowedMoving && formation.Echelon == "platoon")
                {
                    Assert.That(formation.Slots[4][0], Is.EqualTo(SimVec2.Zero), formation.Id + " guide slot");
                }
            }
        }

        [Test]
        public void EveryDefinitionAppliesOpenForestUrbanAndFallbackScaling()
        {
            foreach (FormationDef formation in DataTestAssets.Catalog.Formations.Values)
            {
                foreach (int count in new[] { 3, 4 })
                {
                    foreach (string terrain in new[] { "open", "forest", "urban" })
                    {
                        double scale = formation.SpacingScale[terrain];
                        List<SimVec2> expected = new List<SimVec2>();
                        foreach (SimVec2 point in formation.Slots[count])
                        {
                            expected.Add(new SimVec2(point.X * (float)scale, point.Y * (float)scale));
                        }

                        AssertPoints(FormationSolver.LocalOffsets(formation, count, terrain), expected,
                            formation.Id + " " + terrain + " " + count);
                    }

                    AssertPoints(FormationSolver.LocalOffsets(formation, count, "swamp"),
                        FormationSolver.LocalOffsets(formation, count, "open"), formation.Id + " fallback");
                }
            }
        }

        [Test]
        public void EveryDefinitionExposesAllModifiersAndDefaults()
        {
            foreach (FormationDef formation in DataTestAssets.Catalog.Formations.Values)
            {
                foreach (string key in FormationDef.ModKeys)
                {
                    Assert.That(formation.Mods.ContainsKey(key), Is.True, formation.Id + " missing " + key);
                }

                foreach (KeyValuePair<string, double> pair in formation.Mods)
                {
                    Assert.That(formation.GetMod(pair.Key), Is.EqualTo(pair.Value), formation.Id + " " + pair.Key);
                }

                Assert.That(formation.GetMod("missing", 0.375), Is.EqualTo(0.375));
            }

            Assert.That(DataTestAssets.Catalog.GetFormation("coil")!.AllowedMoving, Is.False);
            Assert.That(DataTestAssets.Catalog.GetFormation("column")!.GetMod("road_speed"), Is.EqualTo(1.4));
            Assert.That(DataTestAssets.Catalog.GetFormation("echelon_left")!.GetMod("flank_side"), Is.EqualTo(-1.0));
            Assert.That(DataTestAssets.Catalog.GetFormation("echelon_right")!.GetMod("flank_side"), Is.EqualTo(1.0));
        }

        [Test]
        public void SlotSelectionTruncatesAndTrailsDeterministically()
        {
            FormationDef formation = Synthetic();
            AssertPoints(FormationSolver.LocalOffsets(formation, 2), new[]
            {
                new SimVec2(0.0, 0.0), new SimVec2(0.0, -50.0)
            });
            List<SimVec2> trailed = FormationSolver.LocalOffsets(formation, 6);
            Assert.That(trailed.Count, Is.EqualTo(6));
            Assert.That(trailed[4], Is.EqualTo(new SimVec2(0.0, -200.0)));
            Assert.That(trailed[5], Is.EqualTo(new SimVec2(0.0, -250.0)));
            Assert.That(FormationSolver.LocalOffsets(formation, 0), Is.Empty);
            Assert.That(FormationSolver.LocalOffsets(formation, -1), Is.Empty);
            Assert.That(FormationSolver.LocalOffsets(new FormationDef(), 4), Is.Empty);
        }

        [Test]
        public void HeadingVectorsAndInverseTransformsFollowCompassConvention()
        {
            Assert.That(FormationSolver.HeadingFromVector(new SimVec2(0.0, 1.0)), Is.EqualTo(0.0).Within(Epsilon));
            Assert.That(FormationSolver.HeadingFromVector(new SimVec2(1.0, 0.0)), Is.EqualTo(SimMath.Pi / 2.0).Within(Epsilon));
            Assert.That(System.Math.Abs(FormationSolver.HeadingFromVector(new SimVec2(0.0, -1.0))), Is.EqualTo(SimMath.Pi).Within(Epsilon));
            Assert.That(FormationSolver.ForwardVector(0.0), Is.EqualTo(new SimVec2(0.0, 1.0)));
            Assert.That(FormationSolver.RightVector(0.0), Is.EqualTo(new SimVec2(1.0, 0.0)));

            SimVec2 origin = new SimVec2(123.0, -45.0);
            SimVec2 local = new SimVec2(-60.0, -50.0);
            double heading = 37.0 * SimMath.Pi / 180.0;
            SimVec2 world = FormationSolver.LocalToWorld(local, origin, heading);
            SimVec2 roundTrip = FormationSolver.WorldToLocal(world, origin, heading);
            Assert.That(roundTrip.X, Is.EqualTo(local.X).Within(Epsilon));
            Assert.That(roundTrip.Y, Is.EqualTo(local.Y).Within(Epsilon));
        }

        private static FormationDef Wedge()
        {
            FormationDef? wedge = DataTestAssets.Catalog.GetFormation("wedge");
            Assert.That(wedge, Is.Not.Null);
            return wedge!;
        }

        private static FormationDef Synthetic()
        {
            FormationDef formation = new FormationDef();
            formation.Id = "test";
            formation.Echelon = "platoon";
            formation.Slots[4] = new List<SimVec2>
            {
                new SimVec2(0.0, 0.0), new SimVec2(0.0, -50.0),
                new SimVec2(0.0, -100.0), new SimVec2(0.0, -150.0)
            };
            return formation;
        }

        private static void AssertPoints(IReadOnlyList<SimVec2> actual, IReadOnlyList<SimVec2> expected, string? message = null)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), message);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].X, Is.EqualTo(expected[i].X).Within(Epsilon), message + " slot " + i + " x");
                Assert.That(actual[i].Y, Is.EqualTo(expected[i].Y).Within(Epsilon), message + " slot " + i + " y");
            }
        }
    }
}
