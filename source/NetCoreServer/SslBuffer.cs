using System;
using System.IO;
using System.Net.Sockets;

namespace NetCoreServer
{
    /// <summary>
    /// SSL inner stream buffer
    /// </summary>
    public class SslBuffer : Stream
    {
        /// <summary>
        /// Initialize SSL inner stream with a given network stream
        /// </summary>
        /// <param name="networkStream">Network stream</param>
        /// <param name="receiveBufferCapacity">Initial receive buffer capacity</param>
        /// <param name="sendBufferCapacity">Initial send buffer capacity</param>
        public SslBuffer(NetworkStream networkStream, int receiveBufferCapacity, int sendBufferCapacity)
        {
            IsNetworkStream = true; 
            NetworkStream = networkStream;
            ReceiveBuffer = new Buffer(receiveBufferCapacity);
            SendBuffer = new Buffer(sendBufferCapacity);
        }

        /// <summary>
        /// Is using network stream?
        /// </summary>
        public bool IsNetworkStream { get; internal set; }

        /// <summary>
        /// Network stream
        /// </summary>
        public NetworkStream NetworkStream { get; }
        /// <summary>
        /// Receive buffer
        /// </summary>
        public Buffer ReceiveBuffer { get; }
        /// <summary>
        /// Send buffer
        /// </summary>
        public Buffer SendBuffer { get; }

        #region Stream implementation

        public override bool CanRead => true;
        public override bool CanSeek => throw new NotImplementedException();
        public override bool CanWrite => true;
        public override long Length => throw new NotImplementedException();
        public override long Position 
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }
        public override void SetLength(long value) { throw new NotImplementedException(); }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read
        /// </summary>
        /// <param name="buffer">An array of bytes</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream</param>
        /// <returns>The total number of bytes read into the buffer</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (IsNetworkStream)
                return NetworkStream.Read(buffer, offset, count);

            long size = Math.Min(ReceiveBuffer.Size - ReceiveBuffer.Offset, count);
            Array.Copy(ReceiveBuffer.Data, ReceiveBuffer.Offset, buffer, offset, size);
            ReceiveBuffer.Shift(size);
            return (int)size;
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written
        /// </summary>
        /// <param name="buffer">An array of bytes</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream</param>
        /// <param name="count">The number of bytes to be written to the current stream</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (IsNetworkStream)
            {
                NetworkStream.Write(buffer, offset, count);
                return;
            }

            SendBuffer.Append(buffer, offset, count);
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device
        /// </summary>
        public override void Flush()
        {
            if (IsNetworkStream)
                NetworkStream.Flush();
        }

        #endregion

        #region IDisposable implementation

        // Disposed flag.
        private bool _disposed;

        protected override void Dispose(bool disposingManagedResources)
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
                    NetworkStream.Dispose();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                _disposed = true;
            }

            // Call Dispose in the base class.
            base.Dispose(disposingManagedResources);
        }

        // The derived class does not have a Finalize method
        // or a Dispose method without parameters because it inherits
        // them from the base class.

        #endregion
    }
}
