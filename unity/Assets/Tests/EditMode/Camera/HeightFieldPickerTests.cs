using NUnit.Framework;
using UnityEngine;
using Echelon.Unity.Presentation;

namespace Echelon.Unity.Tests.Presentation
{
    public sealed class HeightFieldPickerTests
    {
        private GameObject cameraObject;
        private UnityEngine.Camera camera;
        private RenderTexture renderTexture;
        private MapBounds bounds;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("HeightFieldPickerTest", typeof(UnityEngine.Camera));
            camera = cameraObject.GetComponent<UnityEngine.Camera>();
            renderTexture = new RenderTexture(800, 800, 24);
            camera.targetTexture = renderTexture;
            camera.pixelRect = new Rect(0f, 0f, 800f, 800f);
            camera.aspect = 1f;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = HeightFieldPicker.MaxDistanceMeters;
            bounds = new MapBounds(Vector2.zero, new Vector2(500f, 500f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }

        [Test]
        public void TryPick_FlatField_ReturnsSimEastNorthIntersection()
        {
            PositionCamera(new Vector3(50f, 100f, 50f), 90f);

            bool hit = HeightFieldPicker.TryPick(
                camera,
                new Vector2(400f, 400f),
                bounds,
                _ => 25f,
                out Vector2 simPosition);

            Assert.That(hit, Is.True);
            Assert.That(simPosition.x, Is.EqualTo(50f).Within(0.001f));
            Assert.That(simPosition.y, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void TryPick_SlopedField_RefinesMarchingIntersection()
        {
            const float pitchDegrees = 45f;
            PositionCamera(new Vector3(50f, 100f, 50f), pitchDegrees);

            bool hit = HeightFieldPicker.TryPick(
                camera,
                new Vector2(400f, 400f),
                bounds,
                position => 20f + position.y * 0.1f,
                out Vector2 simPosition);

            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float expectedDistance = 75f / (Mathf.Sin(pitch) + 0.1f * Mathf.Cos(pitch));
            float expectedNorth = 50f + expectedDistance * Mathf.Cos(pitch);
            Assert.That(hit, Is.True);
            Assert.That(simPosition.x, Is.EqualTo(50f).Within(0.001f));
            Assert.That(simPosition.y, Is.EqualTo(expectedNorth).Within(0.01f));
        }

        [Test]
        public void TryPick_OutsideMap_SamplesAndReturnsBoundaryClampedPosition()
        {
            bounds = new MapBounds(Vector2.zero, new Vector2(100f, 100f));
            PositionCamera(new Vector3(50f, 100f, -20f), 90f);
            Vector2 lastSample = Vector2.zero;

            bool hit = HeightFieldPicker.TryPick(
                camera,
                new Vector2(400f, 400f),
                bounds,
                position =>
                {
                    lastSample = position;
                    return 0f;
                },
                out Vector2 simPosition);

            Assert.That(hit, Is.True);
            Assert.That(lastSample.x, Is.EqualTo(50f).Within(0.001f));
            Assert.That(lastSample.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(simPosition.x, Is.EqualTo(50f).Within(0.001f));
            Assert.That(simPosition.y, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Pick_UpwardRay_ReturnsPositiveInfinityWithoutColliders()
        {
            PositionCamera(new Vector3(50f, 100f, 50f), -90f);

            Vector2 result = HeightFieldPicker.Pick(
                camera,
                new Vector2(400f, 400f),
                bounds,
                _ => 0f);

            Assert.That(float.IsPositiveInfinity(result.x), Is.True);
            Assert.That(float.IsPositiveInfinity(result.y), Is.True);
        }

        private void PositionCamera(Vector3 position, float pitchDegrees)
        {
            cameraObject.transform.SetPositionAndRotation(position, Quaternion.Euler(pitchDegrees, 0f, 0f));
        }
    }
}
