using System;
using System.Collections.Generic;
using Echelon.Presentation.Models;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements
{
    public sealed class ElementViewManager : MonoBehaviour
    {
        private readonly Dictionary<int, ElementVisualController> views = new Dictionary<int, ElementVisualController>();
        private readonly Dictionary<ModelKey, Stack<ElementVisualController>> pool =
            new Dictionary<ModelKey, Stack<ElementVisualController>>();
        private readonly HashSet<int> seenIds = new HashSet<int>();
        private readonly List<int> pruneIds = new List<int>();
        private string fogSide = string.Empty;

        public string FogSide
        {
            get { return fogSide; }
            set { fogSide = value ?? string.Empty; }
        }

        public Func<Vector2, float> GroundHeight { get; set; }
        public int ActiveCount { get { return views.Count; } }
        public int CreatedViewCount { get; private set; }
        public int CurrentTick { get; private set; }

        public int PooledCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<ModelKey, Stack<ElementVisualController>> pair in pool)
                    count += pair.Value.Count;
                return count;
            }
        }

        public void Sync(IReadOnlyList<ElementVisualState> states)
        {
            if (states == null)
                throw new ArgumentNullException("states");

            seenIds.Clear();
            for (int i = 0; i < states.Count; i++)
            {
                ElementVisualState state = states[i];
                Validate(state);
                if (!seenIds.Add(state.Id))
                    throw new ArgumentException("Duplicate element visual id: " + state.Id, "states");
            }

            for (int i = 0; i < states.Count; i++)
            {
                ElementVisualState state = states[i];
                ElementVisualController view;
                if (views.TryGetValue(state.Id, out view) && !MatchesDefinition(view.State, state))
                {
                    Release(state.Id, view);
                    view = null;
                }
                if (view == null)
                {
                    view = Acquire(state);
                    views.Add(state.Id, view);
                }
                view.Synchronize(state);
            }

            pruneIds.Clear();
            foreach (KeyValuePair<int, ElementVisualController> pair in views)
            {
                if (!seenIds.Contains(pair.Key))
                    pruneIds.Add(pair.Key);
            }
            for (int i = 0; i < pruneIds.Count; i++)
                Release(pruneIds[i], views[pruneIds[i]]);
        }

        public void UpdateVisuals(
            float realDeltaSeconds,
            float gameDeltaSeconds,
            float interpolationAlpha,
            int currentTick)
        {
            CurrentTick = currentTick;
            foreach (KeyValuePair<int, ElementVisualController> pair in views)
            {
                ElementVisualController view = pair.Value;
                ElementVisualState target = null;
                ElementVisualController targetView;
                if (view.State.TargetId >= 0 && views.TryGetValue(view.State.TargetId, out targetView))
                    target = targetView.State;
                view.UpdateVisuals(
                    realDeltaSeconds,
                    gameDeltaSeconds,
                    interpolationAlpha,
                    currentTick,
                    fogSide,
                    target,
                    GroundHeight);
            }
        }

        public void UpdateElements(
            IReadOnlyList<ElementVisualState> states,
            float realDeltaSeconds,
            float gameDeltaSeconds,
            float interpolationAlpha,
            int currentTick)
        {
            Sync(states);
            UpdateVisuals(realDeltaSeconds, gameDeltaSeconds, interpolationAlpha, currentTick);
        }

        public bool TryGetView(int id, out ElementVisualController view)
        {
            return views.TryGetValue(id, out view);
        }

        public ElementVisualController GetView(int id)
        {
            return views[id];
        }

        public void Clear()
        {
            pruneIds.Clear();
            foreach (KeyValuePair<int, ElementVisualController> pair in views)
                pruneIds.Add(pair.Key);
            for (int i = 0; i < pruneIds.Count; i++)
                Release(pruneIds[i], views[pruneIds[i]]);
            seenIds.Clear();
        }

        private ElementVisualController Acquire(ElementVisualState state)
        {
            ModelKey key = new ModelKey(state);
            Stack<ElementVisualController> available;
            if (pool.TryGetValue(key, out available) && available.Count > 0)
            {
                ElementVisualController reused = available.Pop();
                reused.Synchronize(state);
                return reused;
            }

            EquipmentModelHandle model = EquipmentModelLibrary.Build(
                state.ElementId, state.ElementClass, state.Side, state.Loadout);
            var created = new ElementVisualController(model, state, transform);
            CreatedViewCount++;
            return created;
        }

        private void Release(int id, ElementVisualController view)
        {
            views.Remove(id);
            view.ResetForPool();
            ModelKey key = new ModelKey(view.State);
            Stack<ElementVisualController> available;
            if (!pool.TryGetValue(key, out available))
            {
                available = new Stack<ElementVisualController>();
                pool.Add(key, available);
            }
            available.Push(view);
        }

        private static bool MatchesDefinition(ElementVisualState current, ElementVisualState next)
        {
            return NormalizedEquals(current.ElementId, next.ElementId)
                && NormalizedEquals(current.ElementClass, next.ElementClass)
                && NormalizedEquals(current.Side, next.Side)
                && string.Equals(EffectiveLoadout(current), EffectiveLoadout(next), StringComparison.OrdinalIgnoreCase);
        }

        private static string EffectiveLoadout(ElementVisualState state)
        {
            string id = Normalize(state.ElementId);
            string loadout = Normalize(state.Loadout);
            return string.Equals(id, "us_m1a2_sepv3", StringComparison.OrdinalIgnoreCase)
                && string.Equals(loadout, "sepv3_tusk", StringComparison.OrdinalIgnoreCase)
                    ? "sepv3_tusk"
                    : string.Empty;
        }

        private static bool NormalizedEquals(string a, string b)
        {
            return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        private static void Validate(ElementVisualState state)
        {
            if (state == null)
                throw new ArgumentException("Element visual snapshots cannot be null.", "states");
            if (string.IsNullOrWhiteSpace(state.ElementId))
                throw new ArgumentException("An equipment element id is required.", "states");
            if (string.IsNullOrWhiteSpace(state.ElementClass))
                throw new ArgumentException("An equipment element class is required.", "states");
            if (state.State < ElementVisualCondition.Ok || state.State > ElementVisualCondition.Abandoned)
                throw new ArgumentException("Unknown element visual condition.", "states");
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<int, ElementVisualController> pair in views)
            {
                if (pair.Value.Root != null)
                    UnityEngine.Object.Destroy(pair.Value.Root);
            }
            foreach (KeyValuePair<ModelKey, Stack<ElementVisualController>> pair in pool)
            {
                foreach (ElementVisualController view in pair.Value)
                {
                    if (view.Root != null)
                        UnityEngine.Object.Destroy(view.Root);
                }
            }
            views.Clear();
            pool.Clear();
        }

        private struct ModelKey : IEquatable<ModelKey>
        {
            private readonly string elementId;
            private readonly string elementClass;
            private readonly string side;
            private readonly string loadout;

            internal ModelKey(ElementVisualState state)
            {
                elementId = Normalize(state.ElementId).ToLowerInvariant();
                elementClass = Normalize(state.ElementClass).ToLowerInvariant();
                side = Normalize(state.Side).ToLowerInvariant();
                loadout = EffectiveLoadout(state).ToLowerInvariant();
            }

            public bool Equals(ModelKey other)
            {
                return elementId == other.elementId && elementClass == other.elementClass
                    && side == other.side && loadout == other.loadout;
            }

            public override bool Equals(object obj)
            {
                return obj is ModelKey && Equals((ModelKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = elementId.GetHashCode();
                    hash = hash * 31 + elementClass.GetHashCode();
                    hash = hash * 31 + side.GetHashCode();
                    hash = hash * 31 + loadout.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
