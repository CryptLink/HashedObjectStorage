using CryptLink.HashedObjectStore;
using CryptLink.SigningFramework;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptLink.HashedObjectStoreTests {
    [TestFixture]
    class PerformanceTests
    {
        private TimeSpan TestLength = new TimeSpan(0, 0, 0, 2, 500);
        private Random random = new Random();

        [Test, Category("Performance")]
        public void AddGetPerformance() {
            var results = $"Using single thread, Targeting {TestLength} rounds\r\n";
            int operationCount = 2;

            foreach (var hStore in Factory.GetImplementors()) {

                if (hStore == typeof(NullStore)) {
                    continue;
                }

                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {
                    var c = StoreTests.GetTestStore(hStore, provider, true);

                    DateTime startTime = DateTime.Now;

                    while ((startTime + TestLength) > DateTime.Now) {
                        var item = GetNext();
                        item.ComputeHash(provider);
                        c.StoreItem(item);
                        c.GetItem<HashableString>(item.ComputedHash);
                    }

                    results += GetResultString(provider, c.ItemCount * operationCount, hStore, (DateTime.Now - startTime));
                }
            }

            Assert.Pass(results);
        }

        public HashableString GetNext() {
            return new HashableString(Guid.NewGuid().ToString());
        }

        [Test, Category("Performance")]
        public void AddGetMultithreadPerformance() {
            var threads = 8;
            var results = $"Using {threads} threads, Targeting {TestLength} rounds\r\n";

            foreach (var hStore in Factory.GetImplementors()) {

                if (hStore == typeof(NullStore)) {
                    continue;
                }

                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {

                    var c = StoreTests.GetTestStore(hStore, provider, true);
                    var tasks = new List<Task>();
                    DateTime testStart = DateTime.Now;

                    for (int i = 0; i < threads; i++) {

                        var t = new Task(() => {
                            DateTime taskStartTime = DateTime.Now;

                            while ((taskStartTime + TestLength) > DateTime.Now) {
                                var item = GetNext();
                                item.ComputeHash(provider);
                                c.StoreItem(item);
                                c.GetItem<HashableString>(item.ComputedHash);
                            }
                        });

                        tasks.Add(t);
                        t.Start();
                    }

                    Task.WaitAll(tasks.ToArray());

                    results += GetResultString(provider, c.ItemCount, hStore, (DateTime.Now - testStart));
                    c.Dispose();
                }
            }

            Assert.Pass(results);
        }

        private string GetResultString(HashProvider Provider, long ItemCount, Type StoreType, TimeSpan TestLengthActual) {
            var perSec = (ItemCount / TestLengthActual.TotalSeconds);
            return $"{StoreType} for {Provider}: preformed {ItemCount.ToString("n0")} operations in {TestLengthActual}, ({perSec.ToString("n0")} per sec)\r\n";
        }

    }
}
