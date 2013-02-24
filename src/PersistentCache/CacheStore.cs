﻿using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using PersistentCache.DiskCache;
using ServiceStack.Text;

namespace PersistentCache
{
    /// <summary>
    /// AUTHOR
    /// ------
    /// Andy Long | @theouteredge | andy@theouteredge.co.uk
    /// 
    /// ABOUT
    /// -----
    /// A Persistent Cache. Once a key/value is stored in the cache its stored for the lifetime of the cache.
    /// This is designed to be used in a very specific use case where you would want to large cache persistant cache which would be larger than the available
    /// system memory and you want the cache to store all the values while performing a single long running process. Once the process has completed the
    /// cache should be disposed of.
    /// 
    /// Once the available memory starts to run out and items are evicted or expired from the in memory cache they will be persisted to the disk.
    /// 
    /// DEPENDANCIES
    /// ------------
    /// ServiceStack.Text
    /// </summary>
    public class CacheStore : IDisposable
    {
        private readonly MemoryCache _cache;
        private readonly ICacheToDisk _diskCache;
        private bool _itemsCachedToDisk = false;


        public string PollingInterval { get; private set; }
        public string PhysicalMemoryLimitPercentage { get; private set; }
        public string CacheMemoryLimitMegabytes { get; private set; }

        public string BaseDirectory { get; set; }



        public CacheStore(string baseDirectory, string cacheMemoryLimitMegabytes = null, string physicalMemoryLimitPercentage = null, string pollingInterval = null)
        {
            BaseDirectory = baseDirectory;

            var config = new NameValueCollection
                {
                    {"pollingInterval", pollingInterval ?? "00:01:00" },
                    {"physicalMemoryLimitPercentage", physicalMemoryLimitPercentage ?? "0" },
                    {"cacheMemoryLimitMegabytes", cacheMemoryLimitMegabytes ?? "100" }
                };

            _cache = new MemoryCache("MainCache", config);
            
            //_diskCache = new FileCache(BaseDirectory);
            _diskCache = new DirectoryCache(BaseDirectory);

            //var keyLength = Hash(Guid.NewGuid().ToString()).ToUtf8Bytes().Length;
            //_diskCache = new DiskCache(BaseDirectory, keyLength);
        }


        public void Put(string key, object value, int itemExpiration = 10)
        {
            _cache.Set(Hash(key), value, new CacheItemPolicy() { RemovedCallback = RemovedCallback });
        }


        public bool TryGet<T>(string key, out T value)
        {
            key = Hash(key);

            var valueTmp = _cache.Get(key);
            if (valueTmp != null)
            {
                value = (T)valueTmp;
                return true;
            }

            if (_itemsCachedToDisk)
            {
                // it wasn't in the memory cache, so look for it within the ICacheToDisk
                if (_diskCache.TryGet<T>(key, out value))
                    return true;
            }

            value = default(T);
            return false;
        }


        private void RemovedCallback(CacheEntryRemovedArguments arguments)
        {
            // if we are not removing the item cos it was removed by the caller then save it to disk
            if (arguments.RemovedReason == CacheEntryRemovedReason.Evicted || arguments.RemovedReason == CacheEntryRemovedReason.Expired)
            {
                _itemsCachedToDisk = true;
                _diskCache.Put(arguments.CacheItem.Key, arguments.CacheItem.Value);
            }
        }

        
        private static string Hash(string s)
        {
            var hash = new SHA1Managed();
            var result = "";

            var buffer = Encoding.UTF8.GetBytes(s);
            var hashedBuffer = hash.ComputeHash(buffer);

            result = Convert.ToBase64String(hashedBuffer);

            return result;
        }

        public void Dispose()
        {
            if (_diskCache != null)
                _diskCache.Dispose();

            if (_cache != null)
                _cache.Dispose();
        }
    }
}