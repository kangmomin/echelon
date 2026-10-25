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

    /// <summary>오른쪽 알림 목록. 알림마다 왼쪽 색 띠가 있는 작은 카드이고, 10초 뒤 흐려지며 사라진다.</summary>
    [DisallowMultipleComponent]
    public sealed class NotificationFeed : MonoBehaviour
    {
        private readonly List<FeedItem> _items = new List<FeedItem>();

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
            BattleHudLayout.PlaceNotifications((RectTransform)transform);
            VerticalLayoutGroup layout = UiKit.AddVerticalLayout((RectTransform)transform, 4);
            layout.childAlignment = TextAnchor.UpperRight;
        }

        public NotificationEntry Push(string text, Color? color = null)
        {
            NotificationEntry entry = State.Push(text, color ?? UiTheme.Text);
            if (IsBuilt)
                Sync();
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
                Sync();
        }

        public void Clear()
        {
            State.Clear();
            if (IsBuilt)
                Sync();
        }

        public TextMeshProUGUI GetLabel(int index)
        {
            return index >= 0 && index < State.Entries.Count ? _items[index].Label : null;
        }

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void Sync()
        {
            IReadOnlyList<NotificationEntry> entries = State.Entries;
            while (_items.Count < entries.Count)
                _items.Add(CreateItem());
            for (int i = 0; i < _items.Count; i++)
            {
                FeedItem item = _items[i];
                bool shown = i < entries.Count;
                item.Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                NotificationEntry entry = entries[i];
                item.Label.text = entry.Text;
                float alpha = entry.Alpha;
                item.Label.color = UiTheme.WithAlpha(entry.Color, entry.Color.a * alpha);
                item.Stripe.color = UiTheme.WithAlpha(entry.Color, alpha);
                item.Background.color = UiTheme.WithAlpha(UiTheme.Panel, 0.86f * alpha);
            }
        }

        private FeedItem CreateItem()
        {
            var item = new FeedItem();
            item.Root = UiKit.CreatePanel(transform, "Notification", UiTheme.Panel);
            item.Background = item.Root.GetComponent<Image>();
            item.Background.raycastTarget = false;
            HorizontalLayoutGroup row = UiKit.AddHorizontalLayout(item.Root, 8);
            row.padding = new RectOffset(0, 10, 5, 5);
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            // 강제 확장 그룹은 유연 높이 1을 보고해, 그대로 두면 알림 열 높이만큼 카드가 늘어난다.
            UiKit.SetSize(item.Root).flexibleHeight = 0f;
            RectTransform stripe = UiKit.CreatePanel(item.Root, "Stripe", Color.white);
            item.Stripe = stripe.GetComponent<Image>();
            item.Stripe.raycastTarget = false;
            UiKit.SetSize(stripe, 3f);
            item.Label = UiKit.CreateLabel(item.Root, string.Empty, UiTheme.FontSmall, UiTheme.Text);
            item.Label.name = "Text";
            UiKit.SetSize(item.Label, 0f, -1f, 1f);
            return item;
        }

        private sealed class FeedItem
        {
            public RectTransform Root;
            public Image Background;
            public Image Stripe;
            public TextMeshProUGUI Label;
        }
    }
}
