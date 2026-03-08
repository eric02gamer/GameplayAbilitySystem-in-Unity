using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public class ChangeTimeSortedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, TValue> _dictionary;
        private readonly List<TKey> _keys;

        public ChangeTimeSortedDictionary()
        {
            _dictionary = new Dictionary<TKey, TValue>();
            _keys = new List<TKey>();
        }

        public void AddOrUpdate(TKey key, TValue value)
        {
            if (!_dictionary.ContainsKey(key))
            {
                _dictionary.Add(key, value);
                _keys.Add(key);
            }
            else
            {
                _dictionary[key] = value;
                _keys.Remove(key);
                _keys.Add(key);
            }
        }

        public void Remove(TKey key)
        {
            if(!_dictionary.ContainsKey(key)) return;

            _dictionary.Remove(key);
            _keys.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (!_dictionary.ContainsKey(key))
            {
                value = default;
                return false;
            }

            value = _dictionary[key];
            return true;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var key in _keys)
            {
                yield return new KeyValuePair<TKey, TValue>(key, _dictionary[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
