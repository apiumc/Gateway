using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UMC.Data;
using UMC.Net;
using UMC.Web;
using UMC.Web.UI;

namespace UMC.Host
{

    public class HttpMimeServier
    {
        static String _Server = Dns.GetHostName();
        public static String Server => _Server;

        X509Certificate certificate;// = new X509Certificate2();
        X509Certificate ServerCertificateSelectionCallback(object sender, string hostName)
        {
            if (String.IsNullOrEmpty(hostName) == false)
            {
                if (UMC.Net.Certificater.Certificates.TryGetValue(hostName, out var x509Certificate))
                {

                    return x509Certificate.Certificate ?? certificate;
                }
                else
                {
                    var l = hostName.IndexOf('.');
                    if (l > 0)
                    {
                        if (UMC.Net.Certificater.Certificates.TryGetValue("*" + hostName.Substring(l), out var x509))
                        {

                            return x509.Certificate ?? certificate;
                        }
                    }
                }
            }

            return certificate;
        }
        SslServerAuthenticationOptions sslServerAuthentication;
        Dictionary<int, Socket> _host = new Dictionary<int, Socket>();


        List<String> _urls = new List<string>();
        public const String UnixPath = @"/tmp/umc.unix";
        string Config(ProviderConfiguration hosts)
        {
            UMC.Proxy.WebServlet.IsHttps = WebResource.Instance().Provider["scheme"] == "https";
            _urls.Clear();
            var host = new Dictionary<int, Socket>();

            var sb = new StringBuilder();
            for (var i = 0; i < hosts.Count; i++)
            {
                var p = hosts[i];
                switch (p.Type)
                {
                    case "unix":
                        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                    || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                        {

                            if (_host.ContainsKey(0) == false)
                            {
                                if (System.IO.Directory.Exists(UnixPath))
                                {
                                    System.IO.Directory.Delete(UnixPath, true);
                                }
                                if (System.IO.File.Exists(UnixPath))
                                {
                                    System.IO.File.Delete(UnixPath);
                                }
                                try
                                {
                                    var unix = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                                    unix.Bind(new UnixDomainSocketEndPoint(UnixPath));
                                    unix.Listen(512);
                                    SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
                                    eventArgs.Completed += Unix;
                                    unix.AcceptAsync(eventArgs);
                                    host[0] = unix;
                                    _urls.Add("unix://" + UnixPath);
                                }
                                catch (Exception ex)
                                {
                                    sb.Append($"Unix:{UnixPath} {ex.Message};");

                                }
                            }
                            else
                            {
                                _urls.Add("unix://" + UnixPath);
                                host[0] = _host[0];
                                _host.Remove(0);
                            }
                        }
                        break;
                    case "https":
                        {
                            int port = Utility.IntParse(p.Attributes["port"], 443);

                            try
                            {
                                if (_host.ContainsKey(port) == false)
                                {
                                    if (host.ContainsKey(port) == false)
                                    {
                                        var socket = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                                        socket.Bind(new IPEndPoint(IPAddress.Any, port));
                                        socket.Listen(512);
                                        SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
                                        eventArgs.Completed += Https;
                                        socket.AcceptAsync(eventArgs);
                                        host[port] = socket;
                                    }
                                }
                                else
                                {
                                    host[port] = _host[port];
                                    _host.Remove(port);
                                }

                                _urls.Add($"https://*:{port}");


                            }
                            catch (Exception ex)
                            {
                                sb.Append($"Port:{port} {ex.Message};");
                            }
                        }
                        break;
                    case "http":
                    default:
                        {
                            int port = Utility.IntParse(p.Attributes["port"], 80);

                            if (_host.ContainsKey(port) == false)
                            {
                                if (host.ContainsKey(port) == false)
                                {
                                    try
                                    {
                                        var socket = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                                        socket.Bind(new IPEndPoint(IPAddress.Any, port));
                                        socket.Listen(512);

                                        SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
                                        eventArgs.Completed += Http;
                                        socket.AcceptAsync(eventArgs);

                                        host[port] = socket;
                                        _urls.Add($"http://*:{port}");

                                    }
                                    catch (Exception ex)
                                    {
                                        sb.Append($"Port:{port} {ex.Message};");
                                    }
                                }
                            }
                            else
                            {

                                host[port] = _host[port];
                                _host.Remove(port);
                                _urls.Add($"http://*:{port}");
                            }


                        }
                        break;
                }
            }
            foreach (var s in this._host.Values)
            {
                s.Close();
                s.Dispose();
            }
            this._host.Clear();
            this._host = host;

            StartMsg = sb.ToString();
            return sb.ToString();

        }
        static HttpMimeServier httpMimeServier;
        bool IsStop = false;

