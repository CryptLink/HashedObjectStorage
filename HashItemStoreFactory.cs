using CryptLink.HashedObjectStore;
using CryptLink.SigningFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CryptLink.HashedObjectStore
{
    public class Factory {

        /// <summary>
        /// Uses reflection to create a ItemStore of a specified type, uses defaults
        /// </summary>
        /// <param name="StoreType">The type of object you would like to create
        /// The type must implement IHashItemStore, and a constructor with the signature: 
        ///     HashProvider Provider 
        ///     TimeSpan KeepItemsFor (default: 1 yr)
        ///     TimeSpan OperationTimeout (default: 30 seconds)
        ///     long MaxTotalItems (default: int.MaxValue)
        ///     long MaxItemSizeBytes (default: int.MaxValue)
        ///     long MaxTotalSizeBytes (default: int.MaxValue)
        ///     string ConnectionString (default: null)
        /// </param>
        /// <param name="StoreType">Type of store to create</param>
        /// <param name="Provider">The hash provider you would like to use</param>
        /// <returns>A IHashItemStore of the type specified</returns>
        public static IHashItemStore Create(Type StoreType, HashProvider Provider) {
            return Create(StoreType, Provider, new TimeSpan(365, 0, 0, 0, 0), new TimeSpan(0, 0, 30), int.MaxValue, int.MaxValue, int.MaxValue, null);
        }

        /// <summary>
        /// Uses reflection to create a ItemStore of a specified type
        /// </summary>
        /// <param name="StoreType">The type of object you would like to create
        /// The type must implement IHashItemStore, and a constructor with the signature: 
        ///     HashProvider Provider, 
        ///     TimeSpan KeepItemsFor, 
        ///     TimeSpan OperationTimeout,
        ///     long MaxTotalItems, 
        ///     long MaxItemSizeBytes, 
        ///     long MaxTotalSizeBytes,
        ///     string ConnectionString
        /// </param>
        /// <param name="Provider">The hash provider you would like to use</param>
        /// <param name="KeepItemsFor">Time to keep items for</param>
        /// <param name="OperationTimeout">Amount of time to wait for read/write operations</param>
        /// <param name="MaxTotalItems">Total number of items to keep</param>
        /// <param name="MaxItemSizeBytes">The largest object to store</param>
        /// <param name="MaxTotalSizeBytes">The max total number of bytes to keep (approximate)</param>
        /// <param name="ConnectionString">Connection string for the store, if null default is used</param>
        /// <returns>A IHashItemStore of the type specified</returns>
        public static IHashItemStore Create(Type StoreType, HashProvider Provider, TimeSpan KeepItemsFor, TimeSpan OperationTimeout,
            long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes, string ConnectionString) {

            var hisInterface = StoreType.GetInterface(typeof(IHashItemStore).ToString());

            if (hisInterface == null) {
                throw new ArgumentOutOfRangeException($"The type '{StoreType.Name}' does not implement 'IHashItemStore', can't create the store.");
            }

            Type[] constorTypeParams = new Type[] { 
                typeof(HashProvider),
                typeof(TimeSpan),
                typeof(TimeSpan),
                typeof(long),
                typeof(long),
                typeof(long),
                typeof(string)
            };

        var constructor = StoreType.GetConstructor(constorTypeParams);

            if (constructor == null) {
                throw new NotImplementedException($"The type '{StoreType.Name}' does not implement a constructor with the correct signature: 'HashProvider Provider, TimeSpan KeepItemsFor, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes, string ConnectionString', can't create the store");
            }

            object[] constorParams = new object[] {
                Provider,
                KeepItemsFor,
                OperationTimeout,
                MaxTotalItems,
                MaxItemSizeBytes,
                MaxTotalSizeBytes,
                ConnectionString
            };

            return (IHashItemStore)constructor.Invoke(constorParams);

        }

        private static Type[] implementors = null;

        /// <summary>
        /// Scans all loaded assemblies and types for classes that implement IHashItemStore 
        /// </summary>
        /// <returns></returns>
        public static Type[] GetImplementors() {

            if (implementors == null) {
                implementors = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(p => 
                        typeof(IHashItemStore).IsAssignableFrom(p) && 
                        typeof(IHashItemStore) != p
                    ).ToArray();
            }

            return implementors;
        }

    }
}
