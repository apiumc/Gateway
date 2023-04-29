using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using UMC.Net;

namespace UMC.Host
{

    class HttpMimeStream : System.IO.Stream, IValueTaskSource<int>
    {
        private ManualResetValueTaskSourceCore<int> _source = new ManualResetValueTaskSourceCore<int>();

        #region 实现接口，告诉调用者，任务是否已经完成，以及是否有结果，是否有异常等
        // 获取结果
        public int GetResult(short token)
        {
            return _source.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return _source.GetStatus(token); ;
        }

        // 实现延续
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _source.OnCompleted(continuation, state, token, flags);
        }

        #endregion


        // 以及完成任务，并给出结果
        public void SetResult(int result)
        {
            _source.SetResult(result);
        }
        short _token = 0;
        // 要执行的任务出现异常
        public void SetException(Exception exception)
        {
            _source.SetException(exception);
        }


        private int _disposed;


        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }
        HttpMime _mime;
        public HttpMimeStream(HttpMime mime)
        {
            this._mime = mime;
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count).Result;
        }
        public void AppendData(byte[] buffer, int offset, int size)
        {
            if (_buffers.IsEmpty == false)
            {
                if (_buffers.Length >= size)
                {
                    buffer.AsMemory(offset, size).CopyTo(_buffer);
                    _source.SetResult(size);
                }
                else
                {
                    buffer.AsMemory(offset, _buffers.Length).CopyTo(_buffer);
                    int len = size - _buffers.Length;
                    if (len + _bufferSize > _buffer.Length)
                    {
                        var _bs = new byte[len + _bufferSize + 200];
                        Array.Copy(_buffer, 0, _bs, 0, _bufferSize);
                        _buffer = _bs;
                    }
                    Array.Copy(buffer, offset + _buffer.Length, _buffer, _bufferSize, len);


                    _source.SetResult(_buffers.Length);

                }
            }
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            _mime.Write(buffer, offset, count);
        }



        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            base.Dispose(disposing);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Stream.ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }
        byte[] _buffer = Array.Empty<byte>();
        int _bufferSize = 0;
        Memory<byte> _buffers;
        //int _start = 0;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_bufferSize == 0)
            {
                _token++;
                _buffers = buffer;
                _source.Reset();
                return new ValueTask<int>(this, _token);
            }
            if (buffer.Length >= _bufferSize)
            {
                int len = _bufferSize;
                _buffer.AsMemory(0, _bufferSize).CopyTo(buffer);
                _bufferSize = 0;

                return new ValueTask<int>(len);
            }
            else
            {
                _buffer.AsMemory(0, buffer.Length).CopyTo(buffer);
                _bufferSize -= buffer.Length;
                Array.Copy(_buffer, buffer.Length, _buffer, 0, _bufferSize);

                return new ValueTask<int>(buffer.Length);


            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Stream.ValidateBufferArguments(buffer, offset, count);
            _mime.Write(buffer, offset, count);
            return Task.CompletedTask;
            //NetworkStream 
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(array);
                _mime.Write(array, 0, buffer.Length);
                return ValueTask.CompletedTask;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }

        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

    }
}

