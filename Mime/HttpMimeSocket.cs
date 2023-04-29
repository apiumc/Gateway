using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using UMC.Net;

namespace UMC.Host
{

    public class HttpMimeSocket : HttpMime
    {

        String _scheme = "http";
        public override string Scheme => _scheme;
        int _timeOut = 20;
        public int TimeOut => _timeOut;

        public HttpMimeSocket(String scheme, System.IO.Stream stream, String host, String ip)
        {
            _scheme = scheme;
            this._stream = stream;
            this.ActiveTime = UMC.Data.Utility.TimeSpan();

            this.pid = stream.GetHashCode();

            this._Host = host;

            this._remoteIpAddress = ip;

            HttpMimeServier.httpMimes.TryAdd(pid, this);
            Read(new HttpMimeRequest(this));

        }
        Stream _stream;
        int pid = 0;
        public int Id => pid;
        String _remoteIpAddress, _Host;
        public override String Host => _Host;
        public override String RemoteIpAddress => _remoteIpAddress;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (isDispose == false)
            {
                try
                {

                    _stream.Write(buffer, offset, count);

                }
                catch
                {
                    this.Dispose();
                }
            }
        }



        public override void OutputFinish()
        {
            try
            {
                Read(new HttpMimeRequest(this));

                this.ActiveTime = UMC.Data.Utility.TimeSpan();
                _timeOut = 20;
            }
            catch
            {
                this.Dispose();
            }
        }
        public override void PrepareRespone(HttpMimeRequest httpMimeRequest)
        {
            _timeOut = 300;
            base.PrepareRespone(httpMimeRequest);
        }
        public override void Subscribe(HttpMimeRequest webRequest)
        {
            var _subscribe = UMC.Net.NetSubscribe.Subscribe(webRequest.Headers, webRequest.UserHostAddress, HttpMimeServier.Server, _stream, UMC.Data.WebResource.Instance().Provider["appSecret"]);
            if (_subscribe != null)
            {
                HttpMimeSocket link;
                HttpMimeServier.httpMimes.TryRemove(pid, out link);

                var writer = new Net.TextWriter(this.Write, _data);
                writer.Write($"HTTP/1.1 101 {HttpStatusDescription.Get(101)}\r\n");
                writer.Write("Connection: upgrade\r\n");
                writer.Write("Upgrade: websocket\r\n");
                writer.Write($"UMC-Publisher-Key: {HttpMimeServier.Server}\r\n");
                writer.Write("Server: UMC.Proxy\r\n\r\n");

                writer.Flush();
                writer.Dispose();
                _subscribe.Publish();
            }
            else
            {
                OutText(401, "连接验证不通过");
            }
        }
        protected override void WebSocket(NetContext context)
        {
            if (context.Tag is HttpWebRequest)
            {
                var webr = context.Tag as HttpWebRequest;
                this.WebSocket(webr);
            }
            else
            {
                HttpWebSocket.AcceptWebSocketAsyncCore(context, _stream);
            }
        }
        public int ActiveTime
        {

            get; set;
        }
        bool isDispose = false;
        public override void Dispose()
        {
            if (this._data != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(this._data);
            }
            if (isDispose == false)
            {
                isDispose = true;
                try
                {
                    _stream.Close();
                    _stream.Dispose();
                    _data = null;
                    if (_webSocket != null)
                    {
                        _webSocket.Dispose();
                    }
                }
                catch
                {

                }
            }
            HttpMimeServier.httpMimes.TryRemove(pid, out var _);

        }
        async void WebSocket(HttpWebRequest webRequest)
        {
            try
            {
                var url = webRequest.RequestUri;
                if (webRequest.CookieContainer != null)
                {
                    String cookie;
                    if (webRequest.CookieContainer is Net.NetCookieContainer)
                    {
                        cookie = ((Net.NetCookieContainer)webRequest.CookieContainer).GetCookieHeader(url);
                    }
                    else
                    {
                        cookie = webRequest.CookieContainer.GetCookieHeader(url);
                    }
                    if (String.IsNullOrEmpty(cookie) == false)
                    {
                        webRequest.Headers[HttpRequestHeader.Cookie] = cookie;
                    }
                }
                if (String.IsNullOrEmpty(webRequest.Headers[HttpRequestHeader.Host]))
                {
                    webRequest.Headers[HttpRequestHeader.Host] = webRequest.Host;
                }
                webRequest.Headers["Connection"] = "Upgrade";


                var client = new Socket(SocketType.Stream, ProtocolType.Tcp);

                await client.ConnectAsync(url.Host, url.Port);

                _webSocket = new WebSocketer();

                if (url.Scheme == "https")
                {
                    SslStream ssl = new SslStream(new NetworkStream(client, true), false, (sender, certificate, chain, sslPolicyErrors) => true);
                    await ssl.AuthenticateAsClientAsync(url.Host, new X509CertificateCollection(), SslProtocols.None, false);
                    _webSocket.stream = ssl;
                }
                else
                {
                    _webSocket.stream = new NetworkStream(client, true);

                }
                WebSocketWrite();
                await _webSocket.stream.WriteAsync(_webSocket.buffer, 0, UMC.Net.NetHttpResponse.Header(webRequest, _webSocket.buffer));
                WebSocketRead();

                HttpMimeServier.httpMimes.TryRemove(this.pid, out var _);
            }
            catch (Exception ex)
            {
                OutText(500, ex.ToString());
            }
        }

        class WebSocketer : IDisposable
        {
            public byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(0x600);//new byte[0x600];
            public System.IO.Stream stream;

            public void Dispose()
            {
                stream.Close();
                stream.Dispose();
                buffer = null;
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        WebSocketer _webSocket;
        byte[] _data = System.Buffers.ArrayPool<byte>.Shared.Rent(0x600);// new byte[0x600];

        async void WebSocketRead()
        {
            int size = 0;
            try
            {
                size = await _webSocket.stream.ReadAsync(_webSocket.buffer, 0, _webSocket.buffer.Length);

                if (size > 0)
                {
                    await _stream.WriteAsync(_webSocket.buffer, 0, size);
                    WebSocketRead();
                }
                else
                {
                    this.Dispose();
                }
            }
            catch
            {
                this.Dispose();
            }
        }
        async void WebSocketWrite()
        {
            this.ActiveTime = UMC.Data.Utility.TimeSpan();
            int size = 0;
            try
            {
                size = await _stream.ReadAsync(this._data, 0, this._data.Length);

                await _webSocket.stream.WriteAsync(_data, 0, size);


                WebSocketWrite();

            }
            catch
            {
                this.Dispose();
            }
        }
        async void Read(HttpMimeRequest req)
        {
            int size = 0;
            try
            {
                size = await _stream.ReadAsync(this._data, 0, this._data.Length);
            }
            catch
            {
                this.Dispose();
                return;
            }
            if (size > 0)
            {
                this.ActiveTime = UMC.Data.Utility.TimeSpan();
                try
                {
                    req.Receive(this._data, 0, size);
                }
                catch (Exception ex)
                {
                    this.OutText(500, ex.ToString());

                    return;
                }
            }
            else
            {
                this.Dispose();
                return;
            }

            if (req.IsHttpFormatError)
            {
                req.Dispose();
                this.Dispose();
                return;
            }

            if (req.IsWebSocket == false && req.IsMimeFinish == false)
            {
                Read(req);
            }

        }

    }
}

