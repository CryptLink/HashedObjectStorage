using System;
using System.Collections.Generic;
using System.Linq;
using CryptLink.HashedObjectStore;
using CryptLink.SigningFramework;
using NUnit.Framework;

namespace CryptLink.HashedObjectStoreTests {
    [TestFixture]
    class StoreTests {

        [Test, Category("HashableStore")]
        public void AddRemove() {

            foreach (var hStoreType in Factory.GetImplementors()) {
                var c = Factory.Create(hStoreType, HashProvider.SHA384, TimeSpan.MaxValue, 1, long.MaxValue, long.MaxValue);

                var firstItem = new HashableString(Guid.NewGuid().ToString());
                firstItem.ComputeHash(HashProvider.SHA384, null);

                c.StoreItem(firstItem);
                Assert.True(c.GetItem<HashableString>(firstItem.ComputedHash) == firstItem);
                Assert.True(c.ItemCount == 1);

                c.TryRemoveItem(firstItem.ComputedHash);
                Assert.False(c.GetItem<HashableString>(firstItem.ComputedHash) == firstItem);
                Assert.True(c.ItemCount == 0);

                c.DropData();
                c.Dispose();
            }

        }

        [Test, Category("HashableStore")]
        public void RunMaintence() {
            int testSize = 20;

            foreach (var hStoreType in Factory.GetImplementors()) {
                var c = Factory.Create(hStoreType, HashProvider.SHA384, new TimeSpan(0,0,0,1,0), testSize, long.MaxValue, long.MaxValue);

                var firstItem = new HashableString(Guid.NewGuid().ToString());
                firstItem.ComputeHash(HashProvider.SHA384, null);
                c.StoreItem(firstItem);

                Assert.True(c.GetItem<HashableString>(firstItem.ComputedHash) == firstItem);

                for (int i = 0; i < testSize; i++) {
                    var item = new HashableString(Guid.NewGuid().ToString());
                    item.ComputeHash(HashProvider.SHA384, null);
                    c.StoreItem(item);
                    Assert.True(c.GetItem<HashableString>(item.ComputedHash) == item);
                    System.Threading.Thread.Sleep(100);
                }

                Assert.True(c.ItemCount == testSize + 1);

                c.RunMaintence();

                Assert.True(c.ItemCount > 0);
                Assert.True(c.ItemCount < testSize + 1);

                Assert.False(c.GetItem<HashableString>(firstItem.ComputedHash) == firstItem);

                c.DropData();
                c.Dispose();

                var exItem = new HashableString("Disposed storage object should throw an error if storage is attempted");
                exItem.ComputeHash(HashProvider.SHA384);

                Assert.Throws<NullReferenceException>(delegate {
                    c.StoreItem(exItem);
                });
            }

        }

    }
}
