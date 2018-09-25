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
        //private Random random = new Random();
        //private byte[] psudoRandomData = new byte[1048576]; //1mb

        //[SetUp]
        //public void SetupFunction() {
        //    var r = new Random();
        //    r.NextBytes(psudoRandomData);
        //}

        [Test, Category("Performance")]
        public void AddGetPerformance() {
            var results = $"Using single thread, Targeting {TestLength} rounds\r\n";
            int operationCount = 2;

            foreach (var hStore in Factory.GetImplementors()) {

                if (hStore == typeof(NullStore)) {
                    continue;
                }

                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {
                    var c = StoreTests.GetTestStore(hStore, provider, true, int.MaxValue);

                    DateTime startTime = DateTime.Now;
                    int count = 0;

                    while ((startTime + TestLength) > DateTime.Now) {
                        var item = GetNextGuid();
                        item.ComputeHash(provider, null);
                        c.StoreItem(item);
                        c.GetItem<IHashable>(item.ComputedHash);
                        count++;
                    }

                    results += GetResultString(provider, count * operationCount, hStore, (DateTime.Now - startTime));
                }
            }

            Assert.Pass(results);
        }

        public IHashable GetNextGuid() {
            return new HashableString(Guid.NewGuid().ToString());
        }

        //public IHashable GetNextLarge() {
        //    Array.Copy(psudoRandomData, 1, psudoRandomData, 0, psudoRandomData.Length - 1);
        //    return new HashableBytes(psudoRandomData, HashProvider.SHA256);
        //}

        [Test, Category("Performance")]
        public void AddGetMultithreadPerformance() {
            var threads = 8;
            var results = $"Using {threads} threads, Targeting {TestLength} rounds\r\n";

            foreach (var hStore in Factory.GetImplementors()) {

                if (hStore == typeof(NullStore)) {
                    continue;
                }

                foreach (HashProvider provider in Enum.GetValues(typeof(HashProvider))) {

                    var c = StoreTests.GetTestStore(hStore, provider, true, int.MaxValue);
                    var tasks = new List<Task>();
                    DateTime testStart = DateTime.Now;

                    for (int i = 0; i < threads; i++) {

                        var t = new Task(() => {
                            DateTime taskStartTime = DateTime.Now;

                            while ((taskStartTime + TestLength) > DateTime.Now) {
                                var item = GetNextGuid();
                                item.ComputeHash(provider, null);
                                c.StoreItem(item);
                                c.GetItem<IHashable>(item.ComputedHash);
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
