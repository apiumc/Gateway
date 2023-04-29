using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Net;
using System.Text;
using UMC.Net;

namespace UMC.Host
{

    public class HttpMimeResponse
    {
        protected static readonly byte[] ChunkedEnd = Encoding.ASCII.GetBytes($"0\r\n\r\n");
        public int StatusCode
        {
            get;
            set;
        }
        public string ContentType
        {
            get;
            set;
        }
        public long? ContentLength { set; get; }
        HttpMime _context;
        HttpMimeRequest _req;
        public HttpMimeResponse(HttpMime context, HttpMimeRequest request)
        {
            this._req = request;
            this.StatusCode = 200;
            this._context = context;
            this.bodyStream = new BodyStream(this);
        }
        class Header
        {
            public String Name;
            public String Value;
        }
        List<Header> headers = new List<Header>();
        public void AddHeader(string name, string value)
        {
            if (this.lengthWrite == 0)
            {
                headers.Add(new Header { Name = name, Value = value });
            }
            else
            {
                UMC.Data.Utility.Debug("Http", DateTime.Now, "内容已经输出不可再追加Header", _req.Url.AbsoluteUri);
            }
        }

        public void Redirect(string url)
        {
            if (this.lengthWrite == 0)
            {
                this.StatusCode = 302;
                this.AddHeader("Location", url);
                this.ContentLength = null;
                this.isChunked = false;
                this.WriteHeader();
                this.lengthWrite = -1;
            }
            else
            {
                UMC.Data.Utility.Debug("Http", DateTime.Now, "内容已经输出不可再重定向", _req.Url.AbsoluteUri);
            }

        }
        public void AppendCookie(string name, string value)
        {
            if (String.Equals(name, UMC.Web.WebServlet.SessionCookieName))
            {
                this.AddHeader("Set-Cookie", $"{name}={value}; Expires={DateTime.Now.AddYears(10).ToString("r")}; Path=/");

            }
            else
            {
                this.AddHeader("Set-Cookie", $"{name}={value}; Path=/");
            }
        }
        public void AppendCookie(string name, string value, string path)
        {
            this.AddHeader("Set-Cookie", $"{name}={value}; Path={path}");
        }
        BodyStream bodyStream;
        public System.IO.Stream OutputStream
        {
            get
            {
                return bodyStream;
            }
        }
        bool isChunked;

        void WriteHeader()
        {
            var header = new UMC.Net.TextWriter(_context.Write);
            try
            {
                header.Write($"HTTP/1.1 {this.StatusCode} {HttpStatusDescription.Get(this.StatusCode)}\r\n");
                foreach (var h in this.headers)
                {
                    header.Write($"{h.Name}: {h.Value}\r\n");
                }
                if (String.IsNullOrEmpty(this.ContentType) == false)
                {
                    header.Write($"Content-Type: {this.ContentType}\r\n");
                }

                if (this.ContentLength.HasValue && this.ContentLength > 0)
                {
                    header.Write($"Content-Length: {this.ContentLength}\r\n");
                }
                else if (isChunked)
                {
                    header.Write("Transfer-Encoding: chunked\r\n");
                }
                else
                {
                    if (this.headers.Exists(r =>
                    {
                        switch (r.Name.ToLower())
                        {
                            case "content-length":
                            case "transfer-encoding":
                                return true;
                        }
                        return false;

                    }) == false)
                    {
                        header.Write("Content-Length: 0\r\n");
                    }
                }

                if (_IsClose || _req.IsClose)
                {
                    header.Write("Connection: close\r\n");
                }
                else
                {
                    header.Write("Keep-Alive: timeout=20\r\n");
                    header.Write("Connection: keep-alive\r\n");
                }
                header.Write("Server: UMC.Proxy\r\n\r\n");
            }
            finally
            {
                header.Flush();
                header.Dispose();
            }
        }
        bool _IsClose;
        public void OutputError(Exception ex)
        {
            if (lengthWrite == 0)
            {
                //_context.er
                var errStr = ex.ToString();
                var sbytes = System.Buffers.ArrayPool<Byte>.Shared.Rent(errStr.Length * 2);
                try
                {
                    this.StatusCode = 500;
                    _IsClose = true;

                    this.ContentType = "text/plain; charset=utf-8";
                    var blength = Encoding.UTF8.GetBytes(errStr, sbytes);
                    this.ContentLength = blength;//.Length;
                    this.isChunked = false;

                    bodyStream.Write(sbytes, 0, blength);
                }
                finally
                {
                    System.Buffers.ArrayPool<Byte>.Shared.Return(sbytes);
                }

            }
            else if (this.isChunked)
            {
                _context.Write(ChunkedEnd, 0, ChunkedEnd.Length);
            }
        }
        public bool OutputFinish()
        {
            try
            {
                if (lengthWrite == -1)
                {
                    return true;
                }
                else if (lengthWrite == 0)
                {
                    if (this.ContentLength > 0)
                    {
                        _IsClose = true;

                        UMC.Data.Utility.Debug("Http", DateTime.Now, $"输入内容长度只能是{this.ContentLength},但只输出了{lengthWrite}", _req.Url.AbsoluteUri);
                        this.ContentLength = 0;
                        this.isChunked = false;
                        WriteHeader();
                        lengthWrite = -1;
                        return false;
                    }
                    else
                    {
                        this.ContentLength = 0;
                        this.isChunked = false;
                        WriteHeader();
                        lengthWrite = -1;
                        return true;
                    }

                }
                else if (this.isChunked)
                {
                    this.bodyStream.Flush();

                    _context.Write(ChunkedEnd, 0, ChunkedEnd.Length);

                    return true;
                }
                else
                {
                    return lengthWrite == this.ContentLength;
                }
            }
            finally
            {
                headers.Clear();
                this.bodyStream.Dispose();
            }

        }
        long lengthWrite = 0;
        class BodyStream : System.IO.Stream
        {
            HttpMimeResponse response;
            public BodyStream(HttpMimeResponse response)
            {
                this.response = response;
            }
            public override bool CanRead => false;

            public override bool CanSeek => false;


            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush()
            {

            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                switch (response.lengthWrite)
                {
                    case -1:
                        UMC.Data.Utility.Debug("Http", DateTime.Now, "内容输出已经关闭，不可再写入内容", response._req.Url.AbsoluteUri);
                        return;
                    case 0:
                        response.isChunked = response.ContentLength.HasValue == false || response.ContentLength <= 0;
                        response.WriteHeader();

                        break;
                }
                response.lengthWrite += count;
                if (response.ContentLength > 0)
                {
                    if (response.ContentLength < response.lengthWrite)
                    {
                        UMC.Data.Utility.Debug("Http", DateTime.Now, $"输入内容长度只能是{response.ContentLength}", response._req.Url.AbsoluteUri);
                        var size = response.ContentLength.Value - (response.lengthWrite - count);
                        if (size > 0)
                        {
                            response._context.Write(buffer, offset, (int)size);
                        }
                        response._IsClose = true;
                    }
                    else
                    {
                        response._context.Write(buffer, offset, count);
                    }
                }
                else if (count > 0)
                {
                    var str = $"{(count).ToString("x")}\r\n";
                    var bytes = System.Buffers.ArrayPool<byte>.Shared.Rent(str.Length);
                    try
                    {
                        response._context.Write(bytes, 0, Encoding.ASCII.GetBytes(str, bytes));
                        response._context.Write(buffer, offset, count);
                        response._context.Write(HttpMimeBody.HeaderEnd, 0, 2);
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(bytes);
                    }
                }
            }
        }
    }


}