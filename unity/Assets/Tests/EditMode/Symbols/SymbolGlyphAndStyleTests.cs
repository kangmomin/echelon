using Echelon.Unity.Presentation.Symbols;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.Tests.Symbols
{
    public sealed class SymbolGlyphAndStyleTests
    {
        [TestCase("tank")]
        [TestCase("mech")]
        [TestCase("inf")]
        [TestCase("recon")]
        [TestCase("mortar")]
        [TestCase("supply")]
        [TestCase("fo")]
        [TestCase("aa")]
        [TestCase("wheeled")]
        [TestCase("at")]
        [TestCase("sniper")]
        [TestCase("engineer")]
        [TestCase("aviation")]
        public void EveryCurrentVectorBranch_ProducesRealGeometry(string branch)
        {
            var vertices = new VertexHelper();
            try
            {
                var batch = new SymbolMeshBatch(vertices);
                bool drawn = SymbolGlyphRenderer.Draw(batch, new Rect(-15f, -10f, 30f, 20f), branch, SymbolStyle.FrameColor);
                Assert.That(drawn, Is.True, branch);
                Assert.That(batch.IndexCount, Is.GreaterThan(0), branch);
            }
            finally
            {
                vertices.Dispose();
            }
        }

        [TestCase("hq", "HQ")]
        [TestCase("cp", "CP")]
        [TestCase("ew", "EW")]
        [TestCase("unknown", "UN")]
        public void TextGlyphs_MatchGodotAbbreviations(string branch, string expected)
        {
            Assert.That(SymbolGlyphRenderer.GetFallbackText(branch), Is.EqualTo(expected));
            Assert.That(SymbolGlyphRenderer.IsVectorGlyph(SymbolGlyphRenderer.Classify(branch)), Is.False);
        }

        [Test]
        public void SideAndOwnerColors_MatchGodotPalette()
        {
            Assert.That(SymbolStyle.SideColor("blue", "blue"), Is.EqualTo(SymbolStyle.FriendlyFill));
            Assert.That(SymbolStyle.SideColor("red", "blue"), Is.EqualTo(SymbolStyle.HostileFill));
            Assert.That(SymbolStyle.OwnerColor(string.Empty, "blue"), Is.EqualTo(SymbolStyle.NeutralColor));
            Assert.That(SymbolStyle.OwnerColor("blue", "blue"), Is.EqualTo(SymbolStyle.FriendlyFill));
        }

        [Test]
        public void ContactAlpha_DistinguishesCurrentReportedAndStale()
        {
            Assert.That(SymbolStyle.ApplyContact(SymbolStyle.FriendlyFill, SymbolContactState.Current).a,
                Is.EqualTo(SymbolStyle.FriendlyFill.a));
            Assert.That(SymbolStyle.ApplyContact(SymbolStyle.HostileFill, SymbolContactState.Reported).a,
                Is.EqualTo(SymbolStyle.HostileFill.a));
            Assert.That(SymbolStyle.ApplyContact(SymbolStyle.HostileFill, SymbolContactState.Stale).a,
                Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(SymbolStyle.ApplyContact(SymbolStyle.HostileFill, SymbolContactState.Reported, true).a,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(SymbolStyle.ApplyContact(SymbolStyle.HostileFill, SymbolContactState.Hidden).a,
                Is.Zero);
        }

        [Test]
        public void StrengthAndCommsColors_FollowGodotMapping()
        {
            AssertColor(SymbolStyle.StrengthColor(0f), new Color(1f, 0.4f, 0.2f, 1f));
            AssertColor(SymbolStyle.StrengthColor(1f), new Color(0f, 0.9f, 0.2f, 1f));
            Assert.That(SymbolStyle.CommsBars(SymbolCommsState.Good), Is.EqualTo(3));
            Assert.That(SymbolStyle.CommsBars(SymbolCommsState.Poor), Is.EqualTo(2));
            Assert.That(SymbolStyle.CommsBars(SymbolCommsState.Cut), Is.EqualTo(1));
            AssertColor(SymbolStyle.CommsColor(SymbolCommsState.Cut), new Color(1f, 0.3f, 0.3f, 1f));
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }
    }
}
