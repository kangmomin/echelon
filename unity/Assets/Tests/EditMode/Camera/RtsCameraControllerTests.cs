using NUnit.Framework;
using UnityEngine;
using Echelon.Unity.Presentation;

namespace Echelon.Unity.Tests.Presentation
{
    public sealed class RtsCameraControllerTests
    {
        private GameObject rigObject;
        private UnityEngine.Camera camera;
        private RtsCameraController controller;

        [SetUp]
        public void SetUp()
        {
            rigObject = new GameObject("RtsCameraTest", typeof(UnityEngine.Camera));
            camera = rigObject.GetComponent<UnityEngine.Camera>();
            controller = rigObject.AddComponent<RtsCameraController>();
            controller.TargetCamera = camera;
            controller.Setup(
                new MapBounds(Vector2.zero, new Vector2(10000f, 10000f)),
                _ => 10f,
                new Vector2(5000f, 5000f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rigObject);
        }

        [Test]
        public void TargetCamera_UsesGodotLensSettings()
        {
            Assert.That(camera.fieldOfView, Is.EqualTo(50f));
            Assert.That(camera.nearClipPlane, Is.EqualTo(1f));
            Assert.That(camera.farClipPlane, Is.EqualTo(30000f));
        }

        [Test]
        public void Zoom_UsesMultiplicativeStepAndStopsAtDistanceLimits()
        {
            controller.Distance = 1150f;
            controller.ZoomIn();
            Assert.That(controller.Distance, Is.EqualTo(1000f).Within(0.001f));
            controller.ZoomOut();
            Assert.That(controller.Distance, Is.EqualTo(1150f).Within(0.001f));

            controller.Distance = RtsCameraController.MinDistance;
            controller.ZoomIn();
            Assert.That(controller.Distance, Is.EqualTo(RtsCameraController.MinDistance));

            controller.Distance = RtsCameraController.MaxDistance;
            controller.ZoomOut();
            Assert.That(controller.Distance, Is.EqualTo(RtsCameraController.MaxDistance));
        }

        [Test]
        public void DistanceAndPitch_SettersClampToPlayableRange()
        {
            controller.Distance = -100f;
            Assert.That(controller.Distance, Is.EqualTo(RtsCameraController.MinDistance));
            controller.Distance = 10000f;
            Assert.That(controller.Distance, Is.EqualTo(RtsCameraController.MaxDistance));

            controller.PitchDegrees = -20f;
            Assert.That(controller.PitchDegrees, Is.EqualTo(RtsCameraController.MinPitchDegrees));
            controller.PitchDegrees = 120f;
            Assert.That(controller.PitchDegrees, Is.EqualTo(RtsCameraController.MaxPitchDegrees));
        }

        [Test]
        public void Focus_ClampsToMapAndConvertsBetweenSimAndWorld()
        {
            controller.Bounds = new MapBounds(new Vector2(10f, 20f), new Vector2(100f, 200f));
            controller.FocusOnSim(new Vector2(-50f, 999f));
            Assert.That(controller.SimFocus, Is.EqualTo(new Vector2(10f, 220f)));

            controller.WorldFocus = new Vector3(55f, 999f, 120f);
            Assert.That(controller.SimFocus, Is.EqualTo(new Vector2(55f, 120f)));
            Assert.That(controller.WorldFocus, Is.EqualTo(new Vector3(55f, 10f, 120f)));
        }

        [Test]
        public void Pan_ScalesWithDistanceAndNormalizesDiagonalMovement()
        {
            controller.Distance = 1000f;
            controller.YawDegrees = 0f;
            controller.Pan(Vector2.up, 0.5f);
            Assert.That(controller.SimFocus.x, Is.EqualTo(5000f).Within(0.001f));
            Assert.That(controller.SimFocus.y, Is.EqualTo(5550f).Within(0.001f));

            controller.FocusOnSim(new Vector2(5000f, 5000f));
            controller.Pan(new Vector2(2f, 2f), 1f);
            float expected = 5000f + 1100f / Mathf.Sqrt(2f);
            Assert.That(controller.SimFocus.x, Is.EqualTo(expected).Within(0.001f));
            Assert.That(controller.SimFocus.y, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void Orbit_WrapsYawAndClampsPitch()
        {
            controller.YawDegrees = 179f;
            controller.Orbit(new Vector2(10f, 0f));
            Assert.That(controller.YawDegrees, Is.LessThan(-178f));
            Assert.That(controller.YawDegrees, Is.GreaterThan(-180f));

            controller.Orbit(new Vector2(0f, -10000f));
            Assert.That(controller.PitchDegrees, Is.EqualTo(RtsCameraController.MaxPitchDegrees));
            controller.Orbit(new Vector2(0f, 10000f));
            Assert.That(controller.PitchDegrees, Is.EqualTo(RtsCameraController.MinPitchDegrees));
        }

        [Test]
        public void Apply_PlacesUnityCameraBehindAndAboveSimFocus()
        {
            controller.FocusOnSim(new Vector2(100f, 200f));
            controller.Distance = 200f;
            controller.YawDegrees = 0f;
            controller.PitchDegrees = 60f;

            float pitch = 60f * Mathf.Deg2Rad;
            var expected = new Vector3(
                100f,
                10f + 200f * Mathf.Sin(pitch),
                200f - 200f * Mathf.Cos(pitch));
            Assert.That(camera.transform.position, Is.EqualTo(expected));
            Vector3 expectedForward = (new Vector3(100f, 10f, 200f) - expected).normalized;
            Assert.That(Vector3.Angle(camera.transform.forward, expectedForward), Is.LessThan(0.001f));
            Assert.That(controller.HeightAboveGround, Is.EqualTo(200f * Mathf.Sin(pitch)).Within(0.001f));
        }
    }
}
