using System;
using System.Collections.Generic;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Battle
{
    public readonly struct BattleNotification
    {
        public string Text { get; }
        public Color Color { get; }

        public BattleNotification(string text, Color color)
        {
            Text = text ?? string.Empty;
            Color = color;
        }
    }

    public sealed class NotificationEntry
    {
        public int Id { get; }
        public string Text { get; }
        public Color Color { get; }
        public float Age { get; internal set; }
        public float Alpha => Age <= NotificationFeedState.LifetimeSeconds
            ? 1f
            : Mathf.Clamp01(1f - (Age - NotificationFeedState.LifetimeSeconds) / NotificationFeedState.FadeSeconds);

        internal NotificationEntry(int id, string text, Color color)
        {
            Id = id;
            Text = text;
            Color = color;
        }
    }

    public sealed class NotificationFeedState
    {
        public const int MaximumItems = 8;
        public const float LifetimeSeconds = 10f;
        public const float FadeSeconds = 1.5f;

        private readonly List<NotificationEntry> _entries = new List<NotificationEntry>();
        private int _nextId = 1;

        public IReadOnlyList<NotificationEntry> Entries => _entries;

        public NotificationEntry Push(string text, Color color)
        {
            var entry = new NotificationEntry(_nextId++, text ?? string.Empty, color);
            _entries.Add(entry);
            while (_entries.Count > MaximumItems)
                _entries.RemoveAt(0);
            return entry;
        }

        public void Advance(float deltaSeconds)
        {
            float delta = Mathf.Max(0f, deltaSeconds);
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                _entries[i].Age += delta;
                if (_entries[i].Age > LifetimeSeconds + FadeSeconds)
                    _entries.RemoveAt(i);
            }
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }

    [DisallowMultipleComponent]
    public sealed class NotificationFeed : MonoBehaviour
    {
        private readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();

        public bool IsBuilt { get; private set; }
        public NotificationFeedState State { get; } = new NotificationFeedState();
        public IReadOnlyList<NotificationEntry> Entries => State.Entries;
        public int Count => State.Entries.Count;

        public static NotificationFeed Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("NotificationFeed", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var feed = root.AddComponent<NotificationFeed>();
            feed.Build();
            return feed;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -96f);
            rect.sizeDelta = new Vector2(372f, 400f);
            VerticalLayoutGroup layout = UiKit.AddVerticalLayout(rect, 4);
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        public NotificationEntry Push(string text, Color? color = null)
        {
            NotificationEntry entry = State.Push(text, color ?? new Color(0.95f, 0.95f, 0.95f));
            if (IsBuilt)
                Rebuild();
            return entry;
        }

        public void Push(BattleNotification notification)
        {
            Push(notification.Text, notification.Color);
        }

        public void Advance(float deltaSeconds)
        {
            State.Advance(deltaSeconds);
            if (IsBuilt)
                SyncLabels();
        }

        public void Clear()
        {
            State.Clear();
            if (IsBuilt)
                Rebuild();
        }

        public TextMeshProUGUI GetLabel(int index)
        {
            return index >= 0 && index < _labels.Count ? _labels[index] : null;
        }

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyChild(transform.GetChild(i).gameObject);
            _labels.Clear();
            foreach (NotificationEntry entry in State.Entries)
            {
                TextMeshProUGUI label = UiKit.CreateLabel(transform, entry.Text, 14, entry.Color);
                label.name = "Notification_" + entry.Id;
                label.alignment = TextAlignmentOptions.TopRight;
                label.outlineColor = new Color(0f, 0f, 0f, 0.9f);
                label.outlineWidth = 0.2f;
                LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
                layout.minWidth = 360f;
                layout.preferredWidth = 360f;
                layout.flexibleWidth = 1f;
                _labels.Add(label);
            }
            SyncLabels();
        }

        private void SyncLabels()
        {
            if (_labels.Count != State.Entries.Count)
            {
                Rebuild();
                return;
            }
            for (int i = 0; i < _labels.Count; i++)
            {
                NotificationEntry entry = State.Entries[i];
                _labels[i].color = new Color(entry.Color.r, entry.Color.g, entry.Color.b, entry.Color.a * entry.Alpha);
            }
        }

        private static void DestroyChild(GameObject child)
        {
            child.SetActive(false);
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
