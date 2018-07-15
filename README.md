# CryptLink.HashedObjectStorage
A interface for implementing caching and storage of objects by their hash, a specialized key/value store for any object that implements `CryptLink.SigningFramework.IHashable`.

Nuget package: TODO

[![License: LGPL v3](https://img.shields.io/badge/License-LGPL%20v3-blue.svg)](https://www.gnu.org/licenses/lgpl-3.0)
[![Build status](https://ci.appveyor.com/api/projects/status/h8d84kts2phy4wvj?svg=true)](https://ci.appveyor.com/project/CryptLink/hashedobjectstorage)
[![NuGet](TODO:https://img.shields.io/nuget/v/CryptLink.HashedObjectStore.svg)](https://www.nuget.org/packages/CryptLink.HashedObjectStore/)

## Implementations
Currently the only official implementation is `MemoryStore` that stores items in a thread safe concurrent dictionary, it is very fast, but not durable. 

### Benchmark
MemoryStore performance benchmark test on a i7-7700HQ, 16GB laptop:

```
Message: Using single thread, Targeting 00:00:02.5000000 rounds
CryptLink.HashedObjectStore.MemoryStore for SHA256: preformed 695,186 operations in 00:00:02.5058967, (277,420 per sec)
CryptLink.HashedObjectStore.MemoryStore for SHA384: preformed 776,024 operations in 00:00:02.5000266, (310,406 per sec)
CryptLink.HashedObjectStore.MemoryStore for SHA512: preformed 807,220 operations in 00:00:02.5087127, (321,767 per sec)
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