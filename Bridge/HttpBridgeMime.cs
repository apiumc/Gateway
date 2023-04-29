using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using UMC.Net;

namespace UMC.Host
{

    class HttpBridgeMime : HttpMime
    {
        HttpBridgeClient client;
        public HttpBridgeMime(int pid, HttpBridgeClient client)
        {
            this.client = client;
            this.pid = pid;
        }
        int pid;
        public override string Host => "127.0.0.1";

        public override string RemoteIpAddress => "127.0.0.1";

        public override void Dispose()
        {
            if (_webSocket != null)
            {
                _webSocket.Close();
                _webSocket.Dispose();
            }
            client.Remove(this.pid);
        }
        public void WebSocket(byte[] buffer, int offset, int count)
        {
            try
            {
                mimeStream?.Write(buffer, offset, count);
                _webSocket?.Write(buffer, offset, count);
            }
            catch
            {
                client.Write(this.pid, new byte[0], 0, 0);
                this.Dispose();
            }
        }
        public override void Subscribe(HttpMimeRequest webRequest)
        {
            this.OutText(500, "穿透不支持UMC数据订阅协议");
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
                mimeStream = new HttpMimeStream(this);
                HttpWebSocket.AcceptWebSocketAsyncCore(context, mimeStream);// new HttpMimeStream(this));
            }
        }
        HttpMimeStream mimeStream;
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


                if (url.Scheme == "https")
                {
                    SslStream ssl = new SslStream(new NetworkStream(client, true), false, (sender, certificate, chain, sslPolicyErrors) => true);

                    await ssl.AuthenticateAsClientAsync(url.Host, new X509CertificateCollection(), SslProtocols.None, false);
                    _webSocket = ssl;
                }
                else
                {
                    _webSocket = new NetworkStream(client, true);

                }
                var buffer = new byte[0x600];
                await _webSocket.WriteAsync(buffer, 0, NetHttpResponse.Header(webRequest, buffer));
                WebSocketRead(buffer);

            }
            catch (Exception ex)
            {
                this.OutText(500, ex.ToString());
                this.Dispose();
            }
        }
        Stream _webSocket;
        async void WebSocketRead(byte[] buffer)
        {
            int size = 0;
            try
            {
                size = await _webSocket.ReadAsync(buffer, 0, buffer.Length);
                client.Write(this.pid, buffer, 0, size);
                WebSocketRead(buffer);
            }
            catch
            {
                client.Write(this.pid, Array.Empty<byte>(), 0, 0);
                this.Dispose();
            }
        }
        public override void OutputFinish()
        {

        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > 0)
            {

                client.Write(this.pid, buffer, offset, count);
            }
        }
    }
}

