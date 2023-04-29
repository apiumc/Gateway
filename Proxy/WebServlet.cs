using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using UMC.Data;
using UMC.Net;
using UMC.Security;
using UMC.Web;
using System.IO;
using System.Collections;
using UMC.Proxy.Entities;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace UMC.Proxy
{
    public class WebServlet : UMC.Web.WebServlet
    {
        void Unauthorized(Net.NetContext context)
        {
            if (String.IsNullOrEmpty(context.QueryString["oauth_callback"]))
            {
                var reDomain = AuthDomain(context);
                if (String.Equals(context.Url.Host, reDomain.Host) == false)
                {
                    context.Redirect(new Uri(reDomain, String.Format("/Unauthorized?oauth_callback={0}", Uri.EscapeDataString(context.Url.AbsoluteUri))).AbsoluteUri);
                }
                else
                {
                    context.Redirect(new Uri(reDomain, "/Unauthorized?oauth_callback=/").AbsoluteUri);
                }
            }
            else
            {
                Unauthorized(context, context.QueryString["oauth_callback"]);
            }
        }
        void Close(Net.NetContext context)
        {
            context.StatusCode = 403;
            context.ContentType = "text/html";
            using (System.IO.Stream stream = typeof(WebServlet).Assembly
                               .GetManifestResourceStream(String.Format("UMC.Proxy.Resources.close.html")))
            {
                context.ContentLength = stream.Length;
                stream.CopyTo(context.OutputStream);
            }
        }
        void DocConf(Net.NetContext context, string key)
        {
            context.StatusCode = 200;
            context.ContentType = "text/javascript";
            context.Output.WriteLine(@"UMC.UI.Config({'posurl': 'https://api.apiumc.com/UMC/" + context.Cookies["device"] + "' });");
            context.Output.WriteLine(@"UMC(function ($) {");
            context.Output.Write($"$.UI.Command('Subject', 'Nav', '{key}' ,");
            context.Output.WriteLine(@"function (xhr) {$.UI.On('Portfolio.List', xhr);});");
            context.Output.WriteLine(@"})");
        }
        void Desktop(Net.NetContext context, string key)
        {
            context.ContentType = "text/html";
            using (System.IO.Stream stream = typeof(WebServlet).Assembly
                               .GetManifestResourceStream($"UMC.Proxy.Resources.{key}.html"))
            {
                context.ContentLength = stream.Length;
                stream.CopyTo(context.OutputStream);
            }
        }
        void NotSupport(Net.NetContext context)
        {

            context.StatusCode = 401;
            context.ContentType = "text/html";
            using (System.IO.Stream stream = typeof(WebServlet).Assembly
                                     .GetManifestResourceStream("UMC.Proxy.Resources.Auth.nosupport.html"))
            {
                context.ContentLength = stream.Length;
                stream.CopyTo(context.OutputStream);
            }
        }

        void Auth(Net.NetContext context)
        {
            switch (context.HttpMethod)
            {
                case "POST":
                    context.ReadAsForm(ns =>
                    {
                        var sign = ns["umc-request-sign"];
                        var name = ns["umc-request-user-name"];
                        var appName = ns["umc-request-app"];
                        ns.Remove("umc-request-sign");
                        if (String.IsNullOrEmpty(sign) == false)
                        {
                            if (String.IsNullOrEmpty(appName) || String.IsNullOrEmpty(name))
                            {
                                context.StatusCode = 403;

                                UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "缺少必要的参数"), context.Output);
                                return;

                            }
                            var site = DataFactory.Instance().Site(appName);
                            if (site == null)
                            {
                                context.StatusCode = 403;

                                UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", $"未找到{appName}应用"), context.Output);
                                return;

                            }
                            if (String.IsNullOrEmpty(site.AppSecret))
                            {
                                context.StatusCode = 403;

                                UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", $"{site.Caption}未设置AppSecret"), context.Output);
                                return;

                            }
                            if (String.Equals(Utility.Sign(ns, site.AppSecret), sign, StringComparison.CurrentCultureIgnoreCase))
                            {
                                var time = UMC.Data.Utility.IntParse(ns["umc-request-time"], 0);
                                if (Math.Abs(UMC.Data.Utility.TimeSpan() - time) > 120)
                                {
                                    context.StatusCode = 403;
                                    UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "umc-request-time 参数不符合规定"), context.Output);
                                }
                                else
                                {

                                    var id = ns["umc-request-user-id"];
                                    var srole = ns["umc-request-user-role"];
                                    var alias = ns["umc-request-user-alias"];
                                    var sid = new Guid(UMC.Data.Utility.MD5("umc.api.auth", id, name, srole, alias, appName));
                                    var user = UMC.Security.Membership.Instance().Identity(name);
                                    if (user == null)
                                    {
                                        String[] roles = new string[0];
                                        if (String.IsNullOrEmpty(srole) == false)
                                        {
                                            roles = srole.Split(',').Where(r => String.Equals(r, UMC.Security.AccessToken.AdminRole) == false).ToArray();

                                        }
                                        var uid = Data.Utility.Guid(id) ?? Utility.Guid(name, true).Value;
                                        user = UMC.Security.Identity.Create(uid, name, alias ?? name, roles);
                                    }

                                    new Session<UMC.Security.AccessToken>(new UMC.Data.AccessToken(sid).Login(user, 30 * 60), UMC.Data.Utility.Guid(sid))
                                        .Commit(user.Id.Value, "umc.api.auth", false, $"{context.UserHostAddress}/{context.Server}");


                                    UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("device", UMC.Data.Utility.Guid(sid)), context.Output);
                                }

                            }
                            else
                            {
                                context.StatusCode = 403;
                                UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "签名不正确"), context.Output);

                            }
                            context.OutputFinish();

                        }
                    });
                    return;
            }
            context.Redirect("/notsupport");

        }

        internal static Uri AuthDomain(Net.NetContext context)
        {
            var Domain = Data.WebResource.Instance().WebDomain();

            if (String.Equals("localhost", Domain) == false)
            {
                return new Uri(String.Format("{0}://{1}", context.Url.Scheme, Domain));
            }
            else
            {
                return new Uri(context.Url, "/");
            }
        }
        protected override UMC.Security.AccessToken AccessToken(NetContext context)
        {

            var sessionKey = context.Cookies[SessionCookieName];
            if (String.IsNullOrEmpty(sessionKey))
            {
                sessionKey = NewCookie(context);
                var deviceId = Data.Utility.Guid(sessionKey, true).Value;

                return new UMC.Data.AccessToken(deviceId).Login(new UMC.Security.Guest(deviceId), 0);
            }
            else
            {
                var deviceId = Data.Utility.Guid(sessionKey, true).Value;
                var session = new Session<Data.AccessToken>(deviceId.ToString());
                if (session.Value != null && session.Value.Device == deviceId)
                {
                    var auth = session.Value;

                    var passDate = Data.Utility.TimeSpan();
                    if (auth.Timeout > 0)
                    {
                        if (((auth.ActiveTime ?? 0) + auth.Timeout) < passDate)
                        {
                            var at = new UMC.Data.AccessToken(deviceId).Login(new UMC.Security.Guest(deviceId), 0);
                            at.Commit(context.UserHostAddress, context.Server);
                            context.Token = at;

                            return at;

                        }
                    }
                    if (auth.ActiveTime < passDate - 600)
                    {
                        auth.Commit(context.UserHostAddress, context.Server);
                    }
                    context.Token = auth;
                    return auth;
                }

                return new UMC.Data.AccessToken(deviceId).Login(new UMC.Security.Guest(deviceId), 0);  // Security.AccessToken.Create<Data.AccessToken>(new UMC.Security.Guest(deviceId), deviceId, 0); ;



            }
        }

        void Unauthorized(Net.NetContext context, string oauth_callback)
        {

            context.Token = this.AccessToken(context);

            var reDomain = AuthDomain(context);

            var transfer = context.QueryString.Get("transfer");
            if (context.Token.IsInRole(UMC.Security.Membership.UserRole))
            {
                if (String.IsNullOrEmpty(transfer) == false)
                {
                    var sesion = UMC.Data.DataFactory.Instance().Session(context.Token.Device.ToString());

                    if (sesion != null)
                    {
                        sesion.SessionKey = transfer;
                        UMC.Data.DataFactory.Instance().Put(sesion);
                    }
                }
                context.Redirect(oauth_callback);
                return;
            }
            var webr = UMC.Data.WebResource.Instance();


            if (String.IsNullOrEmpty(context.UserAgent) == false)
            {
                var ua = context.UserAgent.ToUpper();
                if (ua.Contains("UMC CLIENT"))
                {
                    context.StatusCode = 401;
                    context.AddHeader("Cache-Control", "no-store");
                    context.ContentType = "text/html; charset=UTF-8";
                    using (System.IO.Stream stream = typeof(WebServlet).Assembly
                                             .GetManifestResourceStream("UMC.Proxy.Resources.umc.html"))
                    {
                        context.ContentLength = stream.Length;
                        stream.CopyTo(context.OutputStream);

                    }

                }
                else if (ua.Contains("MICROMESSENGER") || ua.Contains("WXWORK"))
                {
                    var account = UMC.Data.Reflection.Configuration("account");
                    var appids = new List<String>();
                    var wkappids = new List<String>();
                    for (var i = 0; i < account.Count; i++)
                    {
                        var p = account[i];

                        switch (p.Type)
                        {
                            case "weixin":
                                appids.Add(p.Name);
                                break;
                            case "wxwork":
                                wkappids.Add(p.Name);
                                break;
                        }
                    }
                    if (wkappids.Count > 0)
                    {
                        context.Token.Put("oauth_callback", oauth_callback).Commit(context.UserHostAddress, context.Server);
                        var wxP = account[wkappids[0]];
                        var agentid = wxP["agentid"];
                        var redirect_uri = Uri.EscapeDataString(new Uri(reDomain, $"/wxwork?appid={wxP.Name}").AbsoluteUri);
                        var urlStr = $"https://open.weixin.qq.com/connect/oauth2/authorize?appid={wxP.Name}&response_type=code&scope=snsapi_base&state={transfer}&redirect_uri={redirect_uri}#wechat_redirect";

                        if (String.IsNullOrEmpty(agentid) == false)
                        {
                            urlStr = $"https://open.weixin.qq.com/connect/oauth2/authorize?appid={wxP.Name}&response_type=code&agentid={agentid}&scope=snsapi_privateinfo&state={transfer}&redirect_uri={redirect_uri}#wechat_redirect";

                        }
                        context.Redirect(urlStr);
                        return;
                    }
                    else if (appids.Count > 0)
                    {
                        var redirect_uri = Uri.EscapeDataString(new Uri(reDomain, $"/weixin?appid={appids[0]}").AbsoluteUri);
                        context.Token.Put("oauth_callback", oauth_callback).Commit(context.UserHostAddress, context.Server);
                        context.Redirect($"https://open.weixin.qq.com/connect/oauth2/authorize?appid={appids[0]}&response_type=code&scope=snsapi_base&state={transfer}&redirect_uri={redirect_uri}#wechat_redirect");//, Uri.EscapeDataString(new Uri(reDomain,$"/weixin?appid={appids[0]}").AbsoluteUri), appids[0]));
                    }

                    else
                    {
                        Error(context, "微信使用提示", "缺少微信参数，请联系管理员", "");
                    }
                }
                else if (ua.Contains("DINGTALK"))
                {
                    var account = UMC.Data.Reflection.Configuration("account");
                    var appids = new List<String>();
                    for (var i = 0; i < account.Count; i++)
                    {
                        var p = account[i];
                        if (String.Equals(p.Type, "dingtalk"))
                        {

                            appids.Add(p.Name);
                        }
                    }
                    if (appids.Count == 0)
                    {
                        Error(context, "钉钉使用提示", "缺少钉钉参数，请联系管理员", "");

                    }
                    else
                    {
                        context.StatusCode = 401;
                        context.AddHeader("Cache-Control", "no-store");
                        context.ContentType = "text/html; charset=UTF-8";
                        using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                                 .GetManifestResourceStream("UMC.Proxy.Resources.dingtalk.html"))
                        {
                            var str = new System.IO.StreamReader(stream).ReadToEnd();
                            var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                            {
                                var key = g.Groups["key"].Value.ToLower();
                                switch (key)
                                {
                                    case "appids":
                                        return UMC.Data.JSON.Serialize(appids);
                                }
                                return "";

                            });
                            context.Output.Write(v);

                        }

                    }
                }
                else
                {
                    context.StatusCode = 401;
                    context.AddHeader("Cache-Control", "no-store");
                    this.LocalResources(context, "/UI/Unauthorized.html", true);
                }


            }
        }
        public static void Error(Net.NetContext context, int statusCode, String title, String msg, String log)
        {
            context.StatusCode = statusCode;
            context.ContentType = "text/html; charset=UTF-8";
            context.AddHeader("Cache-Control", "no-store");
            using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                        .GetManifestResourceStream("UMC.Proxy.Resources.error.html"))
            {
                var str = new System.IO.StreamReader(stream).ReadToEnd();
                var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                {
                    var key = g.Groups["key"].Value.ToLower();
                    switch (key)
                    {
                        case "title":
                            return title;
                        case "msg":
                            return msg;
                        case "log":
                            return log;

                    }
                    return "";

                });
                context.Output.Write(v);


            }
        }
        public static void Error(Net.NetContext context, String title, String msg, String log)
        {
            Error(context, 401, title, msg, log);
        }
        void Auth(Net.NetContext context, string wk)
        {
            context.ContentType = "text/html; charset=UTF-8";

            using (System.IO.Stream stream = typeof(WebServlet).Assembly//UMC.Proxy
                                                   .GetManifestResourceStream(String.Format("UMC.Proxy.Resources.{0}.html", wk)))
            {

                switch (wk)
                {
                    case "dingtalk":
                        {
                            var account = UMC.Data.Reflection.Configuration("account");
                            var appids = new List<String>();
                            for (var i = 0; i < account.Count; i++)
                            {
                                var p = account[i];
                                if (String.Equals(p.Type, wk))
                                {

                                    appids.Add(p.Name);
                                }
                            }
                            var str = new System.IO.StreamReader(stream).ReadToEnd();
                            var v = new Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                            {
                                var key = g.Groups["key"].Value.ToLower();
                                switch (key)
                                {
                                    case "appids":
                                        return UMC.Data.JSON.Serialize(appids);

                                }
                                return "";

                            });
                            context.Output.Write(v);
                        }
                        break;
                    default:
                        stream.CopyTo(context.OutputStream);
                        break;
                }

            }
        }
        String NewCookie(NetContext context)
        {

            var sessionKey = Utility.Guid(Guid.NewGuid());
            context.Cookies[SessionCookieName] = sessionKey;
            String Domain = context.Url.Host;
            var cdmn = Domain;

            var SameSite = "";
            if (context.UrlReferrer != null && context.UrlReferrer.Host.EndsWith(Domain) == false && String.Equals(context.Url.Scheme, "https"))
            {
                SameSite = "SameSite=None; Secure; ";

            }
            var cookieStr = "device={0}; {3}Expires={1}; HttpOnly; Domain={2}; Path=/";
            if (Regex.IsMatch(Domain, @"^(\d{1,3}.)+\d{1,3}$"))
            {
                cookieStr = "device={0}; {3}HttpOnly; Path=/";
            }
            else
            {
                var ds = cdmn.Split('.');
                if (ds.Length > 2)
                {
                    cdmn = ds[ds.Length - 2] + "." + ds[ds.Length - 1];
                }
            }
            context.AddHeader("Set-Cookie", String.Format(cookieStr, sessionKey, DateTime.Now.AddYears(10).ToString("r"), cdmn, SameSite));
            return sessionKey;


        }
        String GetCookie(NetContext context)
        {
            var cookie = context.Cookies[SessionCookieName];
            if (String.IsNullOrEmpty(cookie))
            {
                return NewCookie(context);
            }
            return cookie;
        }
        void LocalResources(NetContext context, String path, bool check)
        {
            var file = UMC.Data.Reflection.ConfigPath($"Static{path}");
            if (check)
            {
                if (System.IO.File.Exists(file))
                {
                    TransmitFile(context, file, true);
                    if (context.AllowSynchronousIO)
                    {
                        context.OutputFinish();
                    }
                    return;
                }
            }
            var url = new Uri($"https://res.apiumc.com{path}?{UMC.Data.Utility.TimeSpan()}");
            if (context.AllowSynchronousIO == false)
            {
                context.UseSynchronousIO(() => { });
            }

            url.WebRequest().Get(xhr =>
            {
                if (xhr.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var stream = UMC.Data.Utility.Writer(file, false);

                    xhr.ReadAsData((b, i, c) =>
                    {
                        if (c == 0 && b.Length == 0)
                        {
                            stream.Close();
                            TransmitFile(context, file, true);
                            context.OutputFinish();
                        }
                        else
                        {
                            stream.Write(b, i, c);
                        }
                    });
                }
                else
                {
                    Error(context, 404, "API UMC", "请求的资源不存在", "");
                    context.OutputFinish();
                }
            });

        }
        bool isLoginAPI = false;

        void Synch(NetContext context, String path)
        {
            var paths = new List<String>(path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries));

            var sessionKey = GetCookie(context);

            var deviceId = Data.Utility.Guid(sessionKey, true).ToString();

            if (paths.Count > 1)
            {
                if (String.Equals(paths[1], context.Server))
                {
                    if (paths.Count > 2 && String.Equals(paths[2], "!"))
                    {
                        context.Redirect(context.RawUrl.Substring(context.RawUrl.IndexOf("/!/")));
                    }
                    else
                    {
                        context.Redirect("/UMC.Login");
                    }
                    return;
                }
                else if (paths.Count > 3 && String.Equals(paths[2], "!"))
                {
                    deviceId = paths[3];

                }

            }
            else
            {
                context.Redirect("/UMC.Login");
                return;
            }
            var p = WebResource.Instance();
            var secret = p.Provider["appSecret"];
            var appId = p.Provider["appId"];
            var type = typeof(UMC.Data.Entities.Session).FullName;
            var nvs = new NameValueCollection();
            var time = UMC.Data.Utility.TimeSpan().ToString();
            nvs.Add("from", appId);
            nvs.Add("time", time);
            nvs.Add("type", type);

            var webD = new Web.WebMeta();
            webD.Put("from", appId);
            webD.Put("time", time);
            webD.Put("type", type);
            webD.Put("sign", UMC.Data.Utility.Sign(nvs, secret));
            webD.Put("value", new WebMeta().Put("SessionKey", deviceId));

            var sub = Net.NetSubscribe.Subscribes.FirstOrDefault(r => String.Equals(r.Key, paths[1]));
            if (sub == null)
            {
                context.Redirect("/UMC.Login");
                return;
            }

            var url = new Uri($"http://{sub.Address}:{sub.Port}");
            context.UseSynchronousIO(() =>
            {
            });
            url.WebRequest().Post(webD, xhr =>
            {
                xhr.ReadAsString(xhrStr =>
                {
                    if (xhr.StatusCode == HttpStatusCode.OK)
                    {
                        var seesion = JSON.Deserialize<UMC.Data.Entities.Session>(xhrStr);

                        UMC.Data.DataFactory.Instance().Put(seesion);
                        if (paths.Count > 2 && String.Equals(paths[2], "!"))
                        {
                            context.Redirect(context.RawUrl.Substring(context.RawUrl.IndexOf("/!/")));
                        }
                        else
                        {
                            context.Redirect("/UMC.Login");
                        }

                    }
                    else
                    {
                        context.Redirect("/UMC.Login");

                    }
                    context.OutputFinish();
                }, e =>
                {
                    context.Redirect("/UMC.Login");
                    context.OutputFinish();
                });
            });



        }
        void Transfer(SiteHost hostSite, UMC.Net.NetContext context, string rawUrl)
        {
            switch (hostSite.Scheme ?? 0)
            {
                case 0:
                    break;
                case 1:
                    if (String.Equals(context.Url.Scheme, "https") && context.Url.Port == 443)
                    {
                        context.Redirect($"http://{context.Url.Host}{context.RawUrl}");
                        return;
                    }
                    break;
                case 2:
                    if (String.Equals(context.Url.Scheme, "http") && context.Url.Port == 80)
                    {
                        context.Redirect($"https://{context.Url.Host}{context.RawUrl}");
                        return;
                    }
                    break;
            }
            var siteConfig = UMC.Proxy.DataFactory.Instance().SiteConfig(hostSite.Root);
            if (siteConfig.AllowAllPath == false)
            {
                switch (context.HttpMethod)
                {
                    case "GET":
                        var domain = Data.WebResource.Instance().WebDomain();

                        var hostModel = siteConfig.Site.HostModel ?? HostModel.None;
                        var union = Data.WebResource.Instance().Provider["union"] ?? ".";
                        var scheme = Data.WebResource.Instance().Provider["scheme"] ?? "http";

                        if (HttpProxy.CheckPath(context.RawUrl, out var _RePath, siteConfig.RedirectPath))
                        {
                            var mainKey = String.Format("SITE_JS_CONFIG_{0}{1}", hostSite.Root, HttpProxy.MD5(_RePath, "")).ToUpper();
                            var config = UMC.Data.DataFactory.Instance().Config(mainKey);
                            if (String.IsNullOrEmpty(config?.ConfValue) == false)
                            {
                                context.Token = this.AccessToken(context);

                                if (String.IsNullOrEmpty(context.Token.Username) || String.Equals(context.Token.Username, "?"))
                                {
                                    var seesionKey = UMC.Data.Utility.Guid(context.Token.Device.Value);
                                    var seesion = UMC.Data.DataFactory.Instance().Session(seesionKey);

                                    if (seesion != null)
                                    {
                                        var Value = UMC.Data.JSON.Deserialize<UMC.Data.AccessToken>(seesion.Content);
                                        var user = Value.Identity();
                                        UMC.Data.DataFactory.Instance().Delete(seesion);
                                        context.Token.Login(user, 30).Commit("Desktop", true, context.UserHostAddress, context.Server);

                                    }
                                    if (String.IsNullOrEmpty(context.Token.Username) || String.Equals(context.Token.Username, "?"))
                                    {
                                        context.Redirect(new Uri(AuthDomain(context), $"/Unauthorized?oauth_callback={Uri.EscapeDataString(context.Url.AbsoluteUri)}&transfer={seesionKey}").AbsoluteUri);
                                        return;
                                    }
                                }
                                var wr = WebTransfer(siteConfig, context, rawUrl);
                                context.UseSynchronousIO(() => { });

                                wr.Net(context, xhr =>
                                {
                                    if (xhr.StatusCode == HttpStatusCode.OK && xhr.ContentType?.StartsWith("text/html", StringComparison.CurrentCultureIgnoreCase) == true)
                                    {
                                        xhr.Header(context);
                                        xhr.ReadAsStream(content =>
                                       {
                                           using (var outStream = DataFactory.Instance().Compress(context.OutputStream, xhr.ContentEncoding))
                                           {
                                               using (var f = DataFactory.Instance().Decompress(content, xhr.ContentEncoding))
                                               {
                                                   f.CopyTo(outStream);
                                               };
                                               var writer = new UMC.Net.TextWriter(outStream.Write);
                                               writer.Write("<script id=\"Site\" root=\"");
                                               writer.Write(hostSite.Root);
                                               writer.WriteLine("\">");
                                               writer.WriteLine(config.ConfValue);
                                               writer.WriteLine("</script>");
                                               writer.Flush();
                                               writer.Dispose();
                                               outStream.Flush();
                                               content.Close();
                                               content.Dispose();
                                           }
                                           context.OutputFinish();
                                       }, context.Error);

                                    }
                                    else
                                    {

                                        xhr.Transfer(context);
                                    }
                                });
                                return;
                            }
                            else
                            {
                                context.Redirect($"{scheme}://{hostSite.Root}{union}{domain}{context.RawUrl}");
                                return;
                            }
                        }

                        switch (hostModel)
                        {
                            case HostModel.Disable:
                                context.Redirect($"{scheme}://{hostSite.Root}{union}{domain}{context.RawUrl}");
                                return;
                            case HostModel.Check:

                                if (String.IsNullOrEmpty(context.Headers.GetIgnore("accept-language")) == false
                                    && String.IsNullOrEmpty(context.Headers.GetIgnore("accept-encoding")) == false)
                                {
                                    context.Redirect($"{scheme}://{hostSite.Root}{union}{domain}{context.RawUrl}");
                                    return;
                                }
                                break;
                            case HostModel.Login:

                                if (HttpProxy.IsLoginPath(siteConfig, context.RawUrl))
                                {
                                    context.Redirect($"{scheme}://{hostSite.Root}{union}{domain}{context.RawUrl}");
                                    return;
                                }
                                break;
                            case HostModel.None:


                                break;
                        }
                        break;
                }
            }
            for (var i = 0; i < siteConfig.SubSite.Length; i++)
            {
                var p = siteConfig.SubSite[i];
                if (rawUrl.StartsWith(p.Key))
                {
                    var site2 = UMC.Proxy.DataFactory.Instance().SiteConfig(p.Value);
                    if (site2 != null)
                    {
                        siteConfig = site2;
                        if (p.IsDel)
                        {
                            rawUrl = rawUrl.Substring(p.Key.Length);
                            if (rawUrl.StartsWith('/') == false)
                            {
                                rawUrl = "/" + rawUrl;
                            }
                        }
                        break;
                    }
                }
            }
            Transfer(siteConfig, context, rawUrl);

        }
        void Cert(NetContext context, bool checkFile)
        {
            var secret = WebResource.Instance().Provider["appSecret"];
            var webr2 = new Uri(APIProxy.Uri, "Certificater").WebRequest();
            Utility.Sign(webr2, new System.Collections.Specialized.NameValueCollection(), secret);
            context.UseSynchronousIO(() => { });
            switch (context.HttpMethod)
            {
                case "GET":

                    webr2.Post(new WebMeta().Put("type", "file", "file", context.Url.AbsolutePath, "domain", context.Url.Host), r =>
                    {
                        r.Transfer(context);
                    });
                    return;
                case "PUT":
                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent((int)context.ContentLength.Value);
                    var bufferSize = 0;
                    context.ReadAsData((b, i, c) =>
                    {
                        if (c == 0 && b.Length == 0)
                        {
                            try
                            {
                                var webMeta = new WebMeta();
                                context.ContentType = "text/plain;charset=utf-8";
                                if (i == -1)
                                {
                                    context.StatusCode = 405;
                                    webMeta.Put("msg", "接收Body错误").Put("code", "error");
                                }
                                else
                                {
                                    var value = System.Text.Encoding.UTF8.GetString(buffer, 0, bufferSize);
                                    var p = WebResource.Instance();
                                    String appId = p.Provider["appId"];

                                    var hsh = JSON.Deserialize<Hashtable>(value);

                                    var time = hsh["time"] as string;
                                    var nvs = new NameValueCollection();
                                    nvs.Add("appId", appId);
                                    nvs.Add("time", time);
                                    var secret = p.Provider["appSecret"];

                                    if (String.Equals(hsh["sign"] as string, UMC.Data.Utility.Sign(nvs, secret)))
                                    {
                                        var Nonce = Utility.Guid(Guid.NewGuid());
                                        nvs.Add("nonce", Nonce);
                                        webMeta.Put("nonce", Nonce);
                                        webMeta.Put("sign", UMC.Data.Utility.Sign(nvs, secret));
                                        webMeta.Put("msg", "验证通过").Put("code", "success");

                                        if (UMC.Net.Certificater.Certificates.TryGetValue(context.Url.Host, out var _v) == false)
                                        {
                                            _v = new Certificater() { Name = context.Url.Host, Status = 0 };
                                            UMC.Net.Certificater.Certificates[_v.Name] = _v;
                                        }
                                        _v.Status = -1;
                                    }
                                    else
                                    {
                                        webMeta.Put("msg", "域名所有权签名验证不通过").Put("code", "error");
                                    }



                                }
                                UMC.Data.JSON.Serialize(webMeta, context.Output);
                                context.OutputFinish();
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);

                            }
                        }
                        else
                        {
                            Array.Copy(b, i, buffer, bufferSize, c);
                            bufferSize += c;
                        }
                    });
                    return;
                default:

                    webr2.Post(new WebMeta().Put("type", "cert", "domain", context.Url.Host), r =>
                    {
                        if (r.StatusCode == HttpStatusCode.OK)
                        {
                            Utility.Certificate(r);

                            context.Output.Write("OK");

                            context.OutputFinish();
                        }
                        else
                        {
                            r.Transfer(context);
                        }
                    });
                    break;
            }
        }
        protected override void IndexResource(NetContext context)
        {
            if (context.Url.AbsolutePath.EndsWith(".html"))
            {
                base.IndexResource(context);
            }
            else
            {
                Desktop(context, "desktop.ui");
            }
        }
        public override void ProcessRequest(NetContext context)
        {
            var Path = context.Url.AbsolutePath;
            String pfxPath = Path.Substring(1);
            var s = pfxPath.IndexOf('/');
            if (s > 0)
            {
                pfxPath = pfxPath.Substring(0, s);
            }
            var rawUrl = context.RawUrl;
            switch (pfxPath)
            {
                case "UMC.TEMP":
                    Temp(context);
                    return;
                case "UMC.For":
                    Synch(context, Path);
                    return;
                case "UMC.Synch":
                    Synchronize(context);
                    return;
                case "UMC.UI":
                    {
                        var _Domain = Data.WebResource.Instance().WebDomain();
                        var lPath = "/" + Path.Substring(5);
                        if (String.IsNullOrEmpty(_Domain) == false && _Domain.StartsWith("/") == false)
                        {
                            var host2 = context.Url.Host;
                            if (host2.EndsWith(_Domain) && host2.Length > _Domain.Length)
                            {
                                var sroot = host2.Substring(0, host2.Length - _Domain.Length - 1);

                                var psite2 = UMC.Proxy.DataFactory.Instance().SiteConfig(sroot);
                                if (psite2 != null)
                                {
                                    context.Token = new UMC.Data.AccessToken(Guid.Empty).Login(new UMC.Security.Guest(Guid.Empty), 0);
                                    var httpProxy = new HttpProxy(psite2, context, 0, true, rawUrl);

                                    if (httpProxy.Domain == null)
                                    {
                                        LocalResources(context, lPath, true);
                                    }
                                    else
                                    {
                                        var uiurl = new Uri(httpProxy.Domain, rawUrl).WebRequest();

                                        context.UseSynchronousIO(() => { });
                                        uiurl.Get(r =>
                                        {
                                            if (r.StatusCode == HttpStatusCode.OK)
                                            {
                                                r.Transfer(context);
                                            }
                                            else
                                            {
                                                LocalResources(context, lPath, true);
                                            }

                                        });
                                    }
                                }
                                else
                                {
                                    LocalResources(context, lPath, true);
                                }
                            }
                            else
                            {
                                LocalResources(context, lPath, true);

                            }

                        }
                        else
                        {
                            LocalResources(context, lPath, true);
                        }
                    }
                    return;
                case "UMC.Home":
                    Desktop(context, "desktop.umc");
                    return;
                case "UMC.TOP":
                    context.Redirect(AuthDomain(context).AbsoluteUri);
                    return;
                case "UMC.SignOut":
                    this.AccessToken(context).SignOut().Commit(context.UserHostAddress, context.Server);
                    context.Redirect(AuthDomain(context).AbsoluteUri);
                    return;
                case "UMC.Reset":
                    var urlReferrer = context.UrlReferrer;
                    if (urlReferrer != null)
                    {
                        context.Redirect(new Uri(AuthDomain(context), String.Format("/Unauthorized?oauth_callback={0}", Uri.EscapeDataString(urlReferrer.AbsoluteUri))).AbsoluteUri);

                    }
                    else
                    {
                        context.Redirect(AuthDomain(context).AbsoluteUri);
                    }
                    return;
                case "UMC.Clear":
                    context.AddHeader("Set-Cookie", "device=Clear; HttpOnly; Max-Age=1; Path=/");
                    context.ContentType = "text/plain; charset=utf-8";
                    for (var i = 0; i < context.Headers.Count; i++)
                    {
                        context.Output.WriteLine($"{context.Headers.GetKey(i)}:{context.Headers.Get(i)}");
                    }
                    return;
                case "UMC.Conf":
                    context.AddHeader("Cache-Control", "no-store");
                    context.ContentType = "text/javascript;charset=utf-8";
                    context.Output.Write("UMC.UI.Config({possrc:'/UMC.',posurl: '");
                    context.Output.Write("/UMC/");
                    context.Output.Write(GetCookie(context));
                    context.Output.Write("'");
                    {
                        var _Domain = Data.WebResource.Instance().WebDomain();
                        if (String.Equals("localhost", _Domain) == false)
                        {
                            var host2 = context.Url.Host;
                            if (host2.EndsWith(_Domain) && host2.Length > _Domain.Length)
                            {
                                context.Output.Write(",'domain':'{0}://{1}'", context.Url.Scheme, _Domain);
                                var sroot = host2.Substring(0, host2.Length - _Domain.Length - 1);

                                var psite2 = UMC.Proxy.DataFactory.Instance().SiteConfig(sroot);
                                if (psite2 != null)
                                {
                                    context.Output.Write(",'root':'{0}'", psite2.Site.Root);
                                    context.Output.Write(",'site':'{0}'", psite2.Site.SiteKey ?? 0);
                                    context.Output.Write(",'title':{0}", UMC.Data.JSON.Serialize(psite2.Caption));
                                }
                            }
                            else
                            {

                                context.Output.Write(",'domain':'{0}://{1}'", context.Url.Scheme, _Domain);

                            }
                        }
                    }
                    context.Output.Write("});");
                    return;
                case "UMC.Cert":
                    Cert(context, false);
                    return;
                case ".well-known":
                    {
                        var domain = context.Url.Host;
                        if (UMC.Net.Certificater.Certificates.TryGetValue(domain, out var _x509)
                        || UMC.Net.Certificater.Certificates.TryGetValue($"*.{domain}", out _x509)
                        || UMC.Net.Certificater.Certificates.TryGetValue($"*.{domain.Substring(domain.IndexOf('.'))}", out _x509))
                        {
                            if (_x509.Status < 0)
                            {
                                Cert(context, true);
                                return;
                            }
                        }
                    }
                    break;
                case "UMC.css":
                case "UMC.js":
                    LocalResources(context, "/" + Path.Substring(5), true);
                    return;
                case "!":
                    var sessionKey = Utility.Guid(GetCookie(context), true).Value;

                    Path = Path.Substring(3);
                    var key = Path;
                    var keyIndex = Path.IndexOf('/');
                    if (keyIndex > 0)
                    {
                        key = Path.Substring(0, keyIndex);
                    }


                    var seesion = UMC.Data.DataFactory.Instance().Session(key);
                    if (seesion != null)
                    {
                        var Value = UMC.Data.JSON.Deserialize<UMC.Data.AccessToken>(seesion.Content);
                        if (Value != null)
                        {
                            var user = Value.Identity();

                            var login = (UMC.Data.Reflection.Configuration("account") ?? new ProviderConfiguration())["login"] ?? Provider.Create("name", "name");
                            var timeout = UMC.Data.Utility.IntParse(login.Attributes["timeout"], 3600);
                            context.Token = new UMC.Data.AccessToken(sessionKey).Login(user, timeout);
                            context.Token.Commit("Desktop", context.UserHostAddress, context.Server);
                        }

                        UMC.Data.DataFactory.Instance().Delete(seesion);


                    }

                    context.Redirect(String.Format("{0}{1}", Path.Substring(key.Length), context.Url.Query));



                    return;
                case "UMC.CDN":
                    var keyIndex2 = Path.IndexOf('/', 10);
                    if (keyIndex2 > 0)
                    {
                        var psite = UMC.Proxy.DataFactory.Instance().SiteConfig(Path.Substring(9, keyIndex2 - 9));
                        if (psite != null)
                        {
                            var sUrlIndex = rawUrl.IndexOf('/', keyIndex2 + 2);
                            context.Token = new UMC.Data.AccessToken(Guid.Empty).Login(new UMC.Security.Guest(Guid.Empty), 0);
                            rawUrl = rawUrl.Substring(sUrlIndex);
                            if (rawUrl.StartsWith("/UMC.Image/"))
                            {
                                keyIndex2 = rawUrl.IndexOf('/', 12);
                                if (keyIndex2 > 0)
                                {
                                    context.QueryString["umc-image"] = rawUrl.Substring(11, keyIndex2 - 11);
                                    rawUrl = rawUrl.Substring(keyIndex2);
                                }
                            }
                            var httpProxy = new HttpProxy(psite, context, 0, true, rawUrl);
                            if (httpProxy.Domain == null)
                            {
                                Close(context);
                            }
                            else
                            {

                                httpProxy.ProcessRequest();
                            }
                        }
                        else
                        {
                            Close(context);
                        }

                    }
                    else
                    {
                        Close(context);
                    }
                    return;
                case "UMC.Login":
                    isLoginAPI = true;
                    break;
                case "UMC.Image":
                    var keyIndex3 = Path.IndexOf('/', 12);
                    if (keyIndex3 > 0)
                    {
                        context.QueryString["umc-image"] = Path.Substring(11, keyIndex3 - 11);

                        rawUrl = rawUrl.Substring(keyIndex3);
                        context.RewriteUrl(rawUrl);
                    }
                    break;
            }


            var host = context.Url.Host;

            SiteConfig siteConfig = null;

            for (var i = 0; i < host.Length; i++)
            {
                if (host[i] == '.')
                {
                    var hostSite = DataFactory.Instance().HostSite(host);
                    if (hostSite != null)
                    {
                        Transfer(hostSite, context, rawUrl);
                        return;
                    }

                    siteConfig = UMC.Proxy.DataFactory.Instance().SiteConfig(host.Substring(0, i));
                    break;
                }
                else if (host[i] == '-')
                {
                    siteConfig = UMC.Proxy.DataFactory.Instance().SiteConfig(host.Substring(0, i));
                    if (siteConfig == null)
                    {
                        var hostSite = DataFactory.Instance().HostSite(host);
                        if (hostSite != null)
                        {

                            Transfer(hostSite, context, rawUrl);
                            return;
                        }
                    }
                    break;
                }

            }
            if (IsHttps && context.Url.Port == 80)
            {
                if (UMC.Net.Certificater.Certificates.TryGetValue(host, out var x509))
                {
                    if (x509.Certificate != null)
                    {
                        context.Redirect($"https://{host}{context.RawUrl}");
                        return;
                    }
                }
                else
                {
                    var l = host.IndexOf('.');
                    if (l > 0)
                    {
                        if (UMC.Net.Certificater.Certificates.TryGetValue("*" + host.Substring(l), out x509))
                        {
                            if (x509.Certificate != null)
                            {
                                context.Redirect($"https://{host}{context.RawUrl}");
                                return;
                            }
                        }
                    }

                }
            }
            if (siteConfig != null)
            {
                Proxy(context, siteConfig, rawUrl);
                return;
            }

            switch (context.HttpMethod)
            {
                case "GET":

                    if (Path.StartsWith("/log/"))
                    {
                        context.Token = this.AccessToken(context);
                        if (context.Token.IsInRole(UMC.Security.Membership.AdminRole) == false)
                        {
                            context.Redirect("/");
                            return;
                        }
                    }

                    break;
            }

            switch (Path)
            {
                case "/notsupport":
                    NotSupport(context);
                    return;
                case "/Unauthorized":
                    Unauthorized(context);
                    return;
                case "/Auth":
                    Auth(context);
                    return;
                case "/weixin":
                case "/dingtalk":
                case "/wxwork":
                    Auth(context, Path.Substring(1));
                    return;
                case "/js/Docs.Conf.js":
                    DocConf(context, "proxy");
                    return;
                case "/":
                case "/Desktop":
                    Desktop(context, "desktop");
                    return;
                case "/favicon.ico":
                    context.StatusCode = 200;
                    context.ContentType = "image/x-icon";
                    using (System.IO.Stream stream = typeof(WebServlet).Assembly
                                       .GetManifestResourceStream("UMC.Proxy.Resources.favicon.ico"))
                    {
                        context.ContentLength = stream.Length;
                        stream.CopyTo(context.OutputStream);
                    }
                    return;

                case "/UMC.WS":
                    if (context.IsWebSocket == false)
                    {
                        switch (context.HttpMethod)
                        {
                            case "GET":
                                var sessionKey = Utility.Guid(GetCookie(context), true).Value;
                                JSON.Serialize(Data.WebResource.Instance().Push(sessionKey), context.Output);

                                break;
                            default:

                                context.UseSynchronousIO(() => { });
                                context.ReadAsStream(ms =>
                                {
                                    using (var rnew = new System.IO.StreamReader(ms))
                                    {

                                        var ids = context.QueryString.Get("device")?.Split(',');
                                        if (ids != null)
                                        {
                                            var ids2 = new List<Guid>();
                                            foreach (var i in ids)
                                            {
                                                ids2.Add(Utility.Guid(i, true).Value);
                                            }
                                            WebResource.Instance().Push(ids2.ToArray(), rnew.ReadToEnd());
                                        }
                                    }
                                    context.OutputFinish();


                                }, context.Error);
                                break;
                        }
                    }
                    return;

                default:
                    switch (pfxPath)
                    {
                        case "Docs":
                            Desktop(context, "desktop.doc");
                            return;
                        case "Setting":
                            var siteKey = Path.IndexOf('/', pfxPath.Length);
                            if (siteKey > 0)
                            {
                                if (DataFactory.Instance().Site(UMC.Data.Utility.IntParse(Path.Substring(siteKey + 1), 0)) != null)
                                {
                                    Desktop(context, "desktop.site");
                                }
                                else
                                {
                                    Desktop(context, "desktop.umc");
                                }
                            }
                            else
                            {

                                Desktop(context, "desktop.umc");
                            }
                            return;
                        case "Desktop":
                            Desktop(context, "desktop.page");
                            return;
                        default:
                            base.ProcessRequest(context);
                            break;


                    }


                    break;
            }




        }
        public static void WebHeaderConf(HttpWebRequest webR, SiteConfig siteConfig, UMC.Net.NetContext context, string account)
        {
            if (siteConfig.HeaderConf.Count > 0)
            {
                var he = siteConfig.HeaderConf.GetEnumerator();
                while (he.MoveNext())
                {
                    var v = he.Current.Value;
                    switch (v)
                    {

                        case "ACCOUNT":
                            if (String.IsNullOrEmpty(account) == false)
                            {
                                webR.Headers[he.Current.Key] = account;
                            }
                            break;
                        case "ROLES":
                            if (context.Token != null)
                            {
                                webR.Headers[he.Current.Key] = context.Token.Roles;
                            }
                            break;
                        case "TOKEN":
                            if (context.Token != null)
                            {
                                webR.Headers[he.Current.Key] = context.Token.Device.ToString();
                            }
                            break;
                        case "USERID":
                            if (context.Token != null)
                            {
                                webR.Headers[he.Current.Key] = context.Token.UserId.ToString();
                            }
                            break;
                        case "USERNAME":
                            if (context.Token != null)
                            {
                                webR.Headers[he.Current.Key] = context.Token.Username;
                            }
                            break;
                        case "HOST":
                            webR.Headers[he.Current.Key] = context.Url.Authority;
                            break;
                        case "SCHEME":
                            webR.Headers[he.Current.Key] = context.Url.Scheme;
                            break;
                        case "ADDRESS":
                            webR.Headers[he.Current.Key] = context.UserHostAddress;//.Split('/')[0];
                            break;
                        default:
                            webR.Headers[he.Current.Key] = v;
                            break;
                    }
                }
            }
        }
        HttpWebRequest WebTransfer(SiteConfig siteConfig, UMC.Net.NetContext context, string rawUrl)
        {

            var webR = new Uri(HttpProxy.WeightUri(siteConfig, context), rawUrl).WebRequest();
            WebHeaderConf(webR, siteConfig, context, String.Empty);


            var Headers = context.Headers;
            for (var i = 0; i < Headers.Count; i++)
            {
                var k = Headers.GetKey(i);
                var v = Headers.Get(i);
                switch (k.ToLower())
                {
                    case "content-type":
                        webR.ContentType = v;
                        break;
                    case "content-length":
                    case "connection":
                    case "host":
                        break;
                    case "user-agent":
                        webR.UserAgent = v;
                        break;
                    default:
                        webR.Headers.Add(k, v);
                        break;
                }
            }

            var host2 = siteConfig.Site.Host;
            if (String.IsNullOrEmpty(host2) == false)
            {
                var port = webR.RequestUri.Port;
                if (String.Equals(host2, "*"))
                {
                    host2 = context.Url.Authority;
                }
                else
                {
                    switch (port)
                    {
                        case 80:
                        case 443:
                            break;
                        default:
                            host2 = String.Format("{0}:{1}", host2, port);
                            break;
                    }
                    var Referer = webR.Headers[HttpRequestHeader.Referer];
                    if (String.IsNullOrEmpty(Referer) == false)
                    {
                        webR.Headers[HttpRequestHeader.Referer] = String.Format("{0}://{1}{2}", webR.RequestUri.Scheme, host2, Referer.Substring(Referer.IndexOf('/', 8)));

                    }
                    var Origin = webR.Headers["Origin"];
                    if (String.IsNullOrEmpty(Origin) == false)
                    {
                        webR.Headers["Origin"] = String.Format("{0}://{1}/", webR.RequestUri.Scheme, host2);
                    }
                }
                webR.Headers[System.Net.HttpRequestHeader.Host] = host2;
            }
            return webR;
        }
        protected override void TransmitFile(NetContext context, string file, bool isCache)
        {
            var lastIndex = file.LastIndexOf('.');
            if (lastIndex > -1)
            {
                var extName = file.Substring(lastIndex + 1).ToLower();

                switch (extName)
                {
                    case "ico":
                        extName = "x-icon";
                        goto case "png";
                    case "jpg":
                        extName = "jpeg";
                        goto case "png";
                    case "bmp":
                    case "gif":
                    case "webp":
                    case "jpeg":
                    case "png":
                        string cacheFile;
                        var version = String.Empty;
                        var ckey = context.QueryString.Get("umc-image");
                        bool _IsCheckLicense = false;
                        if (string.IsNullOrEmpty(ckey) && file.Contains("/BgSrc/"))
                        {
                            _IsCheckLicense = true;
                        }
                        if (context.CheckCache("UMC", version, out cacheFile))
                        {
                            context.OutputFinish();
                            return;
                        }
                        WebMeta ImageConfig;
                        var contentType = $"image/{extName}";

                        if (_IsCheckLicense)
                        {
                            ImageConfig = new WebMeta().Put("Format", "Optimal");

                            var etag = Utility.TimeSpan();
                            var sWriter = NetClient.MimeStream(cacheFile, contentType, etag);
                            using (var fileStream = System.IO.File.OpenRead(file))
                            {
                                SiteImage.Convert(fileStream, sWriter, ImageConfig, cacheFile);
                            }
                            sWriter.Flush();
                            sWriter.Position = 0;
                            context.OutputCache(sWriter);
                            context.OutputFinish();
                            return;
                        }
                        if (HttpProxy.TryImageConf(ckey, out ImageConfig))
                        {
                            var format = ImageConfig["Format"] ?? "Src";
                            if (String.Equals(format, "Src") == false)
                            {
                                contentType = "image/" + format;
                            }
                            var tempFile = System.IO.Path.GetTempFileName();
                            var etag = Utility.TimeSpan();
                            using (var sWriter = NetClient.MimeStream(tempFile, contentType, etag))
                            {
                                using (var fileStream = System.IO.File.OpenRead(file))
                                {
                                    SiteImage.Convert(fileStream, sWriter, ImageConfig, cacheFile);
                                }
                                sWriter.Flush();
                                sWriter.Close();
                            }
                            Utility.Move(tempFile, cacheFile);

                            using (var fileStream = System.IO.File.OpenRead(cacheFile))
                            {
                                context.OutputCache(fileStream);
                            }
                            context.OutputFinish();
                            return;
                        }
                        break;
                }
            }

            base.TransmitFile(context, file, isCache);
        }
        void StaticFile(SiteConfig siteConfig, string dir, Net.NetContext context, string rawUrl)
        {
            var path = (rawUrl ?? context.Url.AbsolutePath).Split("?")[0];
            var file = FilePath(dir + path);

            switch (context.HttpMethod)
            {
                case "GET":
                    var lastIndex = file.LastIndexOf('.');
                    var extName = "html";
                    if (lastIndex > -1)
                    {
                        extName = file.Substring(lastIndex + 1).ToLower();

                    }
                    if (System.IO.File.Exists(file))
                    {

                        switch (extName)
                        {
                            case "ico":
                                extName = "x-icon";
                                goto case "png";
                            case "jpg":
                                extName = "jpeg";
                                goto case "png";
                            case "bmp":
                            case "gif":
                            case "webp":
                            case "jpeg":
                            case "png":
                                string filename;
                                if (context.CheckCache(siteConfig.Root, siteConfig.Site.Version, out filename))
                                {
                                    return;
                                }
                                WebMeta ImageConfig;
                                var contentType = $"image/{extName}";

                                var ckey = context.QueryString.Get("umc-image");
                                if (String.IsNullOrEmpty(ckey))
                                {
                                    HttpProxy.CheckPath(context.Url.AbsolutePath, contentType, out ckey, siteConfig.ImagesConf);
                                }

                                if (HttpProxy.TryImageConfig(siteConfig.Site.Root, ckey, out ImageConfig))
                                {
                                    var format = ImageConfig["Format"] ?? "Src";
                                    if (String.Equals(format, "Src") == false)
                                    {
                                        contentType = "image/" + format;
                                    }
                                    var tempFile = System.IO.Path.GetTempFileName();
                                    var etag = Utility.TimeSpan();
                                    using (var sWriter = NetClient.MimeStream(tempFile, contentType, etag))
                                    {
                                        using (var fileStream = System.IO.File.OpenRead(file))
                                        {
                                            SiteImage.Convert(fileStream, sWriter, ImageConfig, filename);
                                        }
                                        sWriter.Flush();
                                        sWriter.Position = 0;
                                        context.OutputCache(sWriter);
                                        sWriter.Close();
                                    }
                                    Utility.Move(tempFile, filename);
                                    return;

                                }
                                break;
                        }
                        base.TransmitFile(context, file, true);
                    }
                    else if (dir.EndsWith('/'))
                    {
                        var dirInfo = new System.IO.DirectoryInfo(file);
                        if (dirInfo.Exists)
                        {
                            context.ContentType = "text/html";
                            using (System.IO.Stream stream = typeof(WebServlet).Assembly
                                                 .GetManifestResourceStream("UMC.Proxy.Resources.dir.html"))
                            {
                                stream.CopyTo(context.OutputStream);
                            }
                            var dirs = dirInfo.GetDirectories().OrderBy(r => r.Name);
                            foreach (var d in dirs)
                            {
                                context.Output.WriteLine($"<tr><td><a class=\"icon dir\" href=\"{d.Name}/\">{d.Name}</a></td><td></td><td>{d.LastWriteTimeUtc:G}</td></tr>");
                            }
                            var files = dirInfo.GetFiles().OrderBy(r => r.Name);
                            foreach (var d in files)
                            {
                                context.Output.WriteLine($"<tr><td><a class=\"icon file\" href=\"{d.Name}\">{d.Name}</a></td><td>{Utility.GetBitSize(d.Length)}</td><td>{d.LastWriteTimeUtc:G}</td></tr>");
                            }
                            context.Output.WriteLine("</tbody></table>");
                            context.Output.WriteLine("</body>");
                            context.Output.WriteLine("</html>");
                            context.Output.Flush();
                        }
                        else
                        {
                            NotFound(context, extName, dir);
                        }
                    }
                    else
                    {
                        if (path.IndexOf('.', path.LastIndexOf('/')) == -1)
                        {
                            var staticFile = FilePath(file + "/index.html");

                            if (System.IO.File.Exists(staticFile))
                            {
                                TransmitFile(context, staticFile, true);
                                return;

                            }
                        }
                        NotFound(context, extName, dir);
                    }
                    context.OutputFinish();
                    break;
                case "PUT":
                    var ns = new NameValueCollection();
                    var sign = String.Empty;
                    var hs = context.Headers;
                    for (var i = 0; i < hs.Count; i++)
                    {
                        var key = hs.GetKey(i).ToLower();
                        switch (key)
                        {
                            case "umc-request-sign":
                                sign = hs[i];
                                break;
                            default:
                                if (key.StartsWith("umc-"))
                                {
                                    ns.Add(key, Uri.UnescapeDataString(hs[i]));
                                }
                                break;
                        }
                    }
                    if (ns.Count > 0)
                    {
                        if (String.Equals(Data.Utility.Sign(ns, siteConfig.Site.AppSecret), sign, StringComparison.CurrentCultureIgnoreCase) == false)
                        {

                            var d = UMC.Data.Utility.Writer(file, false);

                            context.ReadAsData((b, i, c) =>
                            {
                                if (c == 0 && b.Length == 0)
                                {

                                    d.Close();
                                    if (i == 0)
                                    {
                                        UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "文件已经写入"), context.Output);
                                    }
                                    else
                                    {
                                        System.IO.File.Delete(file);

                                        UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "写入文件出错"), context.Output);
                                    }
                                    context.OutputFinish();
                                }
                                else
                                {
                                    d.Write(b, i, c);
                                }
                            });

                        }
                        else
                        {

                            context.StatusCode = 403;
                            UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "签名不正确"), context.Output);

                            context.OutputFinish();
                        }
                    }
                    else
                    {
                        context.StatusCode = 403;
                        UMC.Data.JSON.Serialize(new UMC.Web.WebMeta().Put("msg", "签名不正确"), context.Output);

                        context.OutputFinish();
                    }
                    break;
            }

        }
        void Transfer(SiteConfig siteConfig, UMC.Net.NetContext context, string RawUrl)
        {

            if (context.IsWebSocket)
            {
                context.Tag = WebTransfer(siteConfig, context, RawUrl);
                return;
            }
            var startTime = UMC.Data.Reflection.TimeSpanMilli(DateTime.Now);
            NameValueCollection resHeaders = null;
            String _attachmentFile = null;
            context.UseSynchronousIO(() =>
            {
                var duration = (int)(UMC.Data.Reflection.TimeSpanMilli(DateTime.Now) - startTime);

                HttpProxy.LogWrite(context, siteConfig, context.StatusCode, String.Format("{0} {1}", context.HttpMethod, context.Url.PathAndQuery), null, duration, resHeaders, _attachmentFile);

            });
            if (siteConfig.IsFile)
            {
                this.StaticFile(siteConfig, siteConfig.Domains[0].Substring(7), context, RawUrl);
                return;
            }
            switch (context.HttpMethod)
            {
                case "GET":
                    string cacheFile = String.Empty;
                    bool IsCache = false;
                    if (HttpProxy.CheckStaticPage(siteConfig, context.Url.AbsolutePath) == 0)
                    {
                        IsCache = true;
                        if (context.CheckCache(siteConfig.Root, siteConfig.Site.Version, out cacheFile))
                        {
                            context.OutputFinish();
                            return;
                        }
                    }

                    var wr = WebTransfer(siteConfig, context, RawUrl);
                    wr.Net(context, xhr =>
                    {

                        var contentType = (xhr.ContentType ?? String.Empty).Split(';')[0];
                        String ckey = null;
                        WebMeta ImageConfig = null;
                        if (contentType.StartsWith("image/") && contentType.Contains("svg") == false)
                        {
                            ckey = context.QueryString.Get("umc-image");
                            if (String.IsNullOrEmpty(ckey))
                            {
                                HttpProxy.CheckPath(context.Url.AbsolutePath, contentType, out ckey, siteConfig.ImagesConf);

                            }
                            if (HttpProxy.TryImageConfig(siteConfig.Site.Root, ckey, out ImageConfig))
                            {
                                var format = ImageConfig["Format"] ?? "Src";
                                if (String.Equals(format, "Src") == false)
                                {
                                    contentType = "image/" + format;
                                }
                                IsCache = true;
                            }
                        }
                        resHeaders = xhr.Headers;
                        if (xhr.StatusCode == HttpStatusCode.OK && IsCache)
                        {
                            var temp = Path.GetTempFileName();

                            if (ImageConfig != null)
                            {
                                xhr.ReadAsStream(ms =>
                                {
                                    var etag = Utility.TimeSpan();
                                    var outStream = NetClient.MimeStream(temp, contentType, etag);
                                    SiteImage.Convert(ms, outStream, ImageConfig, cacheFile);
                                    outStream.Flush();

                                    outStream.Position = 0;
                                    context.OutputCache(outStream);
                                    context.OutputFinish();
                                    outStream.Close();
                                    Utility.Move(temp, cacheFile);

                                }, err =>
                                {
                                    context.Error(xhr.Error);
                                });
                            }
                            else
                            {
                                var tempFile = File.Open(temp, FileMode.Create);

                                var tag = Utility.TimeSpan();
                                context.AddHeader("ETag", tag.ToString());

                                if (xhr.ContentLength > -1)
                                {
                                    context.ContentLength = xhr.ContentLength;
                                }
                                if (String.IsNullOrEmpty(xhr.ContentEncoding) == false)
                                {
                                    context.AddHeader("Content-Encoding", xhr.ContentEncoding);
                                }
                                context.ContentType = contentType;
                                xhr.ReadAsData((b, i, c) =>
                                {
                                    if (c == 0 && b.Length == 0)
                                    {
                                        if (i == -1)
                                        {
                                            tempFile.Close();
                                            File.Delete(temp);
                                            context.Error(xhr.Error);
                                        }
                                        else
                                        {
                                            context.OutputFinish();
                                            tempFile.Flush();
                                            tempFile.Position = 0;
                                            using (var tem = DataFactory.Instance().Decompress(tempFile, xhr.ContentEncoding))
                                            {
                                                var cacheStream = NetClient.MimeStream(cacheFile, contentType, tag);

                                                tem.CopyTo(cacheStream);
                                                tempFile.Close();
                                                cacheStream.Flush();
                                                cacheStream.Close();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        context.OutputStream.Write(b, i, c);
                                        tempFile.Write(b, i, c);
                                    }
                                });

                            }
                        }
                        else
                        {
                            _attachmentFile = xhr.AttachmentFile;
                            xhr.Transfer(context);

                        }
                    });

                    return;
            }

            var webR = WebTransfer(siteConfig, context, RawUrl);

            webR.Net(context, xhr =>
            {
                resHeaders = xhr.Headers;
                _attachmentFile = xhr.AttachmentFile;
                xhr.Transfer(context);
            });


        }
        void Synchronize(NetContext context)
        {
            context.UseSynchronousIO(() => { });
            switch (context.HttpMethod)
            {
                case "PUT":
                case "POST":
                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent((int)context.ContentLength.Value);
                    var bufferSize = 0;
                    context.ReadAsData((b, i, c) =>
                    {
                        if (c == 0 && b.Length == 0)
                        {
                            try
                            {
                                if (i == -1)
                                {
                                    context.StatusCode = 405;
                                    context.ContentType = "text/plain;charset=utf-8";
                                    context.Output.Write("接收Body错误");
                                    context.OutputFinish();
                                }
                                else
                                {
                                    var value = System.Text.Encoding.UTF8.GetString(buffer, 0, bufferSize);
                                    var p = WebResource.Instance();
                                    String appId = p.Provider["appId"];

                                    var hsh = JSON.Deserialize<Hashtable>(value);

                                    var time = hsh["time"] as string;
                                    var point = hsh["point"] as string;
                                    var nvs = new NameValueCollection();

                                    nvs.Add("from", appId);
                                    nvs.Add("time", time);
                                    nvs.Add("point", point);

                                    var type = hsh["type"] as string;
                                    var secret = p.Provider["appSecret"];
                                    if (String.IsNullOrEmpty(type) == false)
                                    {

                                        nvs.Add("type", type);

                                        if (String.Equals(hsh["sign"] as string, UMC.Data.Utility.Sign(nvs, secret)))
                                        {
                                            var v = UMC.Data.HotCache.Cache(type, hsh["value"] as Hashtable);
                                            if (v != null)
                                            {
                                                context.ContentType = "application/json;charset=utf-8";
                                                UMC.Data.JSON.Serialize(v, context.Output, "ts");
                                                return;
                                            }
                                            else
                                            {
                                                context.StatusCode = 404;
                                            }
                                        }
                                        else
                                        {
                                            context.StatusCode = 404;
                                        }
                                    }
                                    else
                                    {

                                        var webMeta = new WebMeta();
                                        if (String.Equals(hsh["sign"] as string, UMC.Data.Utility.Sign(nvs, secret)))
                                        {
                                            if (String.Equals(point, context.Server))
                                            {
                                                context.StatusCode = 405;
                                                webMeta.Put("msg", "不能设置自己为同步节点");
                                            }
                                            else
                                            {
                                                webMeta.Put("msg", "同步节点验证通过").Put("verify", true).Put("server", context.Server);
                                            }
                                        }
                                        else
                                        {
                                            context.StatusCode = 401;
                                            webMeta.Put("msg", "同步节点验证不通过");
                                        }
                                        UMC.Data.JSON.Serialize(webMeta, context.Output);
                                    }

                                    context.OutputFinish();

                                }
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);

                            }
                        }
                        else
                        {
                            Array.Copy(b, i, buffer, bufferSize, c);
                            bufferSize += c;
                        }
                    });
                    break;
                default:
                    context.StatusCode = 401;
                    context.OutputFinish();
                    break;
            }
        }
        public static bool IsHttps
        {
            get; set;
        }
        // public i

        void Proxy(UMC.Net.NetContext context, SiteConfig psite, string rawUrl)
        {
            for (var i = 0; i < psite.SubSite.Length; i++)
            {
                var p = psite.SubSite[i];
                if (rawUrl.StartsWith(p.Key))
                {
                    var site2 = UMC.Proxy.DataFactory.Instance().SiteConfig(p.Value);
                    if (site2 != null)
                    {
                        psite = site2;
                        if (p.IsDel)
                        {
                            rawUrl = rawUrl.Substring(p.Key.Length);
                            if (rawUrl.StartsWith('/') == false)
                            {
                                rawUrl = "/" + rawUrl;
                            }
                        }
                        break;
                    }
                }
            }

            if (psite.Site.Flag == -1)
            {
                Close(context);
                return;
            }
            else if (psite.AllowAllPath)
            {
                Transfer(psite, context, rawUrl);
                return;
            }


            var path = rawUrl.Split('?')[0];
            foreach (var d in psite.AllowPath)
            {
                bool isAllowPath;
                int splitIndex = d.IndexOf('*');
                switch (splitIndex)
                {
                    case -1:
                        isAllowPath = d[0] == '/' ? path.StartsWith(d) : String.Equals(path, d);
                        break;
                    case 0:
                        isAllowPath = d.Length > 1 ? path.EndsWith(d.Substring(1)) : true;
                        break;
                    default:
                        if (splitIndex == d.Length - 1)
                        {
                            isAllowPath = path.StartsWith(d.Substring(0, d.Length - 1));
                        }
                        else
                        {
                            isAllowPath = path.StartsWith(d.Substring(0, splitIndex)) && path.EndsWith(d.Substring(splitIndex + 1));

                        }
                        break;

                }
                if (isAllowPath)
                {
                    Transfer(psite, context, rawUrl);
                    return;
                }
            }
            context.Token = this.AccessToken(context);


            var IsAuth = false;
            var user = context.Token.Identity();

            switch (psite.Site.AuthType ?? Web.WebAuthType.User)
            {
                case Web.WebAuthType.Admin:
                    if (user.IsInRole(UMC.Security.Membership.AdminRole))
                    {
                        IsAuth = true;
                    }
                    else if (UMC.Data.DataFactory.Instance().Roles(user.Id.Value, psite.Site.SiteKey.Value).Contains(UMC.Security.Membership.AdminRole))
                    {
                        IsAuth = true;
                    }
                    break;
                default:
                case Web.WebAuthType.All:
                    IsAuth = true;
                    break;
                case Web.WebAuthType.UserCheck:
                    if (user.IsInRole(UMC.Security.Membership.UserRole))
                    {
                        if (AuthManager.IsAuthorization(user, 0, $"Desktop/{psite.Root}"))
                        {
                            IsAuth = true;
                        }
                    }
                    break;
                case Web.WebAuthType.User:

                    IsAuth = user.IsInRole(UMC.Security.Membership.UserRole);

                    break;
                case Web.WebAuthType.Check:

                    if (user.IsAuthenticated)
                    {
                        if (AuthManager.IsAuthorization(user, 0, $"Desktop/{psite.Root}"))
                        {
                            IsAuth = true;
                        }
                    }
                    break;
                case Web.WebAuthType.Guest:
                    IsAuth = user.IsAuthenticated;
                    break;
            }
            if (IsAuth)
            {
                if (psite.IsFile)
                {
                    this.StaticFile(psite, psite.Domains[0].Substring(7), context, rawUrl);
                    return;
                }
                var httpProxy = new HttpProxy(psite, context, HttpProxy.CheckStaticPage(psite, path), false, rawUrl);
                if (httpProxy.Domain == null)
                {
                    Close(context);
                    return;
                }
                else if (isLoginAPI)
                {
                    if (user.IsAuthenticated)
                    {
                        httpProxy.LoginRequest();

                    }
                    else
                    {
                        Unauthorized(context);
                    }
                    return;
                }
                if (psite.Site.IsAuth == true)
                {
                    switch (psite.Site.UserModel)
                    {
                        case UserModel.Share:
                            break;
                        default:
                            var authPath = httpProxy.RawUrl.Substring(1);

                            var lIndex = authPath.IndexOf('?');
                            if (lIndex > -1)
                            {
                                authPath = authPath.Substring(0, lIndex);
                            }
                            if (AuthManager.IsAuthorization(httpProxy.Account, psite.Site.SiteKey.Value, authPath) == false)
                            {
                                Error(context, "安全防护", $"此资源受保护，请联系应用管理员", "请从标准入口登录");
                                return;
                            }
                            break;
                    }

                }
                switch (httpProxy.Domain.Scheme)
                {
                    case "file":
                        break;
                    default:
                        if (context.IsWebSocket)
                        {
                            if (psite.Site.UserModel == Entities.UserModel.Bridge)
                            {
                                httpProxy.AuthBridge();
                            }

                            var getUrl = new Uri(httpProxy.Domain, httpProxy.RawUrl);

                            context.Tag = httpProxy.Reqesut(context.Transfer(getUrl, httpProxy.Cookies));

                        }

                        else
                        {
                            httpProxy.ProcessRequest();
                        }
                        break;
                }
            }
            else if (user.IsAuthenticated)
            {
                Error(context, "安全审记", $"你的权限不足或者登录过期，请 <a href=\"/UMC.Reset\">从新登录</a>", "请从标准入口登录");
            }
            else
            {
                Unauthorized(context);
            }

        }
    }

}
