using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CryptLink.SigningFramework;

namespace CryptLink.HashedObjectStore
{
    public class MemoryStore : IHashItemStore {

        private ConcurrentDictionary<Hash, StorageItemMeta> _data;
        private ConcurrentDictionary<DateTime, Hash> _dataDates;
        private long _dataSize;

        HashProvider _provider;
        TimeSpan _keepItemsFor;
        DateTime? _minDate;
        DateTime? _maxDate;
        long _maxTotalItems;
        long _maxItemSizeBytes;
        long _maxTotalSizeBytes;

        public long ItemCount => _data.Count;

        /// <summary>
        /// Permanently drops (deletes) all data, memory will be freed on dispose or by Garbage Collector
        /// </summary>
        public void DropData() {
            _data = null;
            _dataDates = null;
            _dataSize = 0;
            _minDate = null;
            _maxDate = null;
        }

        /// <summary>
        /// Creates a new memory store, items stored here are not written to disk
        /// </summary>
        /// <param name="Provider">The hash provider to use</param>
        /// <param name="KeepItemsFor">Length of time to keep any given item for</param>
        /// <param name="MaxTotalItems">The maximum length this store can be, if overrun oldest items will be removed by RunMaintence()</param>
        /// <param name="MaxItemSizeBytes">The maximum length (in bytes) any item can be as reported by ComputedHash.SourceByteLength</param>
        /// <param name="MaxTotalSizeBytes">The maximum size in bytes this entire collection can be, if overrun oldest items will be removed by RunMaintence()</param>
        public MemoryStore(HashProvider Provider, TimeSpan KeepItemsFor, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes) {
            _provider = Provider;
            _keepItemsFor = KeepItemsFor;
            _maxTotalItems = MaxTotalItems;
            _maxItemSizeBytes = MaxItemSizeBytes;

            _data = new ConcurrentDictionary<Hash, StorageItemMeta>();
            _dataDates = new ConcurrentDictionary<DateTime, Hash>();

        }

        /// <summary>
        /// Creates a new memory store, items stored here are not written to disk
        /// Intended for use with HashItemStoreFactory
        /// </summary>
        /// <param name="Provider">The hash provider to use</param>
        /// <param name="KeepItemsFor">Length of time to keep any given item for</param>
        /// <param name="MaxTotalItems">The maximum length this store can be, if overrun oldest items will be removed by RunMaintence()</param>
        /// <param name="MaxItemSizeBytes">The maximum length (in bytes) any item can be as reported by ComputedHash.SourceByteLength</param>
        /// <param name="MaxTotalSizeBytes">The maximum size in bytes this entire collection can be, if overrun oldest items will be removed by RunMaintence()</param>
        public static IHashItemStore CreateStore(HashProvider Provider, TimeSpan KeepItemsFor, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes) {
            return new MemoryStore(Provider, KeepItemsFor, MaxTotalItems, MaxTotalItems, MaxItemSizeBytes);
        }

        public T GetItem<T>(Hash ItemHash) where T : IHashable {
            if (_provider != ItemHash.Provider) {
                throw new NullReferenceException("The items hash provider does not match the storage");
            }

            if (_data.ContainsKey(ItemHash)) {
                return (T)_data[ItemHash].Item;
            } else {
                return default(T);
            }
        }

        /// <summary>
        /// Runs any maintenance needed for the store, including cleaning up old items
        /// </summary>
        public void RunMaintence() {

            //Remove items that are too old
            var maxAge = DateTime.Now.Add(-_keepItemsFor);

            if (_minDate < maxAge) {
                var expiredItems = (from e in _dataDates where e.Key < maxAge select e.Value);
                TryRemoveItems(expiredItems);
            }

            //Check if the store is too big
            if (_data.Count > _maxTotalItems) {
                if (_maxTotalItems > int.MaxValue) {
                    _maxTotalItems = int.MaxValue;
                }

                var oldestItems = (from e in _dataDates orderby e.Key select e.Value).Take((int)_maxTotalItems);
                TryRemoveItems(oldestItems);
            }

            //if the store is still too big
            while (_dataSize > _maxTotalSizeBytes) {
                TryRemoveItem(_dataDates.First().Value);
            }

        }

        /// <summary>
        /// Stores an item
        /// </summary>
        /// <param name="ItemHash">The item to store</param>
        /// <returns>True if the item is unique and stored, false if not</returns>
        public bool StoreItem<T>(T Item) where T : IHashable {

            if (Item.ComputedHash == null) {
                throw new ArgumentOutOfRangeException("The provided item's Hash has not been computed");
            }

            if (Item.ComputedHash.Provider != _provider) {
                throw new ArgumentOutOfRangeException("The provided item's Hash is the wrong provider type");
            }

            if (Item.ComputedHash.SourceByteLength > _maxItemSizeBytes) {
                throw new ArgumentOutOfRangeException("The item provided is larger than this store allows");
            }

            _dataDates.TryAdd(DateTime.Now, Item.ComputedHash);
            _dataSize -= Item.ComputedHash.SourceByteLength;
            
            var meta = new StorageItemMeta() {
                Item = Item,
                StoreTime = DateTime.Now
            };

            if (meta.StoreTime > _maxDate || _maxDate == null) {
                _maxDate = meta.StoreTime;
            }

            if (meta.StoreTime < _minDate || _minDate == null) {
                _minDate = meta.StoreTime;
            }

            return _data.TryAdd(Item.ComputedHash, meta);
        }

        /// <summary>
        /// Tries to remove a single item, returns true if successful 
        /// </summary>
        /// <param name="ItemHash">Hash of the item to remove</param>
        /// <returns>True if successful false if not</returns>
        public bool TryRemoveItem(Hash ItemHash) {
            Hash removedItemHash;
            StorageItemMeta removedMeta;

            var result = _data.TryRemove(ItemHash, out removedMeta);
            _dataDates.TryRemove(removedMeta.StoreTime, out removedItemHash);
            _dataSize -= ItemHash.SourceByteLength;

            if (removedMeta.StoreTime == _maxDate) {
                _maxDate = _dataDates.Keys.Max();
            }

            if (removedMeta.StoreTime == _minDate) {
                _maxDate = _dataDates.Keys.Min();
            }

            return result;
        }

        /// <summary>
        /// Tries to remove a list of items
        /// </summary>
        /// <param name="ItemHash">Hashes of items to remove</param>
        public void TryRemoveItems(IEnumerable<Hash> Items) {
            foreach (var item in Items) {
                TryRemoveItem(item);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    DropData();
                }

                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }
}