        ManualResetEvent mre = new ManualResetEvent(false);
        public static void Start()
        {
            httpMimeServier = new HttpMimeServier();

            if (String.Equals(UMC.Data.WebResource.Instance().Provider["webvpn"], "true"))
            {
                HttpBridgeClient.ServerChange(2);
            }


            Pipe();

            while (httpMimeServier.IsStop == false)
            {

                httpMimeServier.mre.WaitOne(10000);
                try
                {
                    CheckLink();
                    UMC.Net.NetProxy.Check();
                    var now = Utility.TimeSpan();

                    if (httpMimeServier.CheckCertTime < now)
                    {
                        httpMimeServier.Cert();
                        httpMimeServier.CheckCertTime = now + 43200;
                    }
                }
                catch (Exception ex)
                {
                    UMC.Data.Utility.Error("Server", DateTime.Now, ex.ToString());
                }

            }
            HotCache.Flush();
            foreach (var s in httpMimeServier._host.Values)
            {
                s.Close();
                s.Dispose();
            }
        }
        int CheckCertTime = 0;

        public static string Load(ProviderConfiguration config)
        {
            return httpMimeServier.Config(config);
        }

        String StartMsg;
        private HttpMimeServier()
        {
            using (var rsa = System.Security.Cryptography.RSA.Create())
            {
                var req = new CertificateRequest("CN=apiumc", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                certificate = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(10));

            }
            sslServerAuthentication = new SslServerAuthenticationOptions();
            sslServerAuthentication.ClientCertificateRequired = false;
            sslServerAuthentication.ServerCertificateSelectionCallback = ServerCertificateSelectionCallback;

            HotCache.LoadFile();


            var certs = Reflection.Configuration("certs");
            foreach (var cert in certs.Providers)
            {
                try
                {
                    var x509 = X509Certificate2.CreateFromPem(cert["publicKey"], cert["privateKey"]);
                    UMC.Net.Certificater.Certificates[cert.Name] = new UMC.Net.Certificater { Name = cert.Name, Status = 1, Certificate = x509 };

                }
                catch
                {

                }
            }

            var hosts = Reflection.Configuration("host");
            if (hosts.Count == 0)
            {
                var http = UMC.Data.Provider.Create("*", "http");
                http.Attributes["port"] = "80";
                hosts.Add(http);
                var ssl = UMC.Data.Provider.Create("ssl", "https");
                ssl.Attributes["port"] = "443";
                hosts.Add(ssl);
                Reflection.Configuration("host", hosts);
            }

            StartMsg = this.Config(hosts);

            UMC.Data.Sql.Initializer.Register(new UMC.Data.Entities.Initializer(), new UMC.Proxy.Entities.Initializer());
            UMC.Net.APIProxy.Subscribes(_Server);

        }
        private void Cert()
        {
            var secret = WebResource.Instance().Provider["appSecret"];
            var ls = Certificater.Certificates.Values.ToArray();

            if (String.IsNullOrEmpty(secret) == false)
            {
                var certs = Reflection.Configuration("certs");
                foreach (var r in ls)
                {
                    if (r.Certificate != null)
                    {
                        var now = DateTime.Now;
                        var t = Convert.ToDateTime(r.Certificate.GetExpirationDateString());
                        if (t.AddDays(-3) < now)
                        {
                            var cert = certs[r.Name];
                            if (String.Equals(cert.Type, "apiumc"))
                            {
                                UMC.Proxy.Utility.Sign(new Uri(APIProxy.Uri, "Certificater").WebRequest(), new System.Collections.Specialized.NameValueCollection(), secret)
                                .Post(new WebMeta().Put("type", "apply", "domain", r.Name), webr =>
                                {
                                    if (webr.StatusCode == System.Net.HttpStatusCode.OK)
                                    {
                                        webr.ReadAsString(jsonStr =>
                                       {
                                           var hs = JSON.Deserialize<WebMeta>(jsonStr);
                                           if (string.Equals(hs["code"], "success"))
                                           {
                                               new System.Threading.Tasks.Task(async () =>
                                               {
                                                   await Task.Delay(30000);
                                                   UMC.Proxy.Utility.Sign(new Uri(APIProxy.Uri, "Certificater").WebRequest(), new System.Collections.Specialized.NameValueCollection(), secret)
                                                   .Post(new WebMeta().Put("type", "cert", "domain", r.Name), UMC.Proxy.Utility.Certificate);

                                               }).Start();
                                           }
                                           else if (string.Equals(hs["code"], "completed"))
                                           {
                                               UMC.Proxy.Utility.Sign(new Uri(APIProxy.Uri, "Certificater").WebRequest(), new System.Collections.Specialized.NameValueCollection(), secret)
                                                     .Post(new WebMeta().Put("type", "cert", "domain", r.Name), UMC.Proxy.Utility.Certificate);

                                           }
                                       });
                                    }
                                });
                            }
                        }

                    }
                };
            }
        }
        private void Unix(object sender, SocketAsyncEventArgs eventArgs)
        {
            if (eventArgs.SocketError == SocketError.Success)
            {
                var client = eventArgs.AcceptSocket;
                if (client != null)
                {
                    eventArgs.AcceptSocket = null;
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            new HttpMimeSocket("http", new NetworkStream(client, true), "unix", "127.0.0.1");
                        }
                        catch
                        {
                            client.Close();
                            client.Dispose();
                        }
                    });

