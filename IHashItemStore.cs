﻿using System;
using System.Collections.Generic;
using System.IO;
using CryptLink.SigningFramework;

namespace CryptLink.HashedObjectStore {

    public interface IHashItemStore : IDisposable {

        /// <summary>
        /// Gets a typed item
        /// </summary>
        /// <param name="ItemHash">The hash of the item to get</param>
        /// <returns>The item if it exists, null if not</returns>
        T GetItem<T>(Hash ItemHash) where T : IHashable;

        /// <summary>
        /// Writes an item to a stream
        /// </summary>
        /// <param name="ItemHash">The hash of the item to get</param>
        /// <returns>True if the item exists</returns>
        Stream GetItemStream(Hash ItemHash);

        /// <summary>
        /// Tries to remove a single item, returns true if successful 
        /// </summary>
        /// <param name="ItemHash">Hash of the item to remove</param>
        /// <returns>True if successful false if not</returns>
        bool TryRemoveItem(Hash ItemHash);

        /// <summary>
        /// Tries to remove a list of items
        /// </summary>
        /// <param name="ItemHash">Hashes of items to remove</param>
        void TryRemoveItems(IEnumerable<Hash> ItemHash);

        /// <summary>
        /// Stores an item
        /// </summary>
        /// <param name="ItemHash">The item to store</param>
        /// <returns>True if the item is unique and stored, false if not</returns>
        bool StoreItem<T>(T Item) where T : IHashable;

        /// <summary>
        /// Gets the total count of items in the store
        /// </summary>
        long ItemCount { get; }

        /// <summary>
        /// Gets the total size in bytes, accuracy depends on the Hash.SourceByteLength
        /// </summary>
        long StoreSizeBytes { get; }

        /// <summary>
        /// Runs any maintenance needed for the store, including cleaning up old items
        /// </summary>
        void RunMaintenance();

        /// <summary>
        /// Permanently drops (deletes) all data immediately and cleans up any residual files or memory
        /// </summary>
        void DropData();

        /// <summary>
        /// Indicates if the implementor is durable/persistent 
        /// </summary>
        bool IsPersistant { get; }

    }
}
