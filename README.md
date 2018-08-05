# CryptLink.HashedObjectStorage
A interface for implementing caching and storage of objects by their hash, a specialized key/value store for any object that implements `CryptLink.SigningFramework.IHashable`. Useful for quick and simple storage of hashed content.

Nuget package: https://www.nuget.org/packages/CryptLink.HashedObjectStore/

[![License: LGPL v3](https://img.shields.io/badge/License-LGPL%20v3-blue.svg)](https://www.gnu.org/licenses/lgpl-3.0)
[![Build status](https://ci.appveyor.com/api/projects/status/h8d84kts2phy4wvj?svg=true)](https://ci.appveyor.com/project/CryptLink/hashedobjectstorage)
[![NuGet](https://img.shields.io/nuget/v/CryptLink.HashedObjectStore.svg)](https://www.nuget.org/packages/CryptLink.HashedObjectStore/)

## Default Implementations

### `MemoryStore`
A thread safe memory only concurrent dictionary, it is very fast, but not durable. 

### `FileStore`
Low memory usage file based storage, each item is a file in a hashed based folder, slow, but 

### Creating your own implementation
The default implementations are functional and basic with no dependencies, but there are many databases and storage options that you may want to implement, and this library intends to make that possible.

Implement: `IHashItemStore`
Have a creation factory function with the signature: `Create(HashProvider Provider, TimeSpan KeepItemsFor, long MaxTotalItems, long MaxItemSizeBytes, long MaxTotalSizeBytes, string ConnectionString)`

When using custom/3rd party/specialized implementations, you can easily discover them with `CryptLink.HashedObjectStore.Factory.GetImplementors()`

### Generic Creation
In order to load and use implementations not in this library, `CryptLink.HashedObjectStore.Factory.Create(...)` can discover and create instances of any implementor of `IHashItemStore` using reflection.

### Benchmark
MemoryStore performance benchmark test on a i7-7700HQ, 16GB laptop:

```
Message: Using single thread, Targeting 00:00:02.5000000 rounds
MemoryStore for SHA256: preformed 613,350 operations in 00:00:02.5000249, (245,338 per sec)
MemoryStore for SHA384: preformed 546,316 operations in 00:00:02.5000239, (218,524 per sec)
MemoryStore for SHA512: preformed 567,864 operations in 00:00:02.5000293, (227,143 per sec)
FileStore for SHA256: preformed 512 operations in 00:00:02.5007423, (205 per sec)
FileStore for SHA384: preformed 548 operations in 00:00:02.5040153, (219 per sec)
FileStore for SHA512: preformed 498 operations in 00:00:02.5006257, (199 per sec)
```

This test sudo-randomly creates GUIDs, converts them to HashableStrings and stores them (about 72 bytes of text).

Despite being larger in size, the SHA3 family hashes more quickly on my machine and stores about 10% more in this scenario.

### Example
``` C#
var c = Factory.Create(MemoryStore, HashProvider.SHA384, TimeSpan.MaxValue, int.MaxValue, long.MaxValue, long.MaxValue);

var firstItem = new HashableString(Guid.NewGuid().ToString());
firstItem.ComputeHash(HashProvider.SHA384, null);

c.StoreItem(firstItem);
c.TryRemoveItem(firstItem.ComputedHash);

c.DropData();
c.Dispose();

```