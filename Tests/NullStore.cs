using System;
using System.Collections.Generic;
using System.Text;
using CryptLink.SigningFramework;

namespace CryptLink.HashedObjectStore {

    /// <summary>
    /// A IHashItemSore implementation that never stores anything, only useful for throwing errors or mocking
    /// </summary>
    public class NullStore : IHashItemStore {

        public long ItemCount => 0;
        public bool IsPersistant => false;
        public long StoreSizeBytes => 0;
        public void Dispose() {}
        public void DropData() {}
        public void RunMaintenance() {}
        public void TryRemoveItems(IEnumerable<Hash> ItemHash) {}
        public T GetItem<T>(Hash ItemHash) where T : IHashable => default(T);
        public bool StoreItem<T>(T Item) where T : IHashable => false;
        public bool TryRemoveItem(Hash ItemHash) => false;

        public NullStore(HashProvider provider, TimeSpan KeepItemsFor, TimeSpan OperationTimeout, long MaxCount, long MaxItemSize, long MaxTotalSize, string ConnectionString) { }
    }
}
