using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements.Tests
{
    public sealed class ElementVisualMathTests
    {
        [Test]
        public void Interpolation_UsesTickAlphaAndShortestHeadingArc()
        {
            Vector2 position = ElementVisualMath.InterpolatePosition(
                new Vector2(10.0f, 20.0f), new Vector2(30.0f, 40.0f), 0.25f);
            Assert.AreEqual(15.0f, position.x, 0.0001f);
            Assert.AreEqual(25.0f, position.y, 0.0001f);

            float heading = ElementVisualMath.InterpolateHeading(
                170.0f * Mathf.Deg2Rad, -170.0f * Mathf.Deg2Rad, 0.5f);
            Assert.AreEqual(Mathf.PI, Mathf.Abs(heading), 0.0001f);
            Assert.AreEqual(0.2f, ElementVisualMath.InterpolateHeading(0.2f, 1.0f, -1.0f), 0.0001f);
        }

        [Test]
        public void Visibility_PreservesFriendlyWreckAndDetectionRules()
        {
            ElementVisualState enemy = ElementVisualTestData.State(2, side: "red");
            enemy.LastSeenTick = 90;
            Assert.IsTrue(ElementVisualMath.IsVisible(enemy, "blue", 190));
            Assert.IsFalse(ElementVisualMath.IsVisible(enemy, "blue", 191));
            Assert.IsTrue(ElementVisualMath.IsVisible(enemy, string.Empty, 1000));

            enemy.Side = "blue";
            Assert.IsTrue(ElementVisualMath.IsVisible(enemy, "blue", 1000));
            enemy.Side = "red";
            enemy.State = ElementVisualCondition.Destroyed;
            Assert.IsTrue(ElementVisualMath.IsVisible(enemy, "blue", 1000));
            enemy.IsMounted = true;
            Assert.IsFalse(ElementVisualMath.IsVisible(enemy, "blue", 1000));
            enemy.IsMounted = false;
            enemy.OnMap = false;
            Assert.IsFalse(ElementVisualMath.IsVisible(enemy, "blue", 1000));
        }

        [Test]
        public void TurretMath_PointsAtTargetAndTurnsAtBoundedRate()
        {
            float wanted = ElementVisualMath.TargetTurretYaw(
                Vector2.zero, 0.0f, new Vector2(10.0f, 0.0f));
            Assert.AreEqual(Mathf.PI * 0.5f, wanted, 0.0001f);
            Assert.AreEqual(0.0f, ElementVisualMath.TargetTurretYaw(
                Vector2.zero, 0.0f, new Vector2(0.5f, 0.5f)), 0.0001f);

            float yaw = ElementVisualMath.MoveTowardsAngle(0.0f, wanted, 0.11f);
            Assert.AreEqual(0.11f, yaw, 0.0001f);
            yaw = ElementVisualMath.MoveTowardsAngle(yaw, wanted, 10.0f);
            Assert.AreEqual(wanted, yaw, 0.0001f);
        }

        [Test]
        public void TerrainNormal_ProducesGodotPitchAndUnityRoll()
        {
            Vector2 northSlope = ElementVisualMath.TerrainAnglesFromNormal(
                new Vector3(0.0f, 1.0f, -1.0f), 0.0f);
            Assert.AreEqual(Mathf.PI * 0.25f, northSlope.x, 0.0001f);
            Assert.AreEqual(0.0f, northSlope.y, 0.0001f);

            Vector2 eastSlope = ElementVisualMath.TerrainAnglesFromNormal(
                new Vector3(-1.0f, 1.0f, 0.0f), 0.0f);
            Assert.AreEqual(0.0f, eastSlope.x, 0.0001f);
            Assert.AreEqual(-Mathf.PI * 0.25f, eastSlope.y, 0.0001f);
        }

        [Test]
        public void DestroyedTurretFactor_UsesElementIdWithoutRandomness()
        {
            Assert.AreEqual(0.19f, ElementVisualMath.DestroyedTurretFactor(1), 0.0001f);
            Assert.AreEqual(ElementVisualMath.DestroyedTurretFactor(17),
                ElementVisualMath.DestroyedTurretFactor(17));
        }
    }
}
