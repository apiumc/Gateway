using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UMC.Data;
using UMC.Net;
using UMC.Web;

namespace UMC.Host
{
    class HttpBridgeClient : NetBridge
    {
        static System.Threading.Timer _timer;
        static HttpBridgeClient()
        {
            _timer = new System.Threading.Timer(CheckTimeOut, null, 20000, 20000);

        }
        static void CheckTimeOut(Object state)
        {
            var cs = _bridgeClients.ToArray();
            foreach (var c in cs)
            {
                c?.Write(0, new byte[0], 0, 0);
            }
        }
        private static List<HttpBridgeClient> _bridgeClients = new List<HttpBridgeClient>();

        public static void Start(String key, String host, int port, int count)
        {
            if (_bridgeClients.Count > 0)
            {
                var vs = _bridgeClients.ToArray();
                foreach (var vi in vs)
                {
                    vi._Stop();
                }
                _bridgeClients.Clear();

            }

            for (var i = 0; i < count; i++)
            {
                try
                {
                    Connect(key, host, port, i, true);
                }
                catch
                {
                    break;
                }
            }

        }
        String host, ip;
        int port, index;
        static void Connect(String host, String ip, int port, int index, bool start)
        {
            var client = new Socket(SocketType.Stream, ProtocolType.Tcp);

            client.Connect(ip, port);
            var bridge = new NetworkStream(client, true);
            var buffers = new byte[2000];
            var writer = new UMC.Net.TextWriter(bridge.Write, buffers);
            writer.Write($"GET / HTTP/1.1\r\n");
            writer.Write("Connection: upgrade\r\n");
            writer.Write("umc-request-protocol: bridge\r\n");
            writer.Write("Upgrade: websocket\r\n");
            if (index == 0 && start)
            {
                writer.Write($"umc-bridge-number: -1\r\n");
            }
            else
            {
                writer.Write($"umc-bridge-number: {index}\r\n");
            }
            writer.Write($"Host: {host}\r\n\r\n");
            writer.Flush();
            writer.Dispose();
            int i = bridge.Read(buffers);

            var end = UMC.Data.Utility.FindIndex(buffers, 0, i, HttpMimeBody.HeaderEnd);
            if (end > -1)
            {
                var headSize = end + 4;
                HttpStatusCode m_StatusCode;
                var header = new NameValueCollection();
                if (ResponseHeader(buffers, 0, headSize, header, out m_StatusCode))
                {
                    if (m_StatusCode == HttpStatusCode.SwitchingProtocols)
                    {
                        var bridgeClient = new HttpBridgeClient();
                        bridgeClient.host = host;
                        bridgeClient.ip = ip;
                        bridgeClient.index = index;
                        bridgeClient.port = port;
                        bridgeClient.Bridge(bridge, bridge);
                        if (i > end + 4)
                        {
                            bridgeClient.Receive(buffers, headSize, i - headSize);
                        }
                        _bridgeClients.Add(bridgeClient);
                    }
                }

            }

        }

