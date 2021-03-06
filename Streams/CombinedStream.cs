﻿using System;
using System.IO;

namespace Streams
{
    /// <summary>
    /// This class is a <see cref="Stream"/> descendant that manages multiple underlying
    /// streams which are considered to be chained together to one large stream. Only reading
    /// and seeking is allowed, writing will throw exceptions.
    /// </summary>
    public class CombinedStream : Stream
    {
        private readonly Stream[] _UnderlyingStreams;
        private readonly long[] _UnderlyingStartingPositions;
        private readonly long _TotalLength;

        private long _Position;
        private int _Index;

        /// <summary>
        /// Constructs a new <see cref="CombinedStream"/> on top of the specified array
        /// of streams.
        /// </summary>
        /// <param name="underlyingStreams">
        /// An array of <see cref="Stream"/> objects that will be chained together and
        /// considered to be one big stream.
        /// </param>
        public CombinedStream(params Stream[] underlyingStreams)
        {
            if (underlyingStreams == null)
                throw new ArgumentNullException("underlyingStreams");
            foreach (Stream stream in underlyingStreams)
            {
                if (stream == null)
                    throw new ArgumentException("underlyingStreams contains a null stream reference", "underlyingStreams");
                if (!stream.CanRead)
                    throw new InvalidOperationException("CanRead not true for all streams");
                if (!stream.CanSeek)
                    throw new InvalidOperationException("CanSeek not true for all streams");
            }

            _UnderlyingStreams = new Stream[underlyingStreams.Length];
            _UnderlyingStartingPositions = new long[underlyingStreams.Length];
            Array.Copy(underlyingStreams, _UnderlyingStreams, underlyingStreams.Length);

            _Position = 0;
            _Index = 0;

            _UnderlyingStartingPositions[0] = 0;
            for (int index = 1; index < _UnderlyingStartingPositions.Length; index++)
                _UnderlyingStartingPositions[index] = _UnderlyingStartingPositions[index - 1] + _UnderlyingStreams[index - 1].Length;

            _TotalLength = _UnderlyingStartingPositions[_UnderlyingStartingPositions.Length - 1] + _UnderlyingStreams[_UnderlyingStreams.Length - 1].Length;
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <value>
        /// <c>true</c>.
        /// </value>
        /// <returns>
        /// Always <c>true</c> for <see cref="CombinedStream"/>.
        /// </returns>
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <value>
        /// <c>true</c>.
        /// </value>
        /// <returns>
        /// Always <c>true</c> for <see cref="CombinedStream"/>.
        /// </returns>
        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <value>
        /// <c>false</c>.
        /// </value>
        /// <returns>
        /// Always <c>false</c> for <see cref="CombinedStream"/>.
        /// </returns>
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        public override void Flush()
        {
            foreach (Stream stream in _UnderlyingStreams)
                stream.Flush();
        }

        /// <summary>
        /// Gets the total length in bytes of the underlying streams.
        /// </summary>
        /// <value>
        /// The total length of the underlying streams.
        /// </value>
        /// <returns>
        /// A long value representing the total length of the underlying streams in bytes.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">A class derived from Stream does not support seeking. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Length
        {
            get
            {
                return _TotalLength;
            }
        }

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        /// <value></value>
        /// <returns>The current position within the stream.</returns>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support seeking. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Position
        {
            get
            {
                return _Position;
            }

            set
            {
                if (value < 0)
                     throw new IOException("An attempt was made to move the position before the beginning of the stream.");

                _Position = value;
                if (value == _TotalLength)
                    _Index = _UnderlyingStreams.Length - 1;
                else
                {
                    while (_Index > 0 && _Position < _UnderlyingStartingPositions[_Index])
                        _Index--;

                    while (_Index < _UnderlyingStreams.Length - 1 && _Position >= _UnderlyingStartingPositions[_Index] + _UnderlyingStreams[_Index].Length)
                        _Index++;
                }
            }
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="T:System.ArgumentException">The sum of offset and count is larger than the buffer length. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
        /// <exception cref="T:System.ArgumentNullException">buffer is null. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int result = 0;
            while (count > 0)
            {
                _UnderlyingStreams[_Index].Position = _Position - _UnderlyingStartingPositions[_Index];
                int bytesRead = _UnderlyingStreams[_Index].Read(buffer, offset, count);
                result += bytesRead;
                offset += bytesRead;
                count -= bytesRead;
                _Position += bytesRead;

                if (count > 0)
                {
                    if (_Index < _UnderlyingStreams.Length - 1)
                        _Index++;
                    else
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the origin parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"></see> indicating the reference point used to obtain the new position.</param>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;

                case SeekOrigin.Current:
                    Position += offset;
                    break;

                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
            }

            return Position;
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> since the <see cref="CombinedStream"/>
        /// class does not supports changing the length.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="T:System.NotSupportedException">
        /// <see cref="CombinedStream"/> does not support this operation.
        /// </exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("The method or operation is not supported by CombinedStream.");
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> since the <see cref="CombinedStream"/>
        /// class does not supports writing to the underlying streams.
        /// </summary>
        /// <param name="buffer">An array of bytes.  This method copies count bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="T:System.NotSupportedException">
        /// <see cref="CombinedStream"/> does not support this operation.
        /// </exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("The method or operation is not supported by CombinedStream.");
        }
    }
}