using System;
using System.Diagnostics;
using System.Text;

namespace NetCoreServer
{
    /// <summary>
    /// Dynamic byte buffer
    /// </summary>
    public class Buffer
    {
        private byte[] _data;
        private long _size;

        /// <summary>
        /// Is the buffer empty?
        /// </summary>
        public bool IsEmpty => (_data == null) || (_size == 0);
        /// <summary>
        /// Bytes memory buffer
        /// </summary>
        public byte[] Data => _data;
        /// <summary>
        /// Bytes memory buffer capacity
        /// </summary>
        public long Capacity => _data.Length;
        /// <summary>
        /// Bytes memory buffer size
        /// </summary>
        public long Size => _size;

        /// <summary>
        /// Initialize a new expandable buffer with zero capacity
        /// </summary>
        public Buffer() { _data = new byte[0]; _size = 0; }
        /// <summary>
        /// Initialize a new expandable buffer with the given capacity
        /// </summary>
        public Buffer(long capacity) { _data = new byte[capacity]; _size = 0; }

        #region Memory buffer methods

        /// <summary>
        /// Clear the buffer
        /// </summary>
        public void Clear()
        {
            _size = 0;
        }

        /// <summary>
        /// Reserve the buffer of the given capacity
        /// </summary>
        public void Reserve(long capacity)
        {
            Debug.Assert((capacity >= 0), "Invalid reserve capacity!");
            if (capacity < 0)
                throw new ArgumentException("Invalid reserve capacity!", nameof(capacity));

            if (capacity > Capacity)
            {
                byte[] data = new byte[Math.Max(capacity, 2 * Capacity)];
                Array.Copy(_data, 0, data, 0, _size);
                _data = data;
            }
        }

        #endregion

        #region Buffer I/O methods

        /// <summary>
        /// Append the given buffer
        /// </summary>
        /// <param name="buffer">Buffer to append</param>
        /// <returns>Count of append bytes</returns>
        public long Append(byte[] buffer)
        {
            Reserve(_size + buffer.Length);
            Array.Copy(buffer, 0, _data, _size, buffer.Length);
            _size += buffer.Length;
            return buffer.Length;
        }

        /// <summary>
        /// Append the given buffer fragment
        /// </summary>
        /// <param name="buffer">Buffer to append</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>Count of append bytes</returns>
        public long Append(byte[] buffer, long offset, long size)
        {
            Reserve(_size + size);
            Array.Copy(buffer, offset, _data, _size, size);
            _size += size;
            return size;
        }

        /// <summary>
        /// Append the given text in UTF-8 encoding
        /// </summary>
        /// <param name="text">Text to append</param>
        /// <returns>Count of append bytes</returns>
        public long Append(string text)
        {
            Reserve(_size + Encoding.UTF8.GetMaxByteCount(text.Length));
            long result = Encoding.UTF8.GetBytes(text, 0, text.Length, _data, (int)_size);
            _size += result;
            return result;
        }

        #endregion
    }
}
