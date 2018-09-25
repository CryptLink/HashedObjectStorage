using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CryptLink.HashedObjectStore;
using CryptLink.SigningFramework;
using NUnit.Framework;

namespace CryptLink.HashedObjectStoreTests {
    [TestFixture]
    public class StoreTests {

        Random R = new Random();
        Cert signingCert;

        [SetUp]
        public void Setup_LoadCert() {
            signingCert = Cert.LoadFromPfx("cert1.pfx_testonly_donotuse", "");
            Assert.True(signingCert.HasPrivateKey);
        }

        public static IHashItemStore GetTestStore(Type StoreType, HashProvider Provider, bool Recreate, int CopllectionItemSize) {
            var store = Factory.Create(StoreType, Provider, new TimeSpan(1, 0, 0, 0, 0), new TimeSpan(0, 0, 30), CopllectionItemSize, int.MaxValue, int.MaxValue, null);

            if (Recreate) {
                //Drop and recreate just to make sure the default instance is empty
                store.DropData();
                store = Factory.Create(StoreType, Provider, new TimeSpan(1, 0, 0, 0, 0), new TimeSpan(0, 0, 30), CopllectionItemSize, int.MaxValue, int.MaxValue, null);
            }

            return store;
        }

        [Test, Category("HashableStore")]
        public void SimpleFactoryExample() {
            var memStore = Factory.Create(typeof(MemoryStore), HashProvider.SHA256);
            var itemToStore = new HashableString("Test Value", HashProvider.SHA256);
            memStore.StoreItem(itemToStore);
            Assert.IsTrue(itemToStore.Verify());
            Assert.NotNull(memStore.GetItem<HashableString>(itemToStore.ComputedHash));
        }

        [Test, Category("HashableStore")]
        public void SimpleMemStoreExample() {
            var memStore = new MemoryStore(HashProvider.SHA256, new TimeSpan(1,0,0), new TimeSpan(0, 0, 30), int.MaxValue, int.MaxValue, int.MaxValue, null);
            var itemToStore = new HashableString("Test Value", HashProvider.SHA256);
            memStore.StoreItem(itemToStore);
            Assert.IsTrue(itemToStore.Verify());
            Assert.NotNull(memStore.GetItem<HashableString>(itemToStore.ComputedHash));
        }

        [Test, Category("HashableStore")]
        public void AddRemoveAccuracy() { 

            foreach (var hStoreType in Factory.GetImplementors()) {
                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {

                    var store = GetTestStore(hStoreType, provider, true, int.MaxValue);
                    var firstItem = new HashableString(Guid.NewGuid().ToString());
                    firstItem.ComputeHash(provider, signingCert);
                    store.StoreItem(firstItem);

                    if (hStoreType == typeof(NullStore)) {
                        //Skip nullstore
                        continue;
                    }

                    //Check that we can get our item and all the fields match exactly
                    var retrevedItem = store.GetItem<HashableString>(firstItem.ComputedHash);

                    Assert.True(retrevedItem.ComputedHash == firstItem.ComputedHash);
                    Assert.True(retrevedItem.ComputedHash.Provider == firstItem.ComputedHash.Provider);
                    Assert.True(retrevedItem.ComputedHash.SignatureBytes.ToComparable() == firstItem.ComputedHash.SignatureBytes.ToComparable());
                    Assert.True(retrevedItem.ComputedHash.SignatureCertHash.ToComparable() == firstItem.ComputedHash.SignatureCertHash.ToComparable());
                    Assert.True(retrevedItem.ComputedHash.SourceByteLength == firstItem.ComputedHash.SourceByteLength);

                    Assert.True(firstItem.Verify(signingCert));
                    Assert.True(retrevedItem.Verify(signingCert));

                    Assert.NotNull(retrevedItem.ComputedHash.ComputedDate);
                    Assert.True(retrevedItem.ComputedHash.ComputedDate.Value.Ticks == firstItem.ComputedHash.ComputedDate.Value.Ticks);
                    Assert.True(retrevedItem.Value == firstItem.Value);
                    Assert.True(store.ItemCount == 1);
                }
            }
        }

