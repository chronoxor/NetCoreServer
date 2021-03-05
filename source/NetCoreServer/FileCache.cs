using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web;

namespace NetCoreServer
{
    /// <summary>
    /// File cache is used to cache files in memory with optional timeouts.
    /// FileSystemWatcher is used to monitor file system changes in cached
    /// directories.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    public class FileCache : IDisposable
    {
        public delegate bool InsertHandler(FileCache cache, string key, byte[] value, TimeSpan timeout);

        #region Cache items access

        /// <summary>
        /// Is the file cache empty?
        /// </summary>
        public bool Empty => _entriesByKey.Count == 0;

        /// <summary>
        /// Get the file cache size
        /// </summary>
        public int Size => _entriesByKey.Count;

        /// <summary>
        /// Add a new cache value with the given timeout into the file cache
        /// </summary>
        /// <param name="key">Key to add</param>
        /// <param name="value">Value to add</param>
        /// <param name="timeout">Cache timeout (default is 0 - no timeout)</param>
        /// <returns>'true' if the cache value was added, 'false' if the given key was not added</returns>
        public bool Add(string key, byte[] value, TimeSpan timeout = new TimeSpan())
        {
            using (new WriteLock(_lockEx))
            {
                // Try to find and remove the previous key
                _entriesByKey.Remove(key);

                // Update the cache entry
                _entriesByKey.Add(key, new MemCacheEntry(value, timeout));

                return true;
            }
        }

        /// <summary>
        /// Try to find the cache value by the given key
        /// </summary>
        /// <param name="key">Key to find</param>
        /// <returns>'true' and cache value if the cache value was found, 'false' if the given key was not found</returns>
        public (bool, byte[]) Find(string key)
        {
            using (new ReadLock(_lockEx))
            {
                // Try to find the given key
                if (!_entriesByKey.TryGetValue(key, out var cacheValue))
                    return (false, new byte[0]);

                return (true, cacheValue.Value);
            }
        }

        /// <summary>
        /// Remove the cache value with the given key from the file cache
        /// </summary>
        /// <param name="key">Key to remove</param>
        /// <returns>'true' if the cache value was removed, 'false' if the given key was not found</returns>
        public bool Remove(string key)
        {
            using (new WriteLock(_lockEx))
            {
                return _entriesByKey.Remove(key);
            }
        }

        #endregion

        #region Cache management methods

        /// <summary>
        /// Insert a new cache path with the given timeout into the file cache
        /// </summary>
        /// <param name="path">Path to insert</param>
        /// <param name="prefix">Cache prefix (default is "/")</param>
        /// <param name="filter">Cache filter (default is "*.*")</param>
        /// <param name="timeout">Cache timeout (default is 0 - no timeout)</param>
        /// <param name="handler">Cache insert handler (default is 'return cache.Add(key, value, timeout)')</param>
        /// <returns>'true' if the cache path was setup, 'false' if failed to setup the cache path</returns>
        public bool InsertPath(string path, string prefix = "/", string filter = "*.*", TimeSpan timeout = new TimeSpan(), InsertHandler handler = null)
        {
            handler ??= (FileCache cache, string key, byte[] value, TimeSpan timespan) => cache.Add(key, value, timespan);

            // Try to find and remove the previous path
            RemovePathInternal(path);

            using (new WriteLock(_lockEx))
            {
                // Add the given path to the cache
                _pathsByKey.Add(path, new FileCacheEntry(this, prefix, path, filter, handler, timeout));
                // Create entries by path map
                _entriesByPath[path] = new HashSet<string>();
            }

            // Insert the cache path
            if (!InsertPathInternal(path, path, prefix, timeout, handler))
                return false;

            return true;
        }

        /// <summary>
        /// Try to find the cache path
        /// </summary>
        /// <param name="path">Path to find</param>
        /// <returns>'true' if the cache path was found, 'false' if the given path was not found</returns>
        public bool FindPath(string path)
        {
            using (new ReadLock(_lockEx))
            {
                // Try to find the given key
                return _pathsByKey.ContainsKey(path);
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
            using (new WriteLock(_lockEx))
            {
                // Stop all file system watchers
                foreach (var fileCacheEntry in _pathsByKey)
                    fileCacheEntry.Value.StopWatcher();

                // Clear all cache entries
                _entriesByKey.Clear();
                _entriesByPath.Clear();
                _pathsByKey.Clear();
            }
        }

        #endregion

        #region Cache implementation

        private readonly ReaderWriterLockSlim _lockEx = new ReaderWriterLockSlim();
        private Dictionary<string, MemCacheEntry> _entriesByKey = new Dictionary<string, MemCacheEntry>();
        private Dictionary<string, HashSet<string>> _entriesByPath = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, FileCacheEntry> _pathsByKey = new Dictionary<string, FileCacheEntry>();

        private class MemCacheEntry
        {
            private readonly byte[] _value;
            private readonly TimeSpan _timespan;

            public byte[] Value => _value;
            public TimeSpan Timespan => _timespan;

            public MemCacheEntry(byte[] value, TimeSpan timespan = new TimeSpan())
            {
                _value = value;
                _timespan = timespan;
            }

            public MemCacheEntry(string value, TimeSpan timespan = new TimeSpan())
            {
                _value = Encoding.UTF8.GetBytes(value);
                _timespan = timespan;
            }
        };

        private class FileCacheEntry
        {
            private readonly string _prefix;
            private readonly string _path;
            private readonly InsertHandler _handler;
            private readonly TimeSpan _timespan;
            private readonly FileSystemWatcher _watcher;

            public FileCacheEntry(FileCache cache, string prefix, string path, string filter, InsertHandler handler, TimeSpan timespan)
            {
                _prefix = prefix;
                _path = path;
                _handler = handler;
                _timespan = timespan;
                _watcher = new FileSystemWatcher();

                // Start the filesystem watcher
                StartWatcher(cache, path, filter);
            }
            private void StartWatcher(FileCache cache, string path, string filter)
            {
                FileCacheEntry entry = this;

                // Initialize a new filesystem watcher
                _watcher.Created += (sender, e) => OnCreated(sender, e, cache, entry);
                _watcher.Changed += (sender, e) => OnChanged(sender, e, cache, entry);
                _watcher.Deleted += (sender, e) => OnDeleted(sender, e, cache, entry);
                _watcher.Renamed += (sender, e) => OnRenamed(sender, e, cache, entry);
                _watcher.Path = path;
                _watcher.IncludeSubdirectories = true;
                _watcher.Filter = filter;
                _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
                _watcher.EnableRaisingEvents = true;
            }

            public void StopWatcher()
            {
                _watcher.Dispose();
            }

            private static bool IsDirectory(string path)
            {
                try
                {
                    // Skip directory updates
                    if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                        return true;
                }
                catch (Exception) {}

                return false;
            }

            private static void OnCreated(object sender, FileSystemEventArgs e, FileCache cache, FileCacheEntry entry)
            {
                var key = e.FullPath.Replace(entry._path, entry._prefix);
                var file = e.FullPath;

                // Skip missing files
                if (!File.Exists(file))
                    return;
                // Skip directory updates
                if (IsDirectory(file))
                    return;

                cache.InsertFileInternal(entry._path, file, key, entry._timespan, entry._handler);
            }

            private static void OnChanged(object sender, FileSystemEventArgs e, FileCache cache, FileCacheEntry entry)
            {
                if (e.ChangeType != WatcherChangeTypes.Changed)
                    return;

                var key = e.FullPath.Replace(entry._path, entry._prefix);
                var file = e.FullPath;

                // Skip missing files
                if (!File.Exists(file))
                    return;
                // Skip directory updates
                if (IsDirectory(file))
                    return;

                cache.InsertFileInternal(entry._path, file, key, entry._timespan, entry._handler);
            }

            private static void OnDeleted(object sender, FileSystemEventArgs e, FileCache cache, FileCacheEntry entry)
            {
                var key = e.FullPath.Replace(entry._path, entry._prefix);
                var file = e.FullPath;

                cache.RemoveFileInternal(entry._path, key);
            }

            private static void OnRenamed(object sender, RenamedEventArgs e, FileCache cache, FileCacheEntry entry)
            {
                var oldKey = e.OldFullPath.Replace(entry._path, entry._prefix);
                var oldFile = e.OldFullPath;
                var newKey = e.FullPath.Replace(entry._path, entry._prefix);
                var newFile = e.FullPath;

                // Skip missing files
                if (!File.Exists(newFile))
                    return;
                // Skip directory updates
                if (IsDirectory(newFile))
                    return;

                cache.RemoveFileInternal(entry._path, oldKey);
                cache.InsertFileInternal(entry._path, newFile, newKey, entry._timespan, entry._handler);
            }
        };

        private bool InsertFileInternal(string path, string file, string key, TimeSpan timeout, InsertHandler handler)
        {
            try
            {
                key = key.Replace('\\', '/');
                file = file.Replace('\\', '/');

                // Load the cache file content
                var content = File.ReadAllBytes(file);
                if (!handler(this, key, content, timeout))
                    return false;

                using (new WriteLock(_lockEx))
                {
                    // Update entries by path map
                    _entriesByPath[path].Add(key);
                }

                return true;
            }
            catch (Exception) { return false; }
        }

        private bool RemoveFileInternal(string path, string key)
        {
            try
            {
                key = key.Replace('\\', '/');

                using (new WriteLock(_lockEx))
                {
                    // Update entries by path map
                    _entriesByPath[path].Remove(key);
                }

                return Remove(key);
            }
            catch (Exception) { return false; }
        }

        private bool InsertPathInternal(string root, string path, string prefix, TimeSpan timeout, InsertHandler handler)
        {
            try
            {
                string keyPrefix = (string.IsNullOrEmpty(prefix) || (prefix == "/")) ? "/" : (prefix + "/");

                // Iterate through all directory entries
                foreach (var item in Directory.GetDirectories(path))
                {
                    string key = keyPrefix + HttpUtility.UrlDecode(Path.GetFileName(item));

                    // Recursively insert sub-directory
                    if (!InsertPathInternal(root, item, key, timeout, handler))
                        return false;
                }

                foreach (var item in Directory.GetFiles(path))
                {
                    string key = keyPrefix + HttpUtility.UrlDecode(Path.GetFileName(item));

                    // Insert file into the cache
                    if (!InsertFileInternal(root, item, key, timeout, handler))
                        return false;
                }

                return true;
            }
            catch (Exception) { return false; }
        }

        private bool RemovePathInternal(string path)
        {
            using (new WriteLock(_lockEx))
            {
                // Try to find the given path
                if (!_pathsByKey.TryGetValue(path, out var cacheValue))
                    return false;

                // Stop the file system watcher
                cacheValue.StopWatcher();

                // Remove path entries
                foreach (var entryKey in _entriesByPath[path])
                    _entriesByKey.Remove(entryKey);
                _entriesByPath.Remove(path);

                // Remove cache path
                _pathsByKey.Remove(path);

                return true;
            }
        }

        #endregion

        #region IDisposable implementation

        // Disposed flag.
        private bool _disposed;

        // Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposingManagedResources)
        {
            // The idea here is that Dispose(Boolean) knows whether it is
            // being called to do explicit cleanup (the Boolean is true)
            // versus being called due to a garbage collection (the Boolean
            // is false). This distinction is useful because, when being
            // disposed explicitly, the Dispose(Boolean) method can safely
            // execute code using reference type fields that refer to other
            // objects knowing for sure that these other objects have not been
            // finalized or disposed of yet. When the Boolean is false,
            // the Dispose(Boolean) method should not execute code that
            // refer to reference type fields because those objects may
            // have already been finalized."

            if (!_disposed)
            {
                if (disposingManagedResources)
                {
                    // Dispose managed resources here...
                    Clear();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                _disposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~FileCache()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// Disposable lock class performs exit action on dispose operation.
    /// </summary>
    public class DisposableLock : IDisposable
    {
        private readonly Action _exitLock;

        public DisposableLock(Action exitLock)
        {
            _exitLock = exitLock;
        }

        public void Dispose()
        {
            _exitLock();
        }
    }

    /// <summary>
    /// Read lock class enters read lock on construction and performs exit read lock on dispose.
    /// </summary>
    public class ReadLock : DisposableLock
    {
        public ReadLock(ReaderWriterLockSlim locker) : base(locker.ExitReadLock)
        {
            locker.EnterReadLock();
        }
    }

    /// <summary>
    /// Write lock class enters write lock on construction and performs exit write lock on dispose.
    /// </summary>
    public class WriteLock : DisposableLock
    {
        public WriteLock(ReaderWriterLockSlim locker) : base(locker.ExitWriteLock)
        {
            locker.EnterWriteLock();
        }
    }
}
