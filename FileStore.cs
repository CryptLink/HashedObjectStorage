using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CryptLink.SigningFramework;
using Newtonsoft.Json;

namespace CryptLink.HashedObjectStore
{
    public class FileStore : IHashItemStore {

        public long itemCount = 0;
        public long totalSize = 0;

        public int retryMills = 250;

        public long ItemCount => itemCount;
        public long StoreSizeBytes => totalSize;
        public bool IsPersistant => true;

        private long _dataSize;
        HashProvider _provider;
        TimeSpan _keepItemsFor;
        TimeSpan _timeout;
        long _maxTotalItems;
        long _maxItemSizeBytes;
        long _maxTotalSizeBytes;
        long _maxKnownFileCacheLength = 100000;
        string _rootFolder;

        ConcurrentDictionary<Hash, DateTime> fileLocks = new ConcurrentDictionary<Hash, DateTime>();

        /// <summary>
        /// Creates a new memory store, items stored here are not written to disk
        /// </summary>
        /// <param name="Provider">The hash provider to use</param>
        /// <param name="KeepItemsFor">Length of time to keep any given item for</param>
        /// <param name="OperationTimeout">Amount of time to wait for read/write operations</param>
        /// <param name="MaxTotalItems">The maximum length this store can be, if overrun oldest items will be removed by RunMaintenance()</param>
        /// <param name="MaxItemSizeBytes">The maximum length (in bytes) any item can be as reported by ComputedHash.SourceByteLength</param>
        /// <param name="MaxTotalSizeBytes">The maximum size in bytes this entire collection can be, if overrun oldest items will be removed by RunMaintenance()</param>
        /// <param name="ConnectionString">Path to the folder where files are stored</param>
        public FileStore(HashProvider Provider, TimeSpan KeepItemsFor, TimeSpan OperationTimeout, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes, string ConnectionString) {
            _provider = Provider;
            _keepItemsFor = KeepItemsFor;
            _maxTotalItems = MaxTotalItems;
            _maxItemSizeBytes = MaxItemSizeBytes;
            _maxTotalSizeBytes = MaxTotalSizeBytes;
            _timeout = OperationTimeout;

            if (ConnectionString == null) {
                _rootFolder = Path.Combine(Environment.CurrentDirectory, "Default");
            } else {
                _rootFolder = ConnectionString;
            }

            if (!Directory.Exists(_rootFolder)) {
                //attempt to create the folder, will throw if access is denied
                Directory.CreateDirectory(_rootFolder);
            }

            //check that we have write/delete access, also could throw an error
            var tempFile = Path.Combine(_rootFolder, Guid.NewGuid().ToString() + ".test");
            File.WriteAllText(tempFile, tempFile);
            File.Delete(tempFile);
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
        /// <param name="ConnectionString">Path to the folder where files are stored</param>
        public static IHashItemStore CreateStore(HashProvider Provider, TimeSpan KeepItemsFor, TimeSpan OperationTimeout, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes, string ConnectionString) {
            return new FileStore(Provider, KeepItemsFor, OperationTimeout, MaxTotalItems, MaxTotalItems, MaxItemSizeBytes, ConnectionString);
        }

        public void DropData() {
            Directory.Delete(_rootFolder, true);
            Dispose();
        }

        public T GetItem<T>(Hash ItemHash) where T : IHashable {

            if (disposedValue == true) {
                return default(T);
            }

            var path = GetHashPath(ItemHash);
            var startTime = DateTime.Now;

            while (File.Exists(path)) {
                try {
                    using (StreamReader file = File.OpenText(path)) {
                        JsonSerializer serializer = new JsonSerializer();
                        T v = (T)serializer.Deserialize(file, typeof(T));
                        return v;
                    }
                } catch (Exception) {
                    if (TimedOut(startTime)) {
                        break;
                    } else {
                        System.Threading.Thread.Sleep(retryMills);
                    }
                }
            }

            return default(T);
        }

        public void RunMaintenance() {
            var allFiles = GetFiles(_rootFolder).OrderBy(a => a.ComputedDate).ToList();
            itemCount = allFiles.Count();
            totalSize = (from a in allFiles where a.SourceByteLength.HasValue select a.SourceByteLength.Value).Sum();

            var maxAge = DateTime.Now.Add(-_keepItemsFor);
            
            //remove in order of age
            foreach(var file in allFiles) {
                if (file.ComputedDate < maxAge || 
                    itemCount > _maxTotalItems ||
                    totalSize > _maxTotalSizeBytes) {

                    TryRemoveItem(file);
                } else {
                    //no more items to remove
                    break;
                }
            }

        }


        private List<Hash> GetFiles(string Folder) {
            var hashList = new List<Hash>();

            foreach (var file in Directory.GetFiles(Folder)) {
                var fi = new FileInfo(file);
                var hashBytes = Utility.DecodeBytes(Path.GetFileNameWithoutExtension(file), false);

                if (hashBytes == null || hashBytes.Length != _provider.GetProviderByteLength()) {
                    continue;
                }

                var h = Hash.FromComputedBytes(hashBytes, _provider, fi.Length, fi.LastWriteTime);
                hashList.Add(h);
            }

            foreach (var folder in Directory.GetDirectories(Folder)) {
                hashList.AddRange(GetFiles(folder));
            }

            return hashList;
        }

        private string GetHashPath(Hash ForItem) {
            string itemHash = Utility.EncodeBytes(ForItem.Bytes, true, false);
            return Path.Combine(_rootFolder, itemHash.Substring(0, 2).ToUpper(), itemHash.Substring(2, 2).ToUpper(), itemHash);
        }

        public bool StoreItem<T>(T Item) where T : IHashable {

            if (disposedValue == true) {
                return false;
            }

            var path = GetHashPath(Item.ComputedHash);
            var folder = Path.GetDirectoryName(path);

            if (!Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            } 

            if (Item.ComputedHash == null) {
                throw new ArgumentOutOfRangeException("The provided item's Hash has not been computed");
            }

            if (Item.ComputedHash.Provider != _provider) {
                throw new ArgumentOutOfRangeException("The provided item's Hash is the wrong provider type");
            }

            if (Item.ComputedHash.SourceByteLength > _maxItemSizeBytes) {
                throw new ArgumentOutOfRangeException("The item provided is larger than this store allows");
            }

            if (Item.ComputedHash.SourceByteLength.HasValue) {
                totalSize -= Item.ComputedHash.SourceByteLength.Value;
            }

            itemCount++;
            var startTime = DateTime.Now;
            var completed = false;

            while (!File.Exists(path)) {
                try {
                    var serializer = JsonSerializer.Create();

                    using (var sw = new StreamWriter(path)) {
                        using (var jsonTextWriter = new JsonTextWriter(sw)) {
                            serializer.Serialize(jsonTextWriter, Item);
                            completed = true;
                        }
                    }
                } catch (Exception) {
                    if (TimedOut(startTime)) {
                        break;
                    } else { 
                        System.Threading.Thread.Sleep(retryMills);
                    }
                }
            }

            return completed;
        }

        public bool TryRemoveItem(Hash ItemHash) {
            var path = GetHashPath(ItemHash);
            var startTime = DateTime.Now;

            while (File.Exists(path)) {
                try {
                    File.Delete(path);
                } catch (Exception) {
                    if (TimedOut(startTime)) {
                        break;
                    } else {
                        System.Threading.Thread.Sleep(retryMills);
                    }
                }
            }

            if (File.Exists(path)) {
                return false;
            } else {
                itemCount--;

                if (ItemHash.SourceByteLength.HasValue) {
                    totalSize -= ItemHash.SourceByteLength.Value;
                }

                return true;
            }
        }

        public bool TimedOut(DateTime StartTime) {
            return (StartTime + _timeout > DateTime.Now);
        }

        public void TryRemoveItems(IEnumerable<Hash> ItemHash) {
            foreach (var item in ItemHash) {
                TryRemoveItem(item);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
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
