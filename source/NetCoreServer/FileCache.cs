using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;

namespace NetCoreServer
{
    /// <summary>
    /// File cache is used to cache files in memory with optional timeouts.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    public class FileCache
    {
        public delegate bool InsertHandler(FileCache cache, string key, byte[] value, TimeSpan timeout);

        /// <summary>
        /// Is the file cache empty?
        /// </summary>
        public bool Empty { get { lock (_lock) return _entriesByKey.Count == 0; } }
        /// <summary>
        /// Get the file cache size
        /// </summary>
        public int Size { get { lock (_lock) return _entriesByKey.Count; } }

        /// <summary>
        /// Add a new cache value with the given timeout into the file cache
        /// </summary>
        /// <param name="key">Key to add</param>
        /// <param name="value">Value to add</param>
        /// <param name="timeout">Cache timeout (default is 0 - no timeout)</param>
        /// <returns>'true' if the cache value was added, 'false' if the given key was not added</returns>
        public bool Add(string key, byte[] value, TimeSpan timeout = new TimeSpan())
        {
            lock (_lock)
            {
                // Try to find and remove the previous key
                RemoveInternal(key);

                // Update the cache entry
                if (timeout.Ticks > 0)
                {
                    DateTime current = DateTime.UtcNow;
                    _timestamp = (current <= _timestamp) ? new DateTime(_timestamp.Ticks + 1) : current;
                    _entriesByKey.Add(key, new MemCacheEntry(value, _timestamp, timeout));
                    _entriesByTimestamp.Add(_timestamp, key);
                }
                else
                    _entriesByKey.Add(key, new MemCacheEntry(value));

                return true;
            }
        }

        /// <summary>
        /// Add a new cache value with the given timeout into the file cache
        /// </summary>
        /// <param name="key">Key to add</param>
        /// <param name="value">Value to add</param>
        /// <param name="timeout">Cache timeout (default is 0 - no timeout)</param>
        /// <returns>'true' if the cache value was added, 'false' if the given key was not added</returns>
        public bool Add(string key, string value, TimeSpan timeout = new TimeSpan())
        {
            lock (_lock)
            {
                // Try to find and remove the previous key
                RemoveInternal(key);

                // Update the cache entry
                if (timeout.Ticks > 0)
                {
                    DateTime current = DateTime.UtcNow;
                    _timestamp = (current <= _timestamp) ? new DateTime(_timestamp.Ticks + 1) : current;
                    _entriesByKey.Add(key, new MemCacheEntry(value, _timestamp, timeout));
                    _entriesByTimestamp.Add(_timestamp, key);
                }
                else
                    _entriesByKey.Add(key, new MemCacheEntry(value));

                return true;
            }
        }

        /// <summary>
        /// Try to find the cache value by the given key
        /// </summary>
        /// <param name="key">Key to find</param>
        /// <returns>'true' and cache value if the cache value was found, 'false' if the given key was not found</returns>
        public Tuple<bool, byte[]> Find(string key)
        {
            lock (_lock)
            {
                // Try to find the given key
                if (!_entriesByKey.TryGetValue(key, out var cacheValue))
                    return new Tuple<bool, byte[]>(false, new byte[0]);

                return new Tuple<bool, byte[]>(true, cacheValue.Value);
            }
        }

        /// <summary>
        /// Try to find the cache value with timeout by the given key
        /// </summary>
        /// <param name="key">Key to find</param>
        /// <param name="timeout">Cache timeout value</param>
        /// <returns>'true' and cache value if the cache value was found, 'false' if the given key was not found</returns>
        public Tuple<bool, byte[]> Find(string key, out DateTime timeout)
        {
            lock (_lock)
            {
                // Try to find the given key
                if (!_entriesByKey.TryGetValue(key, out var cacheValue))
                {
                    timeout = new DateTime(0);
                    return new Tuple<bool, byte[]>(false, new byte[0]);
                }

                timeout = cacheValue.Timestamp + cacheValue.Timespan;
                return new Tuple<bool, byte[]>(true, cacheValue.Value);
            }
        }

        /// <summary>
        /// Remove the cache value with the given key from the file cache
        /// </summary>
        /// <param name="key">Key to remove</param>
        /// <returns>'true' if the cache value was removed, 'false' if the given key was not found</returns>
        public bool Remove(string key)
        {
            lock (_lock)
            {
                return RemoveInternal(key);
            }
        }

        /// <summary>
        /// Insert a new cache path with the given timeout into the file cache
        /// </summary>
        /// <param name="path">Path to insert</param>
        /// <param name="prefix">Cache prefix (default is "/")</param>
        /// <param name="timeout">Cache timeout (default is 0 - no timeout)</param>
        /// <param name="handler">Cache insert handler (default is 'return cache.Add(key, value, timeout)')</param>
        /// <returns>'true' if the cache path was setup, 'false' if failed to setup the cache path</returns>
        public bool InsertPath(string path, string prefix = "/", TimeSpan timeout = new TimeSpan(), InsertHandler handler = null)
        {
            handler ??= (FileCache cache, string key, byte[] value, TimeSpan timespan) => cache.Add(key, value, timespan);

            // Try to find and remove the previous path
            RemovePathInternal(path);

            // Insert the cache path
            if (!InsertPathInternal(path, prefix, timeout, handler))
                return false;

            lock (_lock)
            {
                // Update the cache entry
                if (timeout.Ticks > 0)
                {
                    DateTime current = DateTime.UtcNow;
                    _timestamp = (current <= _timestamp) ? new DateTime(_timestamp.Ticks + 1) : current;
                    _pathsByKey.Add(path, new FileCacheEntry(prefix, handler, _timestamp, timeout));
                    _pathsByTimestamp.Add(_timestamp, path);
                }
                else
                    _pathsByKey.Add(path, new FileCacheEntry(prefix, handler));

                return true;
            }
        }

        /// <summary>
        /// Try to find the cache path
        /// </summary>
        /// <param name="path">Path to find</param>
        /// <returns>'true' if the cache path was found, 'false' if the given path was not found</returns>
        public bool FindPath(string path)
        {
            lock (_lock)
            {
                // Try to find the given key
                return _pathsByKey.ContainsKey(path);
            }
        }

        /// <summary>
        /// Try to find the cache path with timeout
        /// </summary>
        /// <param name="path">Path to find</param>
        /// <param name="timeout">Cache timeout value</param>
        /// <returns>'true' if the cache path was found, 'false' if the given path was not found</returns>
        public bool FindPath(string path, out DateTime timeout)
        {
            lock (_lock)
            {
                // Try to find the given key
                if (!_pathsByKey.TryGetValue(path, out var cacheValue))
                {
                    timeout = new DateTime(0);
                    return false;
                }

                timeout = cacheValue.Timestamp + cacheValue.Timespan;
                return true;
            }
        }

        /// <summary>
        /// Remove the cache path from the file cache
        /// </summary>
        /// <param name="path">Path to remove</param>
        /// <returns>'true' if the cache path was removed, 'false' if the given path was not found</returns>
        public bool RemovePath(string path)
        {
            return RemovePathInternal(path);
        }

        /// <summary>
        /// Clear the memory cache
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                // Clear all cache entries
                _entriesByKey.Clear();
                _entriesByTimestamp.Clear();
                _pathsByKey.Clear();
                _pathsByTimestamp.Clear();
            }
        }

        /// <summary>
        /// Watchdog the file cache
        /// </summary>
        public void Watchdog(DateTime utc)
        {
            Monitor.Enter(_lock);

            // Watchdog for cache entries
            while (_entriesByTimestamp.Count > 0)
            {
                var entry = _entriesByTimestamp.First();
                if (!_entriesByKey.TryGetValue(entry.Value, out var cachedValue))
                    break;

                if (cachedValue.Timestamp + cachedValue.Timespan <= utc)
                {
                    // Erase the cache entry with timeout
                    _entriesByKey.Remove(entry.Value);
                    _entriesByTimestamp.Remove(entry.Key);
                    continue;
                }
                else
                    break;
            }

            // Watchdog for cache paths
            while (_pathsByTimestamp.Count > 0)
            {
                var entry = _pathsByTimestamp.First();
                if (!_pathsByKey.TryGetValue(entry.Value, out var cachedValue))
                    break;

                if (cachedValue.Timestamp + cachedValue.Timespan <= utc)
                {
                    // Update the cache path with timeout
                    var path = entry.Value;
                    var prefix = cachedValue.Prefix;
                    var timespan = cachedValue.Timespan;
                    var handler = cachedValue.Handler;
                    _pathsByTimestamp.Remove(entry.Key);
                    Monitor.Exit(_lock);
                    InsertPath(path, prefix, timespan, handler);
                    Monitor.Enter(_lock);
                    continue;
                }
                else
                    break;
            }
        }

        private readonly object _lock = new object();
        private DateTime _timestamp = DateTime.UtcNow;

        private struct MemCacheEntry
        {
            public readonly byte[] Value;
            public readonly DateTime Timestamp;
            public readonly TimeSpan Timespan;

            public MemCacheEntry(byte[] v, DateTime ts = new DateTime(), TimeSpan tp = new TimeSpan())
            {
                Value = v;
                Timestamp = ts;
                Timespan = tp;
            }

            public MemCacheEntry(string v, DateTime ts = new DateTime(), TimeSpan tp = new TimeSpan())
            {
                Value = Encoding.UTF8.GetBytes(v);
                Timestamp = ts;
                Timespan = tp;
            }
        };

        private struct FileCacheEntry
        {
            public readonly string Prefix;
            public readonly InsertHandler Handler;
            public readonly DateTime Timestamp;
            public readonly TimeSpan Timespan;

            public FileCacheEntry(string pfx, InsertHandler h, DateTime ts = new DateTime(), TimeSpan tp = new TimeSpan())
            {
                Prefix = pfx;
                Handler = h;
                Timestamp = ts;
                Timespan = tp;
            }
        };

        private Dictionary<string, MemCacheEntry> _entriesByKey = new Dictionary<string, MemCacheEntry>();
        private SortedDictionary<DateTime, string> _entriesByTimestamp = new SortedDictionary<DateTime, string>();
        private SortedDictionary<string, FileCacheEntry> _pathsByKey = new SortedDictionary<string, FileCacheEntry>();
        private SortedDictionary<DateTime, string> _pathsByTimestamp = new SortedDictionary<DateTime, string>();

        private bool RemoveInternal(string key)
        {
            // Try to find the given key
            if(!_entriesByKey.TryGetValue(key, out var cacheValue))
                return false;

            // Try to erase cache entry by timestamp
            if (cacheValue.Timestamp.Ticks > 0)
                _entriesByTimestamp.Remove(cacheValue.Timestamp);

            // Erase cache entry
            _entriesByKey.Remove(key);

            return true;
        }

        private bool InsertPathInternal(string path, string prefix, TimeSpan timeout, InsertHandler handler)
        {
            try
            {
                string keyPrefix = (string.IsNullOrEmpty(prefix) || (prefix == "/")) ? "/" : (prefix + "/");

                // Iterate through all directory entries
                foreach (var item in Directory.GetDirectories(path))
                {
                    string key = keyPrefix + HttpUtility.UrlDecode(Path.GetFileName(item));

                    // Recursively insert sub-directory
                    if (!InsertPathInternal(item, key, timeout, handler))
                        return false;
                }

                foreach (var item in Directory.GetFiles(path))
                {
                    string key = keyPrefix + HttpUtility.UrlDecode(Path.GetFileName(item));

                    try
                    {
                        // Load the cache file content
                        var content = File.ReadAllBytes(item);
                        if (!handler(this, key, content, timeout))
                            return false;
                    }
                    catch (Exception) { return false; }
                }

                return true;
            }
            catch (Exception) { return false; }
        }

        private bool RemovePathInternal(string path)
        {
            lock (_lock)
            {
                // Try to find the given path
                if (!_pathsByKey.TryGetValue(path, out var cacheValue))
                    return false;

                // Try to erase cache path by timestamp
                if (cacheValue.Timestamp.Ticks > 0)
                    _pathsByTimestamp.Remove(cacheValue.Timestamp);

                // Erase cache path
                _pathsByKey.Remove(path);

                return true;
            }
        }
    }
}
