﻿using CryptLink.HashedObjectStore;
using CryptLink.SigningFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CryptLink.HashedObjectStore
{
    public class Factory {

        /// <summary>
        /// Uses reflection to create a ItemStore of a specified type
        /// </summary>
        /// <param name="StoreType">The type of object you would like to create
        /// The type must implement IHashItemStore, and a constructor with the signature: 
        ///     HashProvider Provider, 
        ///     TimeSpan KeepItemsFor, 
        ///     long MaxTotalItems, 
        ///     long MaxItemSizeBytes, 
        ///     long MaxTotalSizeBytes
        /// </param>
        /// <param name="Provider">The hash provider you would like to use</param>
        /// <param name="KeepItemsFor">Time to keep items for</param>
        /// <param name="MaxTotalItems">Total number of items to keep</param>
        /// <param name="MaxItemSizeBytes">The largest object to store</param>
        /// <param name="MaxTotalSizeBytes">The max total number of bytes to keep (approximate)</param>
        /// <returns>A IHashItemStore of the type specified</returns>
        public static IHashItemStore Create(Type StoreType, HashProvider Provider, TimeSpan KeepItemsFor, 
            long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes) {

            var hisInterface = StoreType.GetInterface(typeof(IHashItemStore).ToString());

            if (hisInterface == null) {
                throw new ArgumentOutOfRangeException($"The type '{StoreType.Name}' does not implement 'IHashItemStore', can't create the store.");
            }

            Type[] constorTypeParams = new Type[5];
            constorTypeParams[0] = typeof(HashProvider);
            constorTypeParams[1] = typeof(TimeSpan);
            constorTypeParams[2] = typeof(long);
            constorTypeParams[3] = typeof(long);
            constorTypeParams[4] = typeof(long);

            var constructor = StoreType.GetConstructor(constorTypeParams);

            if (constructor == null) {
                throw new NotImplementedException($"The type '{StoreType.Name}' does not implement a constructor with the signature: 'HashProvider Provider, TimeSpan KeepItemsFor, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes', can't create the store");
            }

            object[] constorParams = new object[5];
            constorParams[0] = Provider;
            constorParams[1] = KeepItemsFor;
            constorParams[2] = MaxTotalItems;
            constorParams[3] = MaxItemSizeBytes;
            constorParams[4] = MaxTotalSizeBytes;

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
                    .Where(p => typeof(IHashItemStore).IsAssignableFrom(p) 
                           && (typeof(IHashItemStore) != p))
                    .ToArray();
            }

            return implementors;
        }

    }
}