        [Test, Category("HashableStore")]
        public void AddRemoveExaustive() {

            foreach (var hStoreType in Factory.GetImplementors()) {
                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {

                    var store = GetTestStore(hStoreType, provider, true, int.MaxValue);

                    var firstItem = new HashableString(Guid.NewGuid().ToString());
                    firstItem.ComputeHash(provider, null);

                    store.StoreItem(firstItem);

                    if (hStoreType == typeof(NullStore)) {
                        //Skip nullstore
                        continue;
                    }

                    //If we are not storing to null, check that we can get our item
                    var retrevedItem = store.GetItem<HashableString>(firstItem.ComputedHash);
                    Assert.True(retrevedItem.ComputedHash == firstItem.ComputedHash);
                    Assert.True(retrevedItem.ComputedHash.ComputedDate == firstItem.ComputedHash.ComputedDate);
                    Assert.True(retrevedItem.Value == firstItem.Value);
                    
                    Assert.IsTrue(retrevedItem.Verify());
                    Assert.IsTrue(firstItem.Verify());

                    Assert.True(store.ItemCount == 1);

                    if (store.IsPersistant) {
                        //Dispose the current instance
                        store.Dispose();

                        //Check that dispose is implemented correctly and we can't get an item from a disposed store
                        Assert.IsNull(store.GetItem<HashableString>(firstItem.ComputedHash), "Disposed stores should not return a value");
                        Assert.False(store.StoreItem(firstItem), "Disposed stores should not store items");

                        //Recreate the store
                        store = GetTestStore(hStoreType, provider, false, int.MaxValue);

                        //Check that we can still get the persisted item
                        var retrevedItem2 = store.GetItem<HashableString>(firstItem.ComputedHash);
                        Assert.True(retrevedItem.ComputedHash == firstItem.ComputedHash);
                        Assert.True(retrevedItem.ComputedHash.ComputedDate == firstItem.ComputedHash.ComputedDate);
                        Assert.True(retrevedItem.Value == firstItem.Value);

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
        /// Check that the store can run maintenance correctly
        /// </summary>
        [Test, Category("HashableStore")]
        public void RunMaintenanceExaustive() {
            int testSize = R.Next(10, 50);

            foreach (var hStoreType in (Factory.GetImplementors())) {
                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {
                                        
                    if (hStoreType == typeof(NullStore)) {
                        continue;
                    }

                    var store = GetTestStore(hStoreType, provider, true, testSize);
                    store.RunMaintenance(); //some stores may not have an item count until maintenance is run
                    Assert.True(store.ItemCount == 0); //new store should be empty

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
                        Assert.IsTrue(item.Verify());
                        Assert.True(store.GetItem<HashableString>(item.ComputedHash).ComputedHash == item.ComputedHash);
                        System.Threading.Thread.Sleep(100);
                    }

                    //check the size, should have all
                    Assert.True(store.ItemCount == testSize + 1);

                    if (store.IsPersistant) {
                        //Dispose the current instance
                        store.Dispose();

                        //Recreate the store
                        store = GetTestStore(hStoreType, provider, false, testSize);

                        //some stores may not have an item count until maintenance is run
                        store.RunMaintenance();
                    } else {
                        //run maintenance and remove the oldest item
                        store.RunMaintenance();
                    }

                    //store should be exactly the test size after maintance
                    Assert.True(store.ItemCount == testSize);

                    //first item should be gone since it is the oldest
                    var firstItemGet = store.GetItem<HashableString>(firstItem.ComputedHash);
                    Assert.IsNull(firstItemGet);

                    store.DropData();
                    store.Dispose();
                }
            }

        }

        /// <summary>
        /// Check that the store can run maintenance correctly
        /// </summary>
        [Test, Category("HashableStore")]
        public void StreamTest() {

            foreach (var hStoreType in (Factory.GetImplementors())) {
                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {

                    var store = GetTestStore(hStoreType, provider, true, int.MaxValue);

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

                    //Test streaming
                    var ms = store.GetItemStream(firstItem.ComputedHash);

                    //read the stream
                    ms.Position = 0;
                    string readString;
                    using (var sr = new StreamReader(ms)) {
                        readString = sr.ReadToEnd();
                    }

                    var deseralized = Newtonsoft.Json.JsonConvert.DeserializeObject<HashableString>(readString);

                    Assert.IsTrue(deseralized.Verify());
                    deseralized.ComputeHash(provider);
                    Assert.IsTrue(deseralized.Verify());
                    Assert.AreEqual(deseralized.ComputedHash.Bytes, firstItem.ComputedHash.Bytes);
                    
                    store.DropData();
                    store.Dispose();
                }
            }

        }

    }
}
