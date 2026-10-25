using System;
using System.Collections;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class InsertionOrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _dictionary;
        private readonly List<TKey> _keys;
        private readonly CollectionView<TKey> _keyView;
        private readonly CollectionView<TValue> _valueView;

        public InsertionOrderedDictionary() : this(0, null)
        {
        }

        public InsertionOrderedDictionary(int capacity) : this(capacity, null)
        {
        }

        public InsertionOrderedDictionary(IEqualityComparer<TKey>? comparer) : this(0, comparer)
        {
        }

        public InsertionOrderedDictionary(int capacity, IEqualityComparer<TKey>? comparer)
        {
            _dictionary = new Dictionary<TKey, TValue>(capacity, comparer);
            _keys = new List<TKey>(capacity);
            _keyView = new CollectionView<TKey>(this, key => key, ContainsKey);
            _valueView = new CollectionView<TValue>(this, key => _dictionary[key], ContainsValue);
        }

        public InsertionOrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection) : this(collection, null)
        {
        }

        public InsertionOrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection,
            IEqualityComparer<TKey>? comparer) : this(comparer)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            foreach (KeyValuePair<TKey, TValue> pair in collection)
            {
                Add(pair.Key, pair.Value);
            }
        }

        public IEqualityComparer<TKey> Comparer => _dictionary.Comparer;
        public int Count => _dictionary.Count;
        public bool IsReadOnly => false;
        public ICollection<TKey> Keys => _keyView;
        public ICollection<TValue> Values => _valueView;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _keyView;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _valueView;

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set
            {
                if (_dictionary.ContainsKey(key))
                {
                    _dictionary[key] = value;
                    return;
                }

                _dictionary.Add(key, value);
                _keys.Add(key);
            }
        }

        public void Add(TKey key, TValue value)
        {
            _dictionary.Add(key, value);
            _keys.Add(key);
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public bool ContainsValue(TValue value)
        {
            return _dictionary.ContainsValue(value);
        }

        public bool Remove(TKey key)
        {
            if (!_dictionary.Remove(key))
            {
                return false;
            }

            for (int i = 0; i < _keys.Count; ++i)
            {
                if (_dictionary.Comparer.Equals(_keys[i], key))
                {
                    _keys.RemoveAt(i);
                    break;
                }
            }

            return true;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        public void Clear()
        {
            _dictionary.Clear();
            _keys.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ValidateCopyTo(array, arrayIndex, Count);
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                array[arrayIndex++] = pair;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (TKey key in _keys)
            {
                yield return new KeyValuePair<TKey, TValue>(key, _dictionary[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            return Contains(item) && Remove(item.Key);
        }

        private static void ValidateCopyTo(Array array, int arrayIndex, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            if (arrayIndex > array.Length || array.Length - arrayIndex < count)
            {
                throw new ArgumentException("The destination array is too small.", nameof(array));
            }
        }

        private sealed class CollectionView<TItem> : ICollection<TItem>, IReadOnlyCollection<TItem>
        {
            private readonly InsertionOrderedDictionary<TKey, TValue> _owner;
            private readonly Func<TKey, TItem> _selector;
            private readonly Func<TItem, bool> _contains;

            internal CollectionView(InsertionOrderedDictionary<TKey, TValue> owner, Func<TKey, TItem> selector,
                Func<TItem, bool> contains)
            {
                _owner = owner;
                _selector = selector;
                _contains = contains;
            }

            public int Count => _owner.Count;
            public bool IsReadOnly => true;

            public bool Contains(TItem item)
            {
                return _contains(item);
            }

            public void CopyTo(TItem[] array, int arrayIndex)
            {
                ValidateCopyTo(array, arrayIndex, Count);
                using IEnumerator<TItem> enumerator = GetEnumerator();
                while (enumerator.MoveNext())
                {
                    array[arrayIndex++] = enumerator.Current;
                }
            }

            public IEnumerator<TItem> GetEnumerator()
            {
                foreach (TKey key in _owner._keys)
                {
                    yield return _selector(key);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(TItem item)
            {
                throw new NotSupportedException("The collection is read-only.");
            }

            public void Clear()
            {
                throw new NotSupportedException("The collection is read-only.");
            }

            public bool Remove(TItem item)
            {
                throw new NotSupportedException("The collection is read-only.");
            }
        }
    }
}
