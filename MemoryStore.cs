﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptLink.SigningFramework;
using Newtonsoft.Json;

namespace CryptLink.HashedObjectStore
{
    public class MemoryStore : IHashItemStore {

        private ConcurrentDictionary<Hash, StorageItemMeta> _data;
        private ConcurrentDictionary<DateTime, Hash> _dataDates;
        private long _dataSize;

        HashProvider _provider;
        TimeSpan _keepItemsFor;
        TimeSpan _timeout;
        DateTime? _minDate;
        DateTime? _maxDate;
        long _maxTotalItems;
        long _maxItemSizeBytes;
        long _maxTotalSizeBytes;

        public long ItemCount => _data.Count;
        public long StoreSizeBytes => _dataSize;
        public bool IsPersistant => false;

        /// <summary>
        /// Permanently drops (deletes) all data, memory will be freed on dispose or by Garbage Collector
        /// </summary>
        public void DropData() {
            _data = new ConcurrentDictionary<Hash, StorageItemMeta>();
            _dataDates = new ConcurrentDictionary<DateTime, Hash>();
            _dataSize = 0;
            _minDate = null;
            _maxDate = null;
        }

        /// <summary>
        /// Creates a new memory store, items stored here are not written to disk
        /// </summary>
        /// <param name="Provider">The hash provider to use</param>
        /// <param name="KeepItemsFor">Length of time to keep any given item for</param>
        /// <param name="OperationTimeout">Amount of time to wait for read/write operations</param>
        /// <param name="MaxTotalItems">The maximum length this store can be, if overrun oldest items will be removed by RunMaintenance()</param>
        /// <param name="MaxItemSizeBytes">The maximum length (in bytes) any item can be as reported by ComputedHash.SourceByteLength</param>
        /// <param name="MaxTotalSizeBytes">The maximum size in bytes this entire collection can be, if overrun oldest items will be removed by RunMaintenance()</param>
        public MemoryStore(HashProvider Provider, TimeSpan KeepItemsFor, TimeSpan OperationTimeout, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes, string ConnectionString) {
            _provider = Provider;
            _keepItemsFor = KeepItemsFor;
            _timeout = OperationTimeout;
            _maxTotalItems = MaxTotalItems;
            _maxItemSizeBytes = MaxItemSizeBytes;
            _maxTotalSizeBytes = MaxTotalSizeBytes;

            _data = new ConcurrentDictionary<Hash, StorageItemMeta>();
            _dataDates = new ConcurrentDictionary<DateTime, Hash>();

        }

        /// <summary>
        /// Creates a new memory store, items stored here are not written to disk
        /// Intended for use with HashItemStoreFactory
        /// </summary>
        /// <param name="Provider">The hash provider to use</param>
        /// <param name="KeepItemsFor">Length of time to keep any given item for</param>
        /// <param name="OperationTimeout">Amount of time to wait for read/write operations</param>
        /// <param name="MaxTotalItems">The maximum length this store can be, if overrun oldest items will be removed by RunMaintenance()</param>
        /// <param name="MaxItemSizeBytes">The maximum length (in bytes) any item can be as reported by ComputedHash.SourceByteLength</param>
        /// <param name="MaxTotalSizeBytes">The maximum size in bytes this entire collection can be, if overrun oldest items will be removed by RunMaintenance()</param>
        /// <param name="ConnectionString">Connection string, not used in this implementation</param>
        public static IHashItemStore CreateStore(HashProvider Provider, TimeSpan KeepItemsFor, TimeSpan OperationTimeout, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes, string ConnectionString) {
            return new MemoryStore(Provider, KeepItemsFor, OperationTimeout, MaxTotalItems, MaxTotalItems, MaxItemSizeBytes, ConnectionString);
        }

        public bool WriteItem(Hash ItemHash, StreamWriter ToStream) {

            var i = GetItem(ItemHash);
            var s = new JsonSerializer();

            using (var jtw = new JsonTextWriter(ToStream) {
                Formatting = Formatting.None
            }) {
                s.Serialize(jtw, i);
                //jtw.Flush();
                //ToStream.Flush();
            }

            return (i != null);
        }

        public T GetItem<T>(Hash ItemHash) where T : IHashable {
            if (_provider != ItemHash.Provider) {
                throw new NullReferenceException("The items hash provider does not match the storage");
            }

            if (disposedValue) {
                return default(T);
            }

            if (_data.ContainsKey(ItemHash)) {
                return (T)_data[ItemHash].Item;
            } else {
                return default(T);
            }
        }

        public Stream GetItemStream(Hash ItemHash) {
            if (_provider != ItemHash.Provider) {
                throw new NullReferenceException("The items hash provider does not match the storage");
            }

            if (disposedValue) {
                return default(Stream);
            }

            if (_data.ContainsKey(ItemHash)) {
                return new MemoryStream(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(_data[ItemHash].Item)));
            } else {
                return default(Stream);
            }
        }

        public object GetItem(Hash ItemHash) {
            if (_provider != ItemHash.Provider) {
                throw new NullReferenceException("The items hash provider does not match the storage");
            }

            if (disposedValue) {
                return null;
            }

            if (_data.ContainsKey(ItemHash)) {
                return _data[ItemHash].Item;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Runs any maintenance needed for the store, including cleaning up old items
        /// </summary>
        public void RunMaintenance() {

            //Remove items that are too old
            var maxAge = DateTime.Now.Add(-_keepItemsFor);

            if (_minDate < maxAge) {
                var expiredItems = (from e in _dataDates where e.Key < maxAge select e.Value);
                TryRemoveItems(expiredItems);
            }

            //Check if the store is too big
            if (_data.Count > _maxTotalItems) {
                long removeItems = (_data.Count - _maxTotalItems);

                if (removeItems > int.MaxValue) {
                    removeItems = int.MaxValue;
                }

                var oldestItems = (from e in _dataDates orderby e.Key select e.Value).Take((int)removeItems);
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

            if (Item.ComputedHash.SourceByteLength.HasValue) {
                _dataSize -= Item.ComputedHash.SourceByteLength.Value;
            }
            
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

            if (ItemHash.SourceByteLength.HasValue) {
                _dataSize -= ItemHash.SourceByteLength.Value;
            }

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
                    _data = null;
                    _dataDates = null;
                    _dataSize = 0;
                    _minDate = null;
                    _maxDate = null;
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
