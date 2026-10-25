using Echelon.Unity.UI.Minimap;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Tests.Symbols
{
    public sealed class MinimapMappingTests
    {
        private static readonly Vector2 MapSize = new Vector2(4000f, 4000f);
        private static readonly Rect LocalRect = new Rect(-105f, -105f, 210f, 210f);

        [Test]
        public void PixelMapping_UsesTopLeftTextureOrigin()
        {
            AssertVector(MinimapMapping.MapToPixels(new Vector2(0f, 4000f), MapSize), Vector2.zero);
            AssertVector(MinimapMapping.MapToPixels(new Vector2(4000f, 0f), MapSize), new Vector2(210f, 210f));
            AssertVector(MinimapMapping.MapToPixels(new Vector2(2000f, 2000f), MapSize), new Vector2(105f, 105f));
            AssertVector(MinimapMapping.PixelsToMap(new Vector2(0f, 210f), MapSize), new Vector2(0f, 0f));
            AssertVector(MinimapMapping.PixelsToMap(new Vector2(210f, 0f), MapSize), new Vector2(4000f, 4000f));
        }

        [Test]
        public void LocalMapping_UsesBottomLeftUiOriginAndRoundTrips()
        {
            AssertVector(MinimapMapping.MapToLocal(new Vector2(0f, 4000f), MapSize, LocalRect), new Vector2(-105f, 105f));
            AssertVector(MinimapMapping.MapToLocal(new Vector2(4000f, 0f), MapSize, LocalRect), new Vector2(105f, -105f));
            Vector2 original = new Vector2(1234f, 2345f);
            Vector2 local = MinimapMapping.MapToLocal(original, MapSize, LocalRect);
            AssertVector(MinimapMapping.LocalToMap(local, MapSize, LocalRect), original);
        }

        [Test]
        public void LocalMapping_ClampsDragPositionsToTheMap()
        {
            Vector2 result = MinimapMapping.LocalToMap(new Vector2(-1000f, 1000f), MapSize, LocalRect);
            AssertVector(result, new Vector2(0f, 4000f));
        }

        [Test]
        public void CameraRectangle_UsesGodotDistanceApproximation()
        {
            var camera = new MinimapCameraSnapshot
            {
                Focus = new Vector2(2000f, 2000f),
                Distance = 1000f
            };
            Rect rect = MinimapMapping.CameraRectangle(camera, MapSize, LocalRect);
            float expectedHalf = 1000f * 0.6f / 4000f * 210f;
            Assert.That(rect.center.x, Is.Zero.Within(0.0001f));
            Assert.That(rect.center.y, Is.Zero.Within(0.0001f));
            Assert.That(rect.width, Is.EqualTo(expectedHalf * 2f).Within(0.0001f));
            Assert.That(rect.height, Is.EqualTo(expectedHalf * 2f).Within(0.0001f));
        }

        [Test]
        public void TerrainSampling_UsesPixelCentersAndInvertedY()
        {
            Vector2 sample = MinimapMapping.TerrainSamplePosition(0, 0, MapSize);
            AssertVector(sample, new Vector2(0.5f / 210f * 4000f, (1f - 0.5f / 210f) * 4000f));
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        }
    }
}
