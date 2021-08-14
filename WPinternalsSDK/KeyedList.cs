using System;
using System.Collections.ObjectModel;

namespace WPinternalsSDK
{
    internal class KeyedList<TKey, TItem> : KeyedCollection<TKey, TItem>
    {
        public KeyedList(Func<TItem, TKey> getKeyFunc)
        {
            this._getKeyFunc = getKeyFunc;
        }

        protected override TKey GetKeyForItem(TItem item)
        {
            return this._getKeyFunc(item);
        }

        public bool TryGetValue(TKey key, out TItem item)
        {
            if (base.Dictionary == null)
            {
                item = default(TItem);
                return false;
            }
            return base.Dictionary.TryGetValue(key, out item);
        }

        public void AddOrUpdate(TItem item)
        {
            if (!this.Contains(item))
            {
                base.Add(item);
            }
        }

        public new bool Contains(TItem item)
        {
            return base.Contains(this._getKeyFunc(item));
        }

        public new bool Contains(TKey key)
        {
            return base.Contains(key);
        }

        public int IndexOf(TKey key)
        {
            return base.IndexOf(base[key]);
        }

        private readonly Func<TItem, TKey> _getKeyFunc;
    }
}