                    if (!((Socket)sender).AcceptAsync(eventArgs))
                    {
                        Unix(sender, eventArgs);
                    }
                }

            }
        }
        private void Http(object sender, SocketAsyncEventArgs eventArgs)
        {
            if (eventArgs.SocketError == SocketError.Success)
            {
                var client = eventArgs.AcceptSocket;
                if (client != null)
                {
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var ip = client.RemoteEndPoint as IPEndPoint;
                            new HttpMimeSocket("http", new NetworkStream(client, true), client.LocalEndPoint.ToString(), ip.Address.ToString());
                        }
                        catch
                        {
                            client.Close();
                            client.Dispose();
                        }
                    });
                }
                eventArgs.AcceptSocket = null;

                if (!((Socket)sender).AcceptAsync(eventArgs))
                {
                    Http(sender, eventArgs);
                }

            }
        }
        private async void Https(Socket client)
        {

            SslStream sslStream = new SslStream(new NetworkStream(client, true), false);
            try
            {
                await sslStream.AuthenticateAsServerAsync(this.sslServerAuthentication);

                var ip = client.RemoteEndPoint as IPEndPoint;
                new HttpMimeSocket("https", sslStream, client.LocalEndPoint.ToString(), ip.Address.ToString());


            }
            catch
            {
                sslStream.Close();
                sslStream.Dispose();
                client.Close();
            }
        }
        private void Https(object sender, SocketAsyncEventArgs eventArgs)
        {
            if (eventArgs.SocketError == SocketError.Success)
            {
                var client = eventArgs.AcceptSocket;
                if (client != null)
                {
                    Https(client);

                }

                eventArgs.AcceptSocket = null;
                if (!((Socket)sender).AcceptAsync(eventArgs))
                {
                    Https(sender, eventArgs);
                }
            }
        }


        static async void Pipe()
        {
            var key = UMC.Data.Utility.Parse36Encode(UMC.Data.Utility.IntParse(new Guid(UMC.Data.Utility.MD5(UMC.Data.Utility.MapPath("~"))))); ;
            using (NamedPipeServerStream pipeServer =
            new NamedPipeServerStream($"APIUMC", PipeDirection.InOut))
            // new NamedPipeServerStream($"APIUMC", PipeDirection.InOut))
            {
                do
                {
                    await pipeServer.WaitForConnectionAsync();
                    var bufer = new byte[100];
                    int l = await pipeServer.ReadAsync(bufer, 0, 100);


                    var str = System.Text.Encoding.UTF8.GetString(bufer, 0, l).Split(' ');
                    switch (str[0])
                    {
                        case "clear":
                            UMC.Data.ProviderConfiguration.Cache.Clear();
                            pipeServer.Write(System.Text.Encoding.UTF8.GetBytes("配置缓存已经清空。\r\n"));
                            break;
                        case "vpn":
                            if (str.Length > 1)
                            {
                                switch (str[1])
                                {
                                    case "start":
                                        pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(HttpBridgeClient.ServerChange(2)));
                                        break;
                                    case "stop":
                                        pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(HttpBridgeClient.ServerChange(1)));
                                        break;
                                    default:
                                        pipeServer.Write(System.Text.Encoding.UTF8.GetBytes($"不支持“vpn {str[1]}”指令。\r\n"));
                                        break;
                                }
                            }
                            else
                            {
                                pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(HttpBridgeClient.ServerChange(0)));
                            }
                            break;
                        case "https":
                            {

                                var hosts = Reflection.Configuration("host");
                                if (str.Length > 1)
                                {

                                    if (Utility.IntParse(str[1], 0) > 0)
                                    {
                                        var http = UMC.Data.Provider.Create("ssl", "https");
                                        http.Attributes["port"] = str[1];

                                        hosts.Add(http);

                                        Reflection.Configuration("host", hosts);

                                    }

                                }
                                else
                                {
                                    if (hosts.ContainsKey("ssl") == false)
                                    {
                                        var http = UMC.Data.Provider.Create("ssl", "https");

                                        http.Attributes["port"] = "443";

                                        hosts.Add(http);

                                        Reflection.Configuration("host", hosts);

                                    }
                                }
                                httpMimeServier.Config(Reflection.Configuration("host"));
                            }
                            goto default;
                        case "ssl":
                            {
                                if (str.Length > 1)
                                {
                                    var host = str[1];

                                    if (System.Text.RegularExpressions.Regex.IsMatch(host, @"^([a-z0-9\*]([a-z0-9\-]{0,61}[a-z0-9])?\.)+[a-z0-9]{1,6}$") == false)
                                    {
                                        pipeServer.Write(System.Text.Encoding.UTF8.GetBytes($"域名格式不正确。\r\n"));
                                        break;
                                    }

                                    var secret = WebResource.Instance().Provider["appSecret"];
                                    if (String.IsNullOrEmpty(secret))
                                    {
                                        pipeServer.Write(System.Text.Encoding.UTF8.GetBytes("\a主体未登记，请在云桌面->应用设置->安全注册中完成登记\r\n"));
                                        break;
                                    }
                                    var webr2 = new Uri(APIProxy.Uri, "Certificater").WebRequest();
                                    UMC.Proxy.Utility.Sign(webr2, new System.Collections.Specialized.NameValueCollection(), secret);

                                    var webr = webr2.Post(new WebMeta().Put("type", "apply", "domain", host));

                                    var jsonStr = webr.ReadAsString();
                                    if (webr.StatusCode == System.Net.HttpStatusCode.OK)
                                    {
                                        var hs = JSON.Deserialize<WebMeta>(jsonStr);
                                        if (string.Equals(hs["code"], "success"))
                                        {
                                            if (UMC.Net.Certificater.Certificates.TryGetValue(hs["domain"] ?? host, out var _v) == false)
                                            {
                                                _v = new Certificater() { Name = hs["domain"] ?? host, Status = 0 };
                                                UMC.Net.Certificater.Certificates[_v.Name] = _v;
                                            }
                                            _v.Status = -1;
                                            pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(hs["msg"] ?? "\t\b正在签发证书,大约一分钟签发完成\r\n"));
                                            new System.Threading.Tasks.Task(async () =>
                                            {
                                                await Task.Delay(30000);
                                                UMC.Proxy.Utility.Sign(new Uri(APIProxy.Uri, "Certificater").WebRequest(), new System.Collections.Specialized.NameValueCollection(), secret)
                                                .Post(new WebMeta().Put("type", "cert", "domain", host), UMC.Proxy.Utility.Certificate);

                                            }).Start();
                                        }
                                        else if (string.Equals(hs["code"], "completed"))
                                        {
                                            if (Certificater.Certificates.TryGetValue(host, out var _cert) == false || _cert.Certificate == null)
                                            {
                                                webr2.Post(new WebMeta().Put("type", "cert", "domain", host), UMC.Proxy.Utility.Certificate);
                                            }
                                            pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(hs["msg"] as string ?? "正在签发证书\r\n"));

                                        }
                                        else
                                        {
                                            pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(hs["msg"] as string ?? "正在签发证书\r\n"));
                                        }

                                    }
                                    else
                                    {
                                        pipeServer.Write(System.Text.Encoding.UTF8.GetBytes($"请确保域名“{host}”解释到服务器，并开放80端口\r\n"));
                                    }

                                }
                                else
                                {

                                    var now = UMC.Data.Utility.TimeSpan();
                                    var ls = Certificater.Certificates.Values.OrderBy(r =>
                                    {
                                        if (r.Certificate != null)
                                        {
                                            r.Time = Utility.TimeSpan(Convert.ToDateTime(r.Certificate.GetExpirationDateString()));

                                        }
                                        return r.Time;
                                    });
                                    var sb = new StringBuilder();
                                    sb.AppendLine("  过期\t\t\t\t证书");
                                    foreach (var r in ls)
                                    {
                                        sb.AppendLine($"{UMC.Proxy.Utility.Expire(now, r.Time, "\a正在签发")}\t\t\t{r.Name}");
                                    }
                                    if (ls.Count() == 0)
                                    {
                                        sb.AppendLine("\t\t还未有证书");
                                    }
                                    else
                                    {
                                        sb.AppendLine();
                                        sb.AppendLine("\t更多详情,请在\b云桌面->应用设置->网关服务\f中查看");
                                    }
                                    pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
                                }

                            }
                            break;
                        case "check":
                            if (str.Length > 1)
                            {
                                if (String.Equals(key, str[1]) == false)
                                {
                                    pipeServer.Write(System.Text.Encoding.UTF8.GetBytes($"主程序目录：{UMC.Data.Utility.MapPath("~")}"));
                                }
                            }
                            break;
                        case "http":
                        case "start":
                            if (str.Length > 1)
                            {
                                if (Utility.IntParse(str[1], 0) > 0)
                                {
                                    var http = UMC.Data.Provider.Create("*", "http");
                                    http.Attributes["port"] = str[1];

                                    var hosts = Reflection.Configuration("host");
                                    hosts.Add(http);

                                    Reflection.Configuration("host", hosts);

                                }
                            }
                            httpMimeServier.Config(Reflection.Configuration("host"));
                            goto default;
                        case "stop":
                            pipeServer.Write(System.Text.Encoding.UTF8.GetBytes("正在停止Apiumc网关服务。\r\n"));
                            httpMimeServier.IsStop = true;
                            httpMimeServier.mre.Set();
                            return;
                        default:
                        case "info":
                            {

                                var secret = WebResource.Instance().Provider["appSecret"];
                                var sb = new StringBuilder();
                                if (httpMimeServier._urls.Count > 0)
                                {
                                    sb.Append("\u0002");
                                    sb.Append("监听地址：\b");
                                    sb.AppendJoin("\n　　　　　\b", httpMimeServier._urls);
                                    sb.AppendLine();

                                    if (HttpBridgeClient.IsRunning)
                                    {
                                        sb.AppendLine($"Web VPN ：\b{WebResource.Instance().Provider["bridge"]}");
                                    }
                                    if (String.IsNullOrEmpty(httpMimeServier.StartMsg) == false)
                                        sb.AppendLine($"异常信息：\a{httpMimeServier.StartMsg}");
                                }
                                else
                                {
                                    sb.AppendLine($"启动失败：\a{httpMimeServier.StartMsg}");

                                }
                                sb.AppendLine();
                                try
                                {
                                    var webr = new Uri(APIProxy.Uri, "Transfer").WebRequest();
                                    var ns = new System.Collections.Specialized.NameValueCollection();
                                    UMC.Proxy.Utility.Sign(webr, ns, secret);
                                    var xhr = webr.Get();
                                    if (xhr.StatusCode == HttpStatusCode.OK)
                                    {
                                        var meta = JSON.Deserialize<WebMeta>(xhr.ReadAsString()) ?? new WebMeta();
                                        var caption = meta["caption"];
                                        if (String.IsNullOrEmpty(caption))
                                        {
                                            sb.AppendLine($"注册主体：\a主体未登记\f，请在云桌面->应用设置->安全注册中完成登记");
                                        }
                                        else
                                        {
                                            sb.AppendLine($"注册主体：{caption}");
                                        }
                                    }
                                    else
                                    {
                                        sb.AppendLine($"注册主体：\a主体未登记\f，请在云桌面->应用设置->安全注册中完成登记");

                                    }
                                }
                                catch
                                {
                                }
                                pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
                            }
                            break;
                        case "pool":
                            pipeServer.Write(System.Text.Encoding.UTF8.GetBytes(Pool()));
                            break;
                    }

                    pipeServer.Disconnect();
                } while (true);

            }
        }

        static String Pool()
        {
            var _pool = Net.NetProxy.Pool;
            var sb = new StringBuilder();
            sb.AppendLine("Http Pools:");
            lock (_pool)
            {
                if (_pool.Count == 0)
                {
                    sb.AppendLine("\t\tEmpty");
                }
                else
                {
                    var p = _pool.GetEnumerator();

                    while (p.MoveNext())
                    {
                        sb.AppendFormat("{0}\t\t{2}\t\t{1}", p.Current.Value.Unwanted.Count, p.Current.Key, p.Current.Value.BurstError);
                        sb.AppendLine();
                    }
                }
            }
            return sb.ToString();
        }
        static void CheckLink()
        {
            var time = UMC.Data.Utility.TimeSpan();
            var ms = httpMimes.Values.ToArray();

            HttpMimeSocket link;
            foreach (var b in ms)
            {
                try
                {
                    if ((b.ActiveTime + b.TimeOut) < time)
                    {
                        httpMimes.TryRemove(b.Id, out link);
                        if (b.TimeOut > 30)
                        {
                            b.OutText(504, "Gateway Timeout");
                        }
                        else
                        {
                            b.Dispose();
                        }
                    }
                }
                catch
                {

                }
            }
        }
        internal static ConcurrentDictionary<int, HttpMimeSocket> httpMimes = new ConcurrentDictionary<int, HttpMimeSocket>();


    }



}