        public static String ServerChange(int states)
        {
            var sb = new StringBuilder();
            switch (states % 3)
            {
                case 0:
                    {

                        var secret = WebResource.Instance().Provider["appSecret"];
                        
                        if (String.IsNullOrEmpty(secret))
                        {
                            sb.AppendLine("获取失败：\a应用未注册，请注册应用。");
                        }
                        else
                        {

                            var webr = new Uri(APIProxy.Uri, "Transfer").WebRequest();

                            var ns = new System.Collections.Specialized.NameValueCollection();

                            UMC.Proxy.Utility.Sign(webr, ns, secret);
                            try
                            {
                                var meta = JSON.Deserialize<WebMeta>(webr.Get().ReadAsString());
                                if (meta.ContainsKey("msg"))
                                {
                                    sb.AppendLine($"获取失败：\a{meta["msg"]}。");

                                }
                                else
                                {
                                    var scheme = meta["scheme"] ?? "http";

                                    if (IsRunning)
                                    {
                                        sb.AppendLine($"服务状态：\b已连接");
                                    }
                                    else
                                    {
                                        sb.AppendLine($"服务状态：未连接");
                                    }
                                    sb.AppendLine($"Web VPN ：{scheme}://{meta["domain"]}/");



                                    sb.AppendLine($"流量过期：{meta["expireTime"]}");
                                    sb.AppendLine($"剩余流量：{meta["allowSize"]}");
                                    sb.AppendLine($"上行流量：{meta["inputSize"]}");
                                    sb.AppendLine($"下行流量：{meta["outputSize"]}");

                                    sb.AppendLine();
                                    sb.AppendLine("\a特别注意：过期后剩余流量将会清零!!!");

                                }
                            }
                            catch (Exception ex)
                            {
                                sb.AppendLine($"获取失败：\a{ex.Message}");
                            }
                        }
                    }
                    break;
                case 1:
                    if (_bridgeClients.Count > 0)
                    {
                        sb.AppendLine("正在关停Web VPN服务。");
                        Stop();
                    }
                    else
                    {
                        sb.AppendLine("Web VPN服务未开启。");
                    }
                    break;
                case 2:
                    {
                        var secret = WebResource.Instance().Provider["appSecret"];
                        if (String.IsNullOrEmpty(secret))
                        {
                            sb.AppendLine($"Web VPN开启失败!!!");
                            sb.AppendLine("失败原因：\a应用未注册，请注册应用。");
                        }
                        else if (_bridgeClients.Count > 0)
                        {
                            return ServerChange(0);
                        }
                        else
                        {
                            var webr = new Uri(APIProxy.Uri, "Transfer").WebRequest();

                            var ns = new System.Collections.Specialized.NameValueCollection();
                            UMC.Proxy.Utility.Sign(webr, ns, secret);
                            try
                            {
                                var meta = JSON.Deserialize<WebMeta>(webr.Get().ReadAsString());

                                var scheme = meta["scheme"] ?? "http";
                                if (meta.ContainsKey("domain") == false)
                                {
                                    sb.AppendLine($"Web VPN开启失败!!!");
                                    sb.AppendLine("失败原因：\a域名未注册，请注册域名。");
                                }
                                else if (meta.ContainsKey("msg"))
                                {
                                    sb.AppendLine($"Web VPN开启失败!!!");
                                    sb.AppendLine($"失败原因：\a{meta["msg"]}。");

                                }
                                else
                                {
                                    var ip = meta["ip"];
                                    var port = UMC.Data.Utility.IntParse(meta["port"], 0);
                                    try
                                    {
                                        Start(meta["domain"], ip, port, 4);

                                        var bridgeUrl = $"{scheme}://{meta["domain"]}";
                                        sb.AppendLine($"Web VPN ：\b{bridgeUrl}");
                                        sb.AppendLine($"服务状态：\a正连接");

                                        sb.AppendLine($"剩余流量：{meta["allowSize"]}");
                                        sb.AppendLine($"上行流量：{meta["inputSize"]}");
                                        sb.AppendLine($"下行流量：{meta["outputSize"]}");


                                        sb.AppendLine($"过期天数：{meta["expireTime"]}");
                                        sb.AppendLine();
                                        sb.AppendLine($"\a特别注意：过期后剩余流量将会清零!!!");

                                        var provider = Data.WebResource.Instance().Provider;
                                        if (String.Equals(provider.Attributes["bridge"], bridgeUrl) == false)
                                        {
                                            provider.Attributes["bridge"] = bridgeUrl;
                                        }

                                        provider.Attributes["webvpn"] = "true";
                                        var pc = Reflection.Configuration("assembly") ?? new ProviderConfiguration();
                                        pc.Add(provider);
                                        Reflection.Configuration("assembly", pc);
                                    }
                                    catch (Exception ex)
                                    {
                                        sb.AppendLine($"Web VPN开启失败!!!");
                                        sb.AppendLine($"失败原因：{ex.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {

                                sb.AppendLine($"Web VPN开启失败!!!");
                                sb.AppendLine($"失败原因：\a{ex.Message}");
                            }
                        }
                    }
                    break;
            }
            return sb.ToString();
        }
        public static bool IsRunning => _bridgeClients.Count > 0;
        public static void Stop()
        {
            lock (_bridgeClients)
            {
                for (var i = 0; i < _bridgeClients.Count; i++)
                {
                    try
                    {
                        _bridgeClients[i]?._Stop();
                    }
                    catch
                    {

                    }
                }
                _bridgeClients.Clear();
            }

            var provider = Data.WebResource.Instance().Provider;
            provider.Attributes.Remove("webvpn");
            var pc = Reflection.Configuration("assembly") ?? new ProviderConfiguration();
            pc.Add(provider);
            Reflection.Configuration("assembly", pc);
        }

        public override void Close()
        {
            base.Close();
            try
            {

                _bridgeClients.Remove(this);
                if (_stoped == false)
                {
                    Connect(host, ip, port, this.index, false);
                }
            }
            catch
            {

            }

        }
        bool _stoped;
        void _Stop()
        {
            _stoped = true;
            base.Close();
        }
        HttpMimeBody Bridge(int pid)
        {
            return new HttpBridgeRequest(new HttpBridgeMime(pid, this));

        }

        public bool Remove(int pid)
        {
            HttpMimeBody httpMime;
            return this.Clients.TryRemove(pid, out httpMime);
        }
        ConcurrentDictionary<int, HttpMimeBody> Clients = new ConcurrentDictionary<int, HttpMimeBody>();



        protected override void Read(int pid, byte[] buffer, int index, int length)
        {
            if (length > 0)
            {
                HttpMimeBody proxy = this.Clients.GetOrAdd(pid, this.Bridge);
                try
                {
                    proxy.Receive(buffer, index, length);
                }
                finally
                {
                    if (proxy.IsHttpFormatError)
                    {
                        this.Clients.TryRemove(pid, out proxy);
                        this.Write(pid, Array.Empty<byte>(), 0, 0);
                    }
                    else if (proxy.IsWebSocket == false && proxy.IsMimeFinish)
                    {
                        this.Clients.TryRemove(pid, out proxy);
                    }
                }
            }
            else
            {
                HttpMimeBody mimeBody;
                if (this.Clients.TryRemove(pid, out mimeBody))
                {
                    mimeBody.ReceiveException(new Exception("穿透传输中止"));
                }
            }

        }
    }


}
