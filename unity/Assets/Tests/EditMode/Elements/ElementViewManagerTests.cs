using System;
using System.Collections.Generic;
using Echelon.Presentation.Models;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements.Tests
{
    public sealed class ElementViewManagerTests
    {
        [Test]
        public void Pool_PrunesMissingIdsAndReusesResetEquipment()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState state = ElementVisualTestData.State(1);
                manager.UpdateElements(new[] { state }, 0.0f, 0.0f, 1.0f, 0);
                ElementVisualController original = manager.GetView(1);
                GameObject originalRoot = original.Root;
                Material normalCamo = original.Model.HullRenderer.sharedMaterials[0];
                Assert.AreEqual(1, manager.CreatedViewCount);

                state.State = ElementVisualCondition.Damaged;
                manager.Sync(new[] { state });
                manager.UpdateVisuals(0.0f, 0.0f, 1.0f, 0);
                Assert.AreNotSame(normalCamo, original.Model.HullRenderer.sharedMaterials[0]);
                manager.Sync(new ElementVisualState[0]);
                Assert.AreEqual(0, manager.ActiveCount);
                Assert.AreEqual(1, manager.PooledCount);
                Assert.IsFalse(originalRoot.activeSelf);

                ElementVisualState reusedState = ElementVisualTestData.State(2);
                manager.UpdateElements(new[] { reusedState }, 0.0f, 0.0f, 1.0f, 0);
                ElementVisualController reused = manager.GetView(2);
                Assert.AreSame(original, reused);
                Assert.AreSame(originalRoot, reused.Root);
                Assert.AreEqual(1, manager.CreatedViewCount);
                Assert.AreEqual(0, manager.PooledCount);
                Assert.AreSame(normalCamo, reused.Model.HullRenderer.sharedMaterials[0]);
                Assert.AreEqual(0.0f, reused.Recoil, 0.0001f);
                Assert.AreEqual(-1.0f, reused.SmoothedAltitude, 0.0001f);
                Assert.AreEqual("E2", reused.Root.name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Pool_DistinguishesEffectiveTuskLoadout()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState regular = ElementVisualTestData.State(1);
                manager.Sync(new[] { regular });
                Assert.AreEqual(EquipmentModelKind.Abrams, manager.GetView(1).Model.Kind);
                manager.Sync(new ElementVisualState[0]);

                ElementVisualState tusk = ElementVisualTestData.State(2);
                tusk.Loadout = "sepv3_tusk";
                manager.Sync(new[] { tusk });
                Assert.AreEqual(EquipmentModelKind.AbramsTusk, manager.GetView(2).Model.Kind);
                Assert.AreEqual(2, manager.CreatedViewCount);
                Assert.AreEqual(1, manager.PooledCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Sync_CopiesSnapshotsInsteadOfRetainingCallerObjects()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState state = ElementVisualTestData.State(1);
                manager.Sync(new[] { state });
                state.Position = new Vector2(100.0f, 100.0f);
                state.State = ElementVisualCondition.Destroyed;
                manager.UpdateVisuals(0.0f, 0.0f, 1.0f, 0);

                ElementVisualController view = manager.GetView(1);
                ElementVisualTestData.AssertVector(Vector3.zero, view.Root.transform.position);
                Assert.AreEqual(ElementVisualCondition.Ok, view.State.State);
                Assert.AreNotSame(state, view.State);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Manager_AppliesMountedOffMapFriendlyWreckAndFogVisibility()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState friendly = ElementVisualTestData.State(1);
                ElementVisualState seen = ElementVisualTestData.State(2, side: "red");
                seen.LastSeenTick = 150;
                ElementVisualState unseen = ElementVisualTestData.State(3, side: "red");
                unseen.LastSeenTick = 49;
                ElementVisualState wreck = ElementVisualTestData.State(4, side: "red");
                wreck.State = ElementVisualCondition.Destroyed;
                ElementVisualState mounted = ElementVisualTestData.State(5);
                mounted.IsMounted = true;
                ElementVisualState offMap = ElementVisualTestData.State(6);
                offMap.OnMap = false;
                var states = new List<ElementVisualState> { friendly, seen, unseen, wreck, mounted, offMap };
                manager.FogSide = "blue";
                manager.UpdateElements(states, 0.0f, 0.0f, 1.0f, 200);

                Assert.IsTrue(manager.GetView(1).Root.activeSelf);
                Assert.IsTrue(manager.GetView(2).Root.activeSelf);
                Assert.IsFalse(manager.GetView(3).Root.activeSelf);
                Assert.IsTrue(manager.GetView(4).Root.activeSelf);
                Assert.IsFalse(manager.GetView(5).Root.activeSelf);
                Assert.IsFalse(manager.GetView(6).Root.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Manager_CreatesAViewForEveryDataEquipmentId()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                List<ElementVisualState> states = ElementVisualTestData.AllEquipment();
                Assert.AreEqual(57, states.Count);
                manager.UpdateElements(states, 0.0f, 0.0f, 1.0f, 0);

                Assert.AreEqual(57, manager.ActiveCount);
                Assert.AreEqual(57, manager.CreatedViewCount);
                Assert.AreEqual(0, manager.PooledCount);
                for (int i = 0; i < states.Count; i++)
                {
                    ElementVisualController view = manager.GetView(states[i].Id);
                    Assert.IsTrue(view.Root.activeSelf, states[i].ElementId);
                    Assert.AreEqual(states[i].ElementId, view.Model.ElementId);
                    Assert.IsNotNull(view.Model.HullMesh, states[i].ElementId);
                    Assert.Greater(view.Model.HullMesh.vertexCount, 0, states[i].ElementId);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Sync_RejectsDuplicateIdsBeforeChangingViews()
        {
            GameObject owner;
            ElementViewManager manager = ElementVisualTestData.Manager(out owner);
            try
            {
                ElementVisualState state = ElementVisualTestData.State(1);
                manager.Sync(new[] { state });
                Assert.Throws<ArgumentException>(delegate
                {
                    manager.Sync(new[] { state, state.Snapshot() });
                });
                Assert.AreEqual(1, manager.ActiveCount);
                Assert.AreEqual(0, manager.PooledCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
