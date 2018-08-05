using System;
using System.Collections.Generic;
using System.Linq;
using CryptLink.HashedObjectStore;
using CryptLink.SigningFramework;
using NUnit.Framework;

namespace CryptLink.HashedObjectStoreTests {
    [TestFixture]
    public class StoreTests {

        public static IHashItemStore GetTestStore(Type StoreType, HashProvider Provider, bool Recreate) {
            var store = Factory.Create(StoreType, Provider, new TimeSpan(1, 0, 0, 0, 0), new TimeSpan(0, 0, 30), 1, long.MaxValue, long.MaxValue, null);

            if (Recreate) {
                //Drop and recreate just to make sure the default instance is empty
                store.DropData();
                store = Factory.Create(StoreType, Provider, new TimeSpan(1, 0, 0, 0, 0), new TimeSpan(0, 0, 30), 1, long.MaxValue, long.MaxValue, null);
            }

            return store;
        }

        [Test, Category("HashableStore")]
        public void SimpleFactoryExample() {
            var memStore = Factory.Create(typeof(MemoryStore), HashProvider.SHA256);
            var itemToStore = new HashableString("Test Value", HashProvider.SHA256);
            memStore.StoreItem(itemToStore);
            Assert.NotNull(memStore.GetItem<HashableString>(itemToStore.ComputedHash));
        }

        [Test, Category("HashableStore")]
        public void SimpleMemStoreExample() {
            var memStore = new MemoryStore(HashProvider.SHA256, new TimeSpan(1,0,0), new TimeSpan(0, 0, 30), int.MaxValue, int.MaxValue, int.MaxValue, null);
            var itemToStore = new HashableString("Test Value", HashProvider.SHA256);
            memStore.StoreItem(itemToStore);
            Assert.NotNull(memStore.GetItem<HashableString>(itemToStore.ComputedHash));
        }

        [Test, Category("HashableStore")]
        public void AddRemoveExaustive() {

            foreach (var hStoreType in Factory.GetImplementors()) {
                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {

                    var store = GetTestStore(hStoreType, provider, true);

                    var firstItem = new HashableString(Guid.NewGuid().ToString());
                    firstItem.ComputeHash(provider, null);

                    store.StoreItem(firstItem);

                    if (hStoreType == typeof(NullStore)) {
                        //Skip nullstore
                        continue;
                    }

                    //If we are not storing to null, check that we can get our item
                    var retrevedItem = store.GetItem<HashableString>(firstItem.ComputedHash);
                    Assert.True(retrevedItem?.ComputedHash == firstItem.ComputedHash);
                    Assert.True(store.ItemCount == 1);

                    if (store.IsPersistant) {
                        //Dispose the current instance
                        store.Dispose();

                        //Check that dispose is implemented correctly and we can't get an item from a disposed store
                        Assert.IsNull(store.GetItem<HashableString>(firstItem.ComputedHash), "Disposed stores should not return a value");
                        Assert.False(store.StoreItem<HashableString>(firstItem), "Disposed stores should not store items");

                        //Recreate the store
                        store = GetTestStore(hStoreType, provider, false);

                        //Check that we can still get the persisted item
                        var retrevedItem2 = store.GetItem<HashableString>(firstItem.ComputedHash);
                        Assert.True(retrevedItem?.ComputedHash == firstItem.ComputedHash);

                        //some stores may not have an item count until maintenance is run (FileStore)
                        store.RunMaintenance();
                        Assert.True(store.ItemCount == 1);
                    }

                    store.TryRemoveItem(firstItem.ComputedHash);

                    if (hStoreType != typeof(NullStore)) {
                        //Check that an item after removal can't be retrieved
                        Assert.IsNull(store.GetItem<HashableString>(firstItem.ComputedHash));
                        Assert.True(store.ItemCount == 0);
                    }

                    //Drop all data
                    store.DropData();
                    Assert.IsNull(store.GetItem<HashableString>(firstItem.ComputedHash), "Dropped stores should not return a value");

                    //Finally dispose
                    store.Dispose();
                }
            }
        }
        /// <summary>
        /// Check that the store can 
        /// </summary>
        [Test, Category("HashableStore")]
        public void RunMaintenanceExaustive() {
            int testSize = 20;

            foreach (var hStoreType in (Factory.GetImplementors())) {
                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {

                    var store = GetTestStore(hStoreType, provider, true);
                    
                    if (hStoreType == typeof(NullStore)) {
                        continue;
                    }

                    //add a single item
                    var firstItem = new HashableString(Guid.NewGuid().ToString());
                    firstItem.ComputeHash(provider, null);
                    store.StoreItem(firstItem);

                    //check that we can get it
                    if (hStoreType != typeof(NullStore)) {
                        Assert.True(store.GetItem<HashableString>(firstItem.ComputedHash).ComputedHash == firstItem.ComputedHash);
                    }

                    //add a bunch more
                    for (int i = 0; i < testSize; i++) {
                        var item = new HashableString(Guid.NewGuid().ToString());
                        item.ComputeHash(provider, null);
                        store.StoreItem(item);
                        Assert.True(store.GetItem<HashableString>(item.ComputedHash).ComputedHash == item.ComputedHash);
                        System.Threading.Thread.Sleep(100);
                    }

                    //check the size, should have all
                    Assert.True(store.ItemCount == testSize + 1);

                    //run maintenance and remove the oldest item
                    store.RunMaintenance();

                    Assert.True(store.ItemCount > 0);
                    Assert.True(store.ItemCount < testSize + 1);

                    //first item should be gone
                    Assert.IsNull(store.GetItem<HashableString>(firstItem.ComputedHash));

                    store.DropData();
                    store.Dispose();
                }
            }

        }

    }
}
