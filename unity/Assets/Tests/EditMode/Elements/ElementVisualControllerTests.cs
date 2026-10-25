using System.Collections.Generic;
using Echelon.Presentation.Models;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements.Tests
{
    public sealed class ElementVisualControllerTests
    {
        [Test]
        public void GroundVehicle_InterpolatesIntoUnityCoordinatesAndFacesMotion()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState state = ElementVisualTestData.State();
                state.PreviousPosition = new Vector2(10.0f, 20.0f);
                state.Position = new Vector2(30.0f, 40.0f);
                state.PreviousHeading = 0.0f;
                state.Heading = Mathf.PI * 0.5f;
                state.TerrainHeight = 7.0f;
                manager.UpdateElements(new[] { state }, 0.1f, 0.1f, 0.5f, 0);

                ElementVisualController view = manager.GetView(state.Id);
                ElementVisualTestData.AssertVector(new Vector3(20.0f, 7.0f, 30.0f), view.Root.transform.position);
                float heading = Mathf.PI * 0.25f;
                Vector3 expectedForward = new Vector3(Mathf.Sin(heading), 0.0f, Mathf.Cos(heading));
                Vector3 actualForward = view.Root.transform.rotation * EquipmentModelLibrary.Front;
                ElementVisualTestData.AssertVector(expectedForward, actualForward);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void GroundVehicle_UsesFourSlopeSamplesAndCachesSmallMovement()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                int samples = 0;
                manager.GroundHeight = delegate(Vector2 position)
                {
                    samples++;
                    return position.x * 0.5f + position.y * 0.25f;
                };
                ElementVisualState state = ElementVisualTestData.State();
                manager.UpdateElements(new[] { state }, 0.1f, 0.1f, 1.0f, 0);
                Assert.AreEqual(5, samples);

                float pitch = Mathf.Atan(0.25f);
                float roll = -Mathf.Atan(0.5f);
                Quaternion expected = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0.0f, roll * Mathf.Rad2Deg);
                Assert.Less(Quaternion.Angle(expected, manager.GetView(state.Id).Model.Tilt.localRotation), 0.01f);

                state.PreviousPosition = state.Position;
                state.Position += new Vector2(0.1f, 0.0f);
                manager.Sync(new[] { state });
                manager.UpdateVisuals(0.1f, 0.1f, 1.0f, 1);
                Assert.AreEqual(6, samples);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Turret_FollowsTargetAtGameRateAndRecoilsOnNewFireTick()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState shooter = ElementVisualTestData.State(1);
                shooter.LastFiredTick = 5;
                shooter.TargetId = 2;
                ElementVisualState target = ElementVisualTestData.State(2, side: "red");
                target.Position = new Vector2(10.0f, 0.0f);
                target.PreviousPosition = target.Position;
                var states = new List<ElementVisualState> { shooter, target };
                manager.UpdateElements(states, 0.1f, 0.0f, 1.0f, 5);
                ElementVisualController view = manager.GetView(1);
                Assert.AreEqual(0.0f, view.TurretYaw, 0.0001f);
                Assert.AreEqual(0.0f, view.Recoil, 0.0001f);

                manager.UpdateVisuals(0.5f, 0.5f, 1.0f, 5);
                Assert.AreEqual(0.55f, view.TurretYaw, 0.0001f);
                Vector3 pivot = view.Model.Turret.localPosition;

                shooter.LastFiredTick = 6;
                manager.Sync(states);
                manager.UpdateVisuals(0.0f, 0.0f, 1.0f, 6);
                Assert.AreEqual(1.0f, view.Recoil, 0.0001f);
                Assert.AreEqual(ElementVisualMath.RecoilMeters,
                    Vector3.Distance(pivot, view.Model.Turret.localPosition), 0.0001f);

                manager.UpdateVisuals(0.2f, 0.2f, 1.0f, 6);
                Assert.AreEqual(0.4f, view.Recoil, 0.0001f);
                Assert.AreEqual(0.77f, view.TurretYaw, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DestroyedTurret_UsesStableIdDisplacement()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState state = ElementVisualTestData.State(1);
                state.State = ElementVisualCondition.Destroyed;
                manager.Sync(new[] { state });
                ElementVisualController view = manager.GetView(1);
                Vector3 pivot = view.Model.Turret.localPosition;
                manager.UpdateVisuals(0.1f, 0.1f, 1.0f, 0);

                float factor = ElementVisualMath.DestroyedTurretFactor(state.Id);
                ElementVisualTestData.AssertVector(
                    pivot + new Vector3(0.0f, 0.35f + factor * 0.4f, 0.0f),
                    view.Model.Turret.localPosition);
                Quaternion firstRotation = view.Model.Turret.localRotation;
                manager.UpdateVisuals(1.0f, 1.0f, 1.0f, 10);
                Assert.Less(Quaternion.Angle(firstRotation, view.Model.Turret.localRotation), 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Helicopter_SmoothsAltitudeAddsSpeedPitchAndAnimatesRotorWhilePaused()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState state = ElementVisualTestData.State(1, "us_ah64e", "heli");
                state.Altitude = 90.0f;
                state.TerrainHeight = 5.0f;
                state.Velocity = new Vector2(70.0f, 0.0f);
                manager.UpdateElements(new[] { state }, 0.1f, 0.1f, 1.0f, 0);
                ElementVisualController view = manager.GetView(1);
                Assert.AreEqual(90.0f, view.SmoothedAltitude, 0.0001f);
                ElementVisualTestData.AssertVector(new Vector3(0.0f, 95.0f, 0.0f), view.Root.transform.position);
                Assert.AreEqual(2.8f, view.RotorAngle, 0.0001f);
                Quaternion speedPitch = Quaternion.Euler(-0.22f * Mathf.Rad2Deg, 0.0f, 0.0f);
                Assert.Less(Quaternion.Angle(speedPitch, view.Model.Tilt.localRotation), 0.001f);

                state.Altitude = 25.0f;
                manager.Sync(new[] { state });
                manager.UpdateVisuals(0.5f, 0.0f, 1.0f, 0);
                Assert.AreEqual(88.8f, view.SmoothedAltitude, 0.0001f);
                Assert.AreEqual(4.8f, view.RotorAngle, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void HelicopterWreck_DescendsStopsRotorAndUsesFixedTilt()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState state = ElementVisualTestData.State(1, "ru_ka52", "heli", "red");
                state.Altitude = 30.0f;
                state.TerrainHeight = 5.0f;
                manager.UpdateElements(new[] { state }, 0.1f, 0.1f, 1.0f, 0);
                ElementVisualController view = manager.GetView(1);
                float flyingRotorAngle = view.RotorAngle;
                Quaternion primaryRotor = Quaternion.AngleAxis(flyingRotorAngle * Mathf.Rad2Deg, Vector3.up);
                Quaternion secondaryRotor = Quaternion.AngleAxis(-flyingRotorAngle * Mathf.Rad2Deg, Vector3.up);
                Assert.Less(Quaternion.Angle(primaryRotor, view.Model.Rotor.localRotation), 0.001f);
                Assert.Less(Quaternion.Angle(secondaryRotor, view.Model.Rotor2.localRotation), 0.001f);

                state.State = ElementVisualCondition.Destroyed;
                manager.Sync(new[] { state });
                manager.UpdateVisuals(0.1f, 0.1f, 1.0f, 1);
                Assert.AreEqual(27.0f, view.SmoothedAltitude, 0.0001f);
                Assert.AreEqual(flyingRotorAngle, view.RotorAngle, 0.0001f);
                Quaternion wreckTilt = Quaternion.Euler(0.25f * Mathf.Rad2Deg, 0.0f, 0.45f * Mathf.Rad2Deg);
                Assert.Less(Quaternion.Angle(wreckTilt, view.Model.Tilt.localRotation), 0.001f);
                ElementVisualTestData.AssertVector(new Vector3(0.0f, 32.0f, 0.0f), view.Root.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DamageAndDeath_UpdateRoleMaterialsAndNormalRestoresSharedMaterials()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState state = ElementVisualTestData.State();
                manager.UpdateElements(new[] { state }, 0.0f, 0.0f, 1.0f, 0);
                ElementVisualController view = manager.GetView(1);
                MeshRenderer renderer = view.Model.TurretRenderer;
                Material normalCamo = renderer.sharedMaterials[0];
                Material normalMarker = renderer.sharedMaterials[2];

                state.State = ElementVisualCondition.Damaged;
                manager.Sync(new[] { state });
                manager.UpdateVisuals(0.0f, 0.0f, 1.0f, 0);
                Assert.AreNotSame(normalCamo, renderer.sharedMaterials[0]);
                Assert.AreSame(normalMarker, renderer.sharedMaterials[2]);
                Color damaged = Color.Lerp(new Color(0.33f, 0.30f, 0.15f), new Color(0.55f, 0.45f, 0.2f), 0.4f);
                damaged = new Color(damaged.r * 0.75f, damaged.g * 0.75f, damaged.b * 0.75f, damaged.a);
                ElementVisualTestData.AssertColor(damaged, renderer.sharedMaterials[0].GetColor("_BaseColor"));

                state.State = ElementVisualCondition.Destroyed;
                manager.Sync(new[] { state });
                manager.UpdateVisuals(0.0f, 0.0f, 1.0f, 0);
                ElementVisualTestData.AssertColor(new Color(0.10f, 0.09f, 0.08f),
                    renderer.sharedMaterials[0].GetColor("_BaseColor"));
                ElementVisualTestData.AssertColor(new Color(0.10f, 0.09f, 0.08f),
                    renderer.sharedMaterials[2].GetColor("_BaseColor"));

                state.State = ElementVisualCondition.Ok;
                manager.Sync(new[] { state });
                manager.UpdateVisuals(0.0f, 0.0f, 1.0f, 0);
                Assert.AreSame(normalCamo, renderer.sharedMaterials[0]);
                Assert.AreSame(normalMarker, renderer.sharedMaterials[2]);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
