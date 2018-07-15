using CryptLink.SigningFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptLink.HashedObjectStore {
    public class StorageItemMeta {
        public DateTime StoreTime { get; set; }
        public IHashable Item { get; set; }

        public Hash Hash => Item.ComputedHash;
    }
}
