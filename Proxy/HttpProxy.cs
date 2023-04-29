
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using UMC.Web;
using UMC.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Specialized;
using UMC.Data;
using System.Reflection;

namespace UMC.Proxy
{
    public class HttpProxy
    {
        public Uri Domain
        {
            get;
            private set;
        }
        public SiteConfig Site
        {
            get;
            private set;
        }
        public UMC.Proxy.Entities.Cookie SiteCookie
        {
            get;
            private set;
        }
        public System.IO.StringWriter Loger
        {

            get;
            private set;
        }
        public String Host
        {
            get;
            private set;
        }
        public bool? IsChangeUser
        {
            get;
            set;
        }
        String sourceUP;

        public string Password
        {
            get;
            private set;
        }
        private HttpProxy(HttpProxy proxy, SiteConfig siteConfig)
        {
            var user = siteConfig.Site.Account;

            this.Site = siteConfig;
            this.Loger = proxy.Loger;
            this.IsLog = proxy.IsLog;
            this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(siteConfig.Root, user));

            this.SiteCookie = new Entities.Cookie
            {
                Account = user,
                user_id = UMC.Data.Utility.Guid(user, true),
                Domain = this.Site.Root,
                IndexValue = 0
            };
            this.IsChangeUser = false;
            this.Context = proxy.Context;
            this.Domain = new Uri(siteConfig.Domains[0]);
            this.RawUrl = proxy.RawUrl;
            this.Host = proxy.Host;
            this.Cookies = new NetCookieContainer();
            this.User = proxy.User;


        }
        void SetCookie(Cookie cookie)
        {
            if (this.Site.Site.AuthType > WebAuthType.All)
            {
                if (String.IsNullOrEmpty(cookie.Path))
                {
                    this.Context.AddHeader("Set-Cookie", $"{cookie.Name}={cookie.Value}");
                }
                else
                {
                    foreach (var name in this.Site.OutputCookies)
                    {
                        if (String.Equals("*", name) || String.Equals(cookie.Name, name))
                        {
                            this.Context.AppendCookie(cookie.Name, cookie.Value, cookie.Path);
                            break;
                        }
                    }
                }
            }

        }

        const string DeviceIndex = "DeviceIndex";

        private bool IsCDN
        {
            get; set;
        }

        public static Uri WeightUri(SiteConfig site, Net.NetContext context)
        {

            if (site.WeightTotal > 0)
            {
                var value = 0;
                if (site.WeightTotal > 1)
                {
                    switch (site.Site.SLB ?? 0)
                    {
                        default:
                        case 0:
                            var r = new Random();
                            value = r.Next(0, site.WeightTotal);
                            break;
                        case 1:
                            value = Math.Abs(UMC.Data.Utility.IntParse(UMC.Data.Utility.Guid(context.UserHostAddress, true).Value)) % site.WeightTotal;
                            break;
                        case 2:
                            if (context.Token != null && context.Token.Device.HasValue)
                            {

                                value = Math.Abs(UMC.Data.Utility.IntParse(context.Token.Device.Value)) % site.WeightTotal;
                            }
                            else
                            {
                                var cookie = context.Headers.Get("Cookie");
                                if (String.IsNullOrEmpty(cookie) == false)
                                {
                                    var md5 = System.Security.Cryptography.MD5.Create();
                                    value = Math.Abs(UMC.Data.Utility.IntParse(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cookie)))) % site.WeightTotal;
                                }
                                else
                                {
                                    value = 0;
                                }
                            }
                            break;
                    }
                }

                var qty = 0;
                for (var i = 0; i < site.Weights.Length; i++)
                {
                    qty += site.Weights[i];
                    if (value < qty)
                    {
                        return new Uri(site.Domains[i]);

                    }
                }

            }
            return new Uri(site.Domains[0]);
        }
        private bool IsTest;
        UMC.Security.Identity _Account;
        public UMC.Security.Identity Account
        {
            get
            {
                if (_Account == null)
                {
                    var user = this.User;
                    if (String.IsNullOrEmpty(this.SiteCookie.Account))
                    {
                        _Account = UMC.Security.Identity.Create(user, UMC.Data.DataFactory.Instance().Roles(user.Id.Value, this.Site.Site.SiteKey.Value));

                    }
                    else if (String.Equals(this.SiteCookie.Account, user.Name))
                    {
                        var feildConfig = UMC.Data.JSON.Deserialize<Hashtable>(this.SiteCookie.Config) ?? new Hashtable();
                        if (feildConfig.ContainsKey("__ROLE"))
                        {
                            _Account = UMC.Security.Identity.Create(user, (feildConfig["__ROLE"] as string ?? "").Split(','));
                        }
                        else
                        {
                            _Account = UMC.Security.Identity.Create(user, UMC.Data.DataFactory.Instance().Roles(user.Id.Value, this.Site.Site.SiteKey.Value));

                        }

                    }
                    else
                    {
                        var feildConfig = UMC.Data.JSON.Deserialize<Hashtable>(this.SiteCookie.Config) ?? new Hashtable();
                        var userName = this.SiteCookie.Account;
                        var alias = (feildConfig["__ALIAS"] as string) ?? userName;

                        _Account = UMC.Security.Identity.Create(UMC.Data.Utility.Guid(userName, true).Value, userName, alias, (feildConfig["__ROLE"] as string ?? "").Split(','), (feildConfig["__ORGA"] as string ?? "").Split(','));

                    }
                }
                return _Account;
            }
        }
        public HttpProxy(SiteConfig site, UMC.Net.NetContext context, int staticModel, bool isCDN, string rawUrl)
        {
            this.Site = site;
            this.IsLog = site.Site.IsDebug == true;

            this.Loger = new StringWriter();

            this.Context = context;
            this.User = context.Token.Identity();

            this.RawUrl = rawUrl;
            this.StaticModel = staticModel;
            this.IsCDN = isCDN;

            this.Cookies = new NetCookieContainer(this.SetCookie);

            if (site.Domains.Length > 0)
            {
                this.Domain = WeightUri(site, context);
            }

            if (StaticModel != 0)
            {

                var deviceIndex = UMC.Data.Utility.IntParse(context.Cookies[$"{this.Site.Root}-{DeviceIndex}"], 0);


                if (this.Site.Site.AuthType > WebAuthType.All)
                {
                    if (User.IsAuthenticated)
                    {
                        if (site.Site.UserModel == Entities.UserModel.Bridge && deviceIndex == 0)
                        {
                            this.SiteCookie = new Entities.Cookie { Domain = Site.Root, user_id = User.Id.Value, IndexValue = 0 };
                        }
                        else
                        {
                            this.SiteCookie = DataFactory.Instance().Cookie(this.Site.Root, User.Id.Value, deviceIndex);
                        }
                        if (this.SiteCookie == null && deviceIndex != 0)
                        {
                            this.SiteCookie = DataFactory.Instance().Cookie(this.Site.Root, User.Id.Value, 0);

                        }
                    }
                    if (this.SiteCookie == null)
                    {
                        this.SiteCookie = new Entities.Cookie { Domain = Site.Root, user_id = User.Id.Value, IndexValue = 0 };

                    }
                    else if (this.Site.OutputCookies.Contains("*"))
                    {
                        this.InitClientCookie();
                    }
                    else if (String.IsNullOrEmpty(this.SiteCookie.Cookies) == false)
                    {
                        var cookies = UMC.Data.JSON.Deserializes<WebMeta>(this.SiteCookie.Cookies);
                        if (cookies != null)
                        {
                            this.Cookies.Add(cookies);
                        }

                        foreach (var k in this.Site.OutputCookies)
                        {
                            var cvalue = this.Context.Cookies[k];
                            if (String.IsNullOrEmpty(cvalue) == false)
                            {
                                var ck = this.Cookies.GetCookie(k);
                                if (ck == null)
                                {
                                    this.Cookies.Add(new Cookie(k, cvalue, "/"));
                                }
                                else if (String.Equals(ck.Value, cvalue) == false)
                                {
                                    this.Cookies.Add(new Cookie(k, cvalue, "/"));
                                }
                            }

                        }
                    }

                }
                else
                {
                    InitClientCookie();
                }

                if (site.Test.Length > 0 && User.IsAuthenticated)
                {
                    var authManager = UMC.Security.AuthManager.Instance();
                    for (var i = 0; i < site.Test.Length; i++)
                    {
                        var tUrl = site.Test[i];
                        if (tUrl.Users.Contains(User.Name))
                        {
                            this.Domain = new Uri(tUrl.Url);
                            this.IsTest = true;
                            break;
                        }
                        else if (tUrl.Auths.Length > 0)
                        {
                            if (authManager.Check(this.User, 0, tUrl.Auths).Contains(1))
                            {
                                this.IsTest = true;
                                this.Domain = new Uri(tUrl.Url);
                                break;

                            }
                        }


                    }
                }

                this.RawUrl = ReplaceRawUrl(this.RawUrl);


            }
            else if (this.IsCDN)
            {
                InitClientCookie();
            }
            if (String.IsNullOrEmpty(this.Site.Site.Host) == false && this.Domain != null)
            {
                var port = this.Domain.Port;
                var host = this.Site.Site.Host;
                switch (port)
                {
                    case 80:
                    case 443:

                        if (String.Equals(host, "*"))
                        {
                            this.Host = this.Context.Url.Authority;
                        }
                        else
                        {
                            this.Host = host;
                        }
                        break;
                    default:
                        if (String.Equals(host, "*"))
                        {
                            this.Host = this.Context.Url.Authority;
                        }
                        else
                        {
                            this.Host = $"{host}:{port}";
                        }
                        break;
                }

            }

            if (this.SiteCookie == null)
            {
                this.SiteCookie = new Entities.Cookie { Domain = Site.Root, user_id = context.Token.UserId.Value, IndexValue = 0 };

            }
            this.StartTime = UMC.Data.Reflection.TimeSpanMilli(DateTime.Now);
        }
        void InitClientCookie()
        {
            var ms = this.Context.Cookies;
            for (var i = 0; i < ms.Count; i++)
            {
                var name = ms.GetKey(i);
                var value = ms.Get(i);
                switch (name)
                {
                    case Web.WebServlet.SessionCookieName:
                        break;
                    default:
                        this.Cookies.Add(new System.Net.Cookie(name, value, "/", this.Domain.Host));

                        break;
                }
            }
        }
        public int StaticModel
        {
            get;
            private set;
        }

        public static int CheckStaticPage(SiteConfig config, string path)
        {
            var mv = config.StatusPage.GetEnumerator();
            while (mv.MoveNext())
            {
                var d = mv.Current.Key;
                int splitIndex = d.IndexOf('*');
                bool isOk;
                switch (splitIndex)
                {
                    case -1:
                        isOk = String.Equals(path, d, StringComparison.CurrentCultureIgnoreCase);
                        break;
                    case 0:
                        isOk = d.Length == 1 ? true : path.EndsWith(d.Substring(1), StringComparison.CurrentCultureIgnoreCase);

                        break;
                    default:
                        if (splitIndex == d.Length - 1)
                        {
                            isOk = path.StartsWith(d.Substring(0, d.Length - 1), StringComparison.CurrentCultureIgnoreCase);
                        }
                        else
                        {
                            isOk = path.StartsWith(d.Substring(0, splitIndex), StringComparison.CurrentCultureIgnoreCase) && path.EndsWith(d.Substring(splitIndex + 1), StringComparison.CurrentCultureIgnoreCase);

                        }

                        break;

                }
                if (isOk)
                {
                    return mv.Current.Value;
                }
            }

            var vk = path;

            var v1 = path.LastIndexOf('.');
            if (v1 > -1)
            {
                vk = vk.Substring(v1).ToLower();

            }
            switch (vk)
            {
                case ".gif":
                case ".ico":
                case ".svg":
                case ".bmp":
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".css":
                case ".less":
                case ".sass":
                case ".scss":
                case ".js":
                case ".webp":
                case ".jsx":
                case ".coffee":
                case ".ts":
                case ".ttf":
                case ".woff":
                case ".woff2":
                case ".wasm":

                    return 0;
                default:
                    return -1;
            }

        }
        public string RawUrl
        {
            get;
            private set;
        }
        public UMC.Net.NetContext Context
        {
            get;
            private set;
        }
        void SignOutHtml()
        {

            var sb = new StringWriter();

            sb.WriteLine("<div  class=\"umc-proxy-acounts\">");
            sb.WriteLine("<a class=\"home\" href=\"/UMC.TOP\">回到云桌面</a>");
            sb.WriteLine("<a class=\"signout\" href=\"/UMC.SignOut\">关闭登录</a>");
            sb.WriteLine("<a class=\"login\" href=\"/UMC.Login\">继续工作</a>");
            sb.WriteLine("</div>");
            sb.WriteLine($"<div style=\"color: #999; line-height: 50px; text-align: center;\">检测到您，正在退出{this.Site.Caption}!!!");
            sb.WriteLine("</div>");

            this.Context.AddHeader("Cache-Control", "no-store");
            this.Context.ContentType = "text/html; charset=UTF-8";

            using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                  .GetManifestResourceStream("UMC.Proxy.Resources.login-html.html"))
            {
                var str = new System.IO.StreamReader(stream).ReadToEnd();
                this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                {
                    var key = g.Groups["key"].Value.ToLower();
                    switch (key)
                    {

                        case "title":
                            return this.Site.Caption;
                        case "html":
                            return sb.ToString();
                    }
                    return "";

                }));

            }


        }
        void LoginCheckHtml()
        {

            var sb = new StringWriter();

            sb.WriteLine("<div style=\"margin-left: 60px;\" class=\"umc-proxy-acounts\">");
            sb.WriteLine("<a class=\"auto\" href=\"/UMC.Login/Auto\">免密自动绑定</a>");

            sb.WriteLine("<a class=\"input\" href=\"/UMC.Login/Input\">手输账户绑定</a>");

            sb.WriteLine("</div>");
            sb.WriteLine($"<div style=\"color: #999; line-height: 50px; text-align: center;\">推荐{this.Site.Caption}使用“免密自动绑定”，更简便。");
            sb.WriteLine("</div>");

            this.Context.AddHeader("Cache-Control", "no-store");
            this.Context.ContentType = "text/html; charset=UTF-8";

            using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                  .GetManifestResourceStream("UMC.Proxy.Resources.login-html.html"))
            {
                var str = new System.IO.StreamReader(stream).ReadToEnd();
                this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                {
                    var key = g.Groups["key"].Value.ToLower();
                    switch (key)
                    {

                        case "title":
                            return "请选择账户绑定方式";
                        case "html":
                            return sb.ToString();
                    }
                    return "";

                }));

            }


        }
        bool CheckAccountSelectHtml()
        {

            var scookies = DataFactory.Instance().Cookies(this.Site.Root, User.Id.Value).Where(r => String.IsNullOrEmpty(r.Account) == false).OrderBy(r => r.IndexValue).ToList();
            if (scookies.Count > 1)
            {
                var sb = new StringWriter();
                if (scookies.Count == 2)
                {
                    sb.WriteLine("<div style=\"margin-left: 60px;\" class=\"umc-proxy-acounts\">");
                }
                else
                {
                    sb.WriteLine("<div class=\"umc-proxy-acounts\">");
                }
                foreach (var sc in scookies)
                {
                    sb.WriteLine("<a href=\"/UMC.Login/{0}\">{1}</a>", sc.IndexValue ?? 0, sc.Account);

                }
                sb.WriteLine("</div>");

                this.Context.AddHeader("Cache-Control", "no-store");
                this.Context.ContentType = "text/html; charset=UTF-8";
                using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                      .GetManifestResourceStream("UMC.Proxy.Resources.login-html.html"))
                {
                    var str = new System.IO.StreamReader(stream).ReadToEnd();
                    this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                    {
                        var key = g.Groups["key"].Value.ToLower();
                        switch (key)
                        {

                            case "title":
                                return "您有多个账户，请选择";
                            case "html":
                                return sb.ToString();

                        }
                        return "";

                    }));

                }
                return true;
            }
            return false;

        }
        void LoginHtml(String error, bool isUser)
        {

            using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                     .GetManifestResourceStream("UMC.Proxy.Resources.login.html"))
            {
                this.Context.AddHeader("Cache-Control", "no-store");
                this.Context.ContentType = "text/html; charset=UTF-8";

                var str = new System.IO.StreamReader(stream).ReadToEnd();
                var v = new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                {
                    var key = g.Groups["key"].Value.ToLower();
                    switch (key)
                    {
                        case "user":
                            var Str = "";
                            if (isUser)
                            {
                                using (System.IO.Stream stream2 = typeof(HttpProxy).Assembly
                                                   .GetManifestResourceStream("UMC.Proxy.Resources.user.html"))
                                {
                                    Str = new System.IO.StreamReader(stream2).ReadToEnd().Replace("{name}", this.SiteCookie.Account ?? this.Context.Token.Username);
                                }
                                if (this.SiteCookie.IndexValue > 0)
                                {
                                    return Str;
                                }
                                var upConfig = GetConf(String.Format("SITE_MIME_{0}_UPDATE", Site.Root).ToUpper());
                                if (SiteConfig.CheckMime(upConfig))
                                {

                                    switch (this.Site.Site.UserModel ?? Entities.UserModel.Standard)
                                    {
                                        case Entities.UserModel.Check:
                                            return Str;
                                    }
                                    var updateModel = upConfig["UpdateModel"] as string ?? "Selected";
                                    switch (updateModel)
                                    {
                                        case "Select":
                                        case "Selected":
                                        case "Compel":


                                            using (System.IO.Stream stream2 = typeof(HttpProxy).Assembly
                                                             .GetManifestResourceStream($"UMC.Proxy.Resources.pwd-{updateModel}.html"))
                                            {
                                                Str += new System.IO.StreamReader(stream2).ReadToEnd();
                                            }
                                            break;
                                        default:
                                            break;
                                    }
                                }


                            }
                            return Str;
                        case "action":
                            var callback = this.Context.QueryString["callback"];
                            if (String.IsNullOrEmpty(callback) == false)
                            {
                                return $"?callback={Uri.EscapeDataString(callback)}";
                            }
                            else
                            {
                                return "";
                            }
                        case "title":
                            return isUser ? $"{this.Site.Caption}账户绑定" : $"{this.Site.Caption}登录选择";
                        case "error":
                            return error;
                        case "fields":
                            return UMC.Data.JSON.Serialize(this.FieldHtml(isUser));
                    }
                    return "";

                });
                this.Context.Output.Write(v);

            }
        }

        WebMeta[] FieldHtml(bool isUser)
        {
            var login = GetConf(String.Format("SITE_MIME_{0}_LOGIN", Site.Root).ToUpper());
            var user = this.Context.Token.Identity();

            var hash = new Hashtable();

            var matchEvaluator = Match(hash, isUser ? "{Username}" : this.SiteCookie.Account, "", "");

            var feilds = login["Feilds"] as Hashtable ?? new Hashtable();
            var list = new List<WebMeta>();
            if (feilds.Count > 0)
            {


                var fd = feilds.Keys.Cast<String>().OrderBy(r => r).GetEnumerator();
                while (fd.MoveNext())
                {
                    var fdKey = fd.Current as string;
                    var mainKey = String.Format("SITE_MIME_{0}_LOGIN_{1}", Site.Root, fdKey).ToUpper();
                    var config = GetConf(mainKey);

                    var script = (config["Script"] as string ?? "").Trim();

                    if (script.StartsWith("[") == false)
                    {
                        continue;
                    }
                    this.Isurlencoded = false;
                    var fConfig = new WebMeta().Put("name", fdKey).Put("title", feilds[fdKey]);
                    var changes = new List<String>();
                    var rawUrl = config["RawUrl"] as string;

                    if (String.IsNullOrEmpty(rawUrl) == false)
                    {
                        var getUrl = Regex.Replace(rawUrl, matchEvaluator);
                        var ms = Regex.Matches(getUrl);
                        if (ms.Count > 0)
                        {

                            for (var i = 0; i < ms.Count; i++)
                            {
                                var cKey = ms[i].Groups["key"].Value;
                                if (changes.Exists(c => cKey == c) == false)
                                    changes.Add(cKey);
                            }

                        }

                        var value = config["Content"] as string;
                        var Method = config["Method"] as string;
                        if (String.IsNullOrEmpty(value) == false && String.IsNullOrEmpty(Method) == false)
                        {
                            switch (Method)
                            {
                                case "POST":
                                case "PUT":
                                    var valResult = Regex.Replace(value, matchEvaluator);

                                    ms = Regex.Matches(valResult);
                                    if (ms.Count > 0)
                                    {
                                        for (var i = 0; i < ms.Count; i++)
                                        {
                                            var cKey = ms[i].Groups["key"].Value;
                                            if (changes.Exists(c => cKey == c) == false)
                                                changes.Add(cKey);
                                        }

                                    }
                                    break;
                            }
                        }
                    }
                    if (changes.Count > 0)
                    {
                        fConfig.Put("change", String.Join(",", changes.ToArray()));
                    }
                    else
                    {
                        fConfig.Put("data", UMC.Data.JSON.Expression(GetConfig(config, matchEvaluator)));
                    }
                    list.Add(fConfig);

                }
            }
            return list.OrderBy(r => r["name"]).ToArray();
        }
        Hashtable GetConf(String mainKey)
        {
            var login = new Hashtable();
            var pconfig = UMC.Data.DataFactory.Instance().Config(mainKey);
            if (pconfig != null)
            {
                var v = UMC.Data.JSON.Deserialize(pconfig.ConfValue) as Hashtable;
                if (v != null)
                {
                    login = v;
                }

            }
            return login;
        }
        void Update(Hashtable feildConfig, NameValueCollection form)
        {

            var newPass = UMC.Data.Utility.Guid(Guid.NewGuid());
            var sb = new System.Text.StringBuilder();

            if (this.IsLog == true)
                this.Loger.WriteLine("更新密码:");
            if (XHR(GetConf(String.Format("SITE_MIME_{0}_UPDATE", Site.Root).ToUpper()), form, feildConfig, "UPDATE", newPass))
            {

                var userM = (this.SiteCookie.Model ?? Entities.AccountModel.Standard) | Entities.AccountModel.Changed;
                this.Password = newPass;
                this.SiteCookie.Model = userM;


            }
            else if (sb.Length > 0)
            {
                if (this.IsLog == true)
                {
                    this.Loger.WriteLine(sb.ToString());
                }
            }
        }

        String GetConfig(Hashtable login, MatchEvaluator matchEvaluator, params string[] sParams)
        {


            var script = (login["Script"] as string ?? "");

            script = script.Trim();
            if ((script.StartsWith("{") && script.EndsWith("}")) || (script.StartsWith("[") && script.EndsWith("]")))
            {
                return Regex.Replace(script, matchEvaluator);
            }
            var rawUrl = login["RawUrl"] as string;
            if (String.IsNullOrEmpty(rawUrl))
            {
                errorMsg = $"未配置请求的Url";
                return "[]";

            }
            var Header = login["Header"] as string;
            if (String.IsNullOrEmpty(Header) == false)
            {
                this.Isurlencoded = false;
                Header = Regex.Replace(Header, matchEvaluator);
            }

            this.Isurlencoded = true;

            var PathAndQuery = Regex.Replace(rawUrl, matchEvaluator);

            Uri getUrl;

            var sStrDomain = login["Domain"] as string;

            if (String.IsNullOrEmpty(sStrDomain) == false)
            {
                getUrl = new Uri(new Uri(sStrDomain), PathAndQuery);

            }
            else
            {
                getUrl = new Uri(Domain, PathAndQuery);
            }

            var Method = login["Method"] as string;
            if (String.IsNullOrEmpty(Method))
            {
                errorMsg = $"未配置{rawUrl}请求的Method";
                return "[]";
            }
            var args = new List<String>(sParams);
            var config = new String[0];

            var value = login["Content"] as string;
            switch (Method)
            {
                case "POST":
                case "PUT":
                    var ContentType = login["ContentType"] as string;
                    if (String.IsNullOrEmpty(ContentType))
                    {
                        errorMsg = $"未配置{rawUrl}请求的ContentType";
                        return "[]";
                    }
                    else
                    {
                        this.Isurlencoded = ContentType.Contains("urlencoded");

                        var valResult = Regex.Replace(value, matchEvaluator);

                        var webr = this.Context.Transfer(getUrl, this.Cookies).Header(Header);

                        webr.ContentType = ContentType;
                        var res = this.Reqesut(webr).Net(Method, valResult);


                        if (this.IsLog == true)
                        {
                            this.Loger.Write(Method);
                            this.Loger.Write(":");
                            this.Loger.WriteLine(getUrl.PathAndQuery);
                            this.Loger.WriteLine(System.Text.UTF8Encoding.UTF8.GetString(webr.Headers.ToByteArray()));

                            this.Loger.WriteLine(valResult);
                            this.Loger.WriteLine();

                            this.Loger.WriteLine("{0} {1} {2}", res.ProtocolVersion, (int)res.StatusCode, res.StatusDescription);

                            this.Loger.WriteLine(Utility.NameValue(res.Headers));
                        }

                        if (res.ContentType.Contains("image/"))
                        {
                            return $"{{\"image\":\"{Convert.ToBase64String(res.ReadAsByteArray())}\"}}";
                        }
                        else
                        {
                            args.Add(res.ReadAsString());
                        }
                        if (this.IsLog == true)
                        {
                            this.Loger.WriteLine(args[args.Count - 1]);
                            this.Loger.WriteLine();
                        }

                        int statusCode = Convert.ToInt32(res.StatusCode);
                        if (statusCode >= 500)
                        {
                            LogWrite(this.Context, this.Site, statusCode, String.Format("{0} {1}", Method, getUrl.PathAndQuery), this.SiteCookie.Account, 0, res.Headers, this._AttachmentFile);

                        }
                        if (res.StatusCode != HttpStatusCode.OK)
                        {
                            return "{}";
                        }
                    }
                    break;
                default:
                case "GET":
                    if (String.IsNullOrEmpty(value) == false)
                    {
                        config = SiteConfig.Config(value);
                    }
                    var webr2 = this.Context.Transfer(getUrl, this.Cookies).Header(Header);


                    var res2 = this.Reqesut(webr2).Get();


                    if (this.IsLog == true)
                    {

                        this.Loger.Write(Method);
                        this.Loger.Write(":");
                        this.Loger.WriteLine(getUrl.PathAndQuery);
                        this.Loger.WriteLine(System.Text.UTF8Encoding.UTF8.GetString(webr2.Headers.ToByteArray()));

                        this.Loger.WriteLine();

                        this.Loger.WriteLine("{0} {1} {2}", res2.ProtocolVersion, (int)res2.StatusCode, res2.StatusDescription);


                        this.Loger.WriteLine(Utility.NameValue(res2.Headers));
                    }

                    if (res2.ContentType.Contains("image/"))
                    {
                        return $"{{\"image\":\"{Convert.ToBase64String(res2.ReadAsByteArray())}\"}}";
                    }
                    else
                    {
                        args.Add(res2.ReadAsString());
                    }
                    if (this.IsLog == true)
                    {
                        this.Loger.WriteLine(args[args.Count - 1]);
                        this.Loger.WriteLine();
                    }

                    if (res2.StatusCode != HttpStatusCode.OK)
                    {
                        return "{}";
                    }
                    int statusCode2 = Convert.ToInt32(res2.StatusCode);
                    if (statusCode2 >= 500)
                    {
                        LogWrite(this.Context, this.Site, statusCode2, String.Format("{0} {1}", Method, getUrl.PathAndQuery), this.SiteCookie.Account, 0, res2.Headers, _AttachmentFile);
                    }



                    break;
            }
            if (String.IsNullOrEmpty(script) || String.Equals(script, "none", StringComparison.CurrentCultureIgnoreCase))
            {
                return "{}";
            }

            if (script.StartsWith("function"))
            {
                var JsCode = new List<String>();
                JsCode.Add("function findValue(h, n) {var i = h.indexOf('name=\"' + n + '\"');i = h.indexOf('value=\"', i);var e = h.indexOf('\"', i + 7);return h.substr(i + 7, e - i - 7)}");

                JsCode.Add(script);
                return Regex.Replace(GetScript(JsCode, args, matchEvaluator, config), matchEvaluator);
            }

            else
            {
                var vvsValue = GetKeyValue(args[args.Count - 1], script);
                return UMC.Data.JSON.Serialize(vvsValue);
            }
        }
        Object GetKeyValue(String html, string nvConfig)
        {
            if (nvConfig.StartsWith("[") && nvConfig.Contains("]:"))
            {
                var tfds = new List<String>();

                var keyIndex = nvConfig.IndexOf(':');
                var nv = nvConfig.Substring(0, keyIndex).Trim('[', ']');
                if (String.IsNullOrEmpty(nv) == false)
                {
                    var nvs = nv.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var k in nvs)
                    {
                        var v = k.Trim();
                        if (String.IsNullOrEmpty(v) == false)
                        {
                            tfds.Add(v);
                        }
                    }
                }

                var keyN = nvConfig.Substring(keyIndex + 1).Trim();


                var sc = UMC.Data.JSON.Deserialize(html);
                Array array = null;
                if (html.StartsWith("["))
                {
                    array = sc as Array;
                }
                else if (sc is Hashtable)
                {
                    var scD = sc as Hashtable;
                    if (scD.ContainsKey(keyN))
                    {
                        array = scD[keyN] as Array;
                    }
                    else
                    {
                        int idex = keyN.IndexOf('.');
                        while (idex > 0)
                        {
                            var k = keyN.Substring(0, idex);
                            if (scD.ContainsKey(k))
                            {
                                scD = scD[k] as Hashtable;
                                keyN = keyN.Substring(idex + 1);
                                if (scD.ContainsKey(keyN))
                                {

                                    array = scD[keyN] as Array;
                                    break;
                                }
                                else
                                {
                                    idex = keyN.IndexOf('.');
                                }
                            }
                        }
                    }
                }
                if (array == null)
                {
                    return new int[0];
                }

                var vField = "";
                if (tfds.Count > 0)
                {
                    vField = tfds[tfds.Count - 1];
                    if (tfds.Count > 1)
                    {
                        tfds.RemoveAt(tfds.Count - 1);
                    }
                }
                var list = new List<WebMeta>();

                foreach (var k in array)
                {
                    if (tfds.Count == 0)
                    {
                        list.Add(new WebMeta().Put("Text", k.ToString(), "Value", k.ToString()));
                    }
                    else if (k is Hashtable)
                    {
                        var kd = k as Hashtable;
                        var tkvs = new List<String>();
                        foreach (var tk in tfds)
                        {
                            var v = kd[tk] as string;
                            if (String.IsNullOrEmpty(v) == false)
                            {
                                tkvs.Add(v);
                            }
                        }
                        list.Add(new WebMeta().Put("Text", String.Join("-", tkvs.ToArray())).Put("Value", kd[vField]));

                    }
                }
                return list;

            }
            var nas = SiteConfig.Config(nvConfig);
            var isKey = nas.Contains("KEY-VALUE");
            var vnvs = new Hashtable();

            var formValues = Utility.FromValue(html, isKey);
            var isError = false;
            int valueCount = 0;
            foreach (var name in nas)
            {
                if (String.Equals(name, "KEY-VALUE"))
                {
                    continue;
                }
                else if (String.Equals(name, "KEY-ERROR"))
                {
                    isError = true;

                    continue;

                }
                valueCount++;
                if (formValues.ContainsKey(name))
                {
                    vnvs[name] = formValues[name];
                }
                else
                {
                    int sIndex = html.IndexOf(name);
                    if (sIndex > 1 && sIndex + name.Length < html.Length)
                    {
                        sIndex = sIndex + name.Length;
                        switch (html[sIndex])
                        {
                            case ' ':
                            case '"':
                            case '\'':
                                if (html[sIndex - 1 - name.Length] == html[sIndex])
                                {

                                    while (sIndex < html.Length)
                                    {
                                        sIndex++;
                                        switch (html[sIndex])
                                        {
                                            case '\r':
                                            case '\t':
                                            case '\n':
                                            case ' ':
                                                break;
                                            case ':':
                                            case '=':
                                                var str = GetHtmlValue(html, sIndex + 1);
                                                if (String.IsNullOrEmpty(str) == false)
                                                {
                                                    vnvs[name.Trim(':', '\'', '"', '=').Trim()] = str;
                                                }
                                                sIndex = html.Length;
                                                break;
                                        }
                                    }
                                }
                                break;
                            case ':':
                            case '=':
                                var str2 = GetHtmlValue(html, sIndex + 1);
                                if (String.IsNullOrEmpty(str2) == false)
                                {
                                    vnvs[name.Trim(':', '\'', '"', '=').Trim()] = str2;
                                }
                                break;
                        }
                    }
                }

            }
            if (isError && vnvs.Count != valueCount)
            {
                return "未获取正确的参数";
            }
            return vnvs;
        }
        string GetHtmlValue(string html, int nIndex)
        {

            int start = 0, end = 0;
            char? startStr = null;

            while (nIndex < html.Length)
            {
                switch (html[nIndex])
                {
                    case '\r':
                    case '\t':
                    case '\n':
                    case ' ':

                        if (start > 0 && startStr.HasValue == false)
                        {
                            end = nIndex;
                        }
                        break;
                    case ';':
                    case ',':
                        if (startStr.HasValue == false)
                        {
                            end = nIndex;
                        }
                        break;
                    case '"':
                    case '\'':
                        if (start == 0)
                        {
                            start = nIndex + 1;
                            startStr = html[nIndex];

                        }
                        else if (html[nIndex] == startStr)
                        {
                            end = nIndex;
                        }
                        break;
                    default:
                        if (start == 0)
                        {
                            start = nIndex;
                        }
                        break;
                }
                if (end > 0)
                {
                    break;
                }
                nIndex++;
            }
            if (start > 0 && start < end)
            {
                return html.Substring(start, end - start);
            }
            return null;

        }
        String GetScript(List<String> jscode, List<String> args, MatchEvaluator matchEvaluator, params String[] config)
        {

            foreach (var s in config)
            {
                var surl = s.Trim();
                if (String.IsNullOrEmpty(surl) == false)
                {
                    this.Isurlencoded = true;
                    var url = Regex.Replace(surl, matchEvaluator);
                    if (url.EndsWith(".js"))
                    {
                        if (url.StartsWith("https://") || url.StartsWith("http://") || url.StartsWith("/"))
                        {
                            var t = MD5(url);
                            var staticFile = Data.Reflection.ConfigPath("Static/TEMP/" + t);
                            if (System.IO.File.Exists(staticFile) == false)
                            {
                                if (url.StartsWith("/"))
                                {
                                    jscode.Add(this.Reqesut(this.Context.Transfer(new Uri(this.Domain, url), this.Cookies)).Get().ReadAsString());

                                }
                                else
                                {
                                    jscode.Add(this.Context.Transfer(new Uri(this.Domain, url), this.Cookies).Send("GET", null).ReadAsString());
                                }
                                Data.Utility.Writer(staticFile, jscode[jscode.Count - 1]);
                            }
                            else
                            {
                                jscode.Add(Data.Utility.Reader(staticFile));
                            }
                        }
                    }
                    else if (url.StartsWith("/"))
                    {
                        var webr = this.Reqesut(this.Context.Transfer(new Uri(this.Domain, url), this.Cookies)).Get();

                        args.Add(webr.ReadAsString());
                    }
                }
            }
            return DataFactory.Instance().Evaluate(String.Join(";", jscode.ToArray()), args.ToArray());
        }

        bool IsLog;

        String errorMsg;

        String ResetPasswork(Hashtable loginConfig, NameValueCollection form, SiteConfig siteConfig)
        {

            if (String.IsNullOrEmpty(siteConfig.Site.Account))
            {
                if (this.IsLog == true)
                    this.Loger.WriteLine("未配置检测账户");
                return null;
            }


            var proxy = new HttpProxy(this, siteConfig);


            var checkConfig = GetConf(String.Format("SITE_MIME_{0}_CHECK", proxy.Site.Root).ToUpper());
            if (checkConfig.ContainsKey("Finish"))
            {
                proxy.Cookies = new NetCookieContainer();
                if (String.IsNullOrEmpty(proxy.Site.Home) == false)
                {
                    var r = this.Context.Transfer(new Uri(proxy.Domain, proxy.Site.Home), proxy.Cookies).Get();
                    r.ReadAsString();

                }
                var config = new Hashtable();

                if (proxy.IsLog == true)
                {
                    this.Loger.WriteLine("管理员登录:");
                }
                if (checkConfig.ContainsKey("IsNotLoginApi") || proxy.XHR(loginConfig, form, config, "LOGIN", ""))
                {

                    var newPass = UMC.Data.Utility.Guid(Guid.NewGuid());

                    config["Account"] = this.Context.Token.Username;
                    if (proxy.IsLog == true)
                    {
                        proxy.Loger.WriteLine("检测账户密码:");
                    }
                    if (proxy.XHR(checkConfig, form, config, "CHECK", newPass))
                    {
                        if (config.ContainsKey("ResetPasswork"))
                        {
                            return config["ResetPasswork"] as string;
                        }
                        return newPass;
                    }
                    else
                    {
                        if (this.IsLog == true)
                        {
                            this.Loger.WriteLine(this.errorMsg);
                        }
                    }
                }
                else
                {
                    errorMsg = "检测账户登录失败导致不能重置密码,请联系应用管理员";

                    if (this.IsLog == true)
                    {
                        this.Loger.WriteLine("管理员账户密码不正确");
                    }
                }
            }
            else
            {
                if (this.IsLog == true)
                {
                    this.Loger.WriteLine("未配置完善账户检测接口");
                }

            }
            return String.Empty;
        }

        static Regex Regex = new Regex("\\{(?<key>[\\w\\.\\$,\\[\\]_-]+)\\}");
        bool Isurlencoded = true;
        bool XHR(Hashtable login, NameValueCollection form, Hashtable FeildConfig, String fieldKey, String newPass)
        {
            NetHttpResponse response = null;
            try
            {
                return XHR(login, form, FeildConfig, fieldKey, newPass, out response);
            }
            finally
            {
                if (response != null)
                {
                    response.ReadAsString();
                }
            }
        }

        String GetValue(String key, IDictionary FeildConfig, String newPWd)
        {
            switch (key.ToLower())
            {
                case "user":
                    return this.SiteCookie.Account;
                case "pwd":
                    return this.Password;
                case "new":
                    return newPWd;

            }
            return FeildConfig[key] as string;
        }
        MatchEvaluator Match(Hashtable FeildConfig, String newPass)
        {
            return Match(FeildConfig, this.SiteCookie.Account, this.Password, newPass);
        }
        MatchEvaluator Match(Hashtable FeildConfig, String user, String pd, String newPass)
        {
            Func<String, String> func = (key) =>
             {

                 switch (key.ToLower())
                 {

                     case "user":
                     case "username":
                         return Isurlencoded ? Uri.EscapeDataString(user) : user;
                     case "pwd":
                     case "password":
                         return Isurlencoded ? Uri.EscapeDataString(pd ?? "") : (pd ?? "");
                     case "md5pwd":
                         return UMC.Data.Utility.MD5(pd);
                     case "md5new":
                         return UMC.Data.Utility.MD5(newPass);
                     case "newpwd":
                     case "new":
                         return Isurlencoded ? Uri.EscapeDataString(newPass) : newPass;
                     case "time":
                         return UMC.Data.Utility.TimeSpan().ToString();
                     case "mtime":
                         return UMC.Data.Reflection.TimeSpanMilli(DateTime.Now).ToString();
                     default:
                         var nIndex = key.IndexOf('.');
                         if (nIndex > 0)
                         {
                             string fvalue = "";
                             switch (key.Substring(0, nIndex))
                             {
                                 case "hex":
                                     key = key.Substring(4);
                                     fvalue = GetValue(key, FeildConfig, newPass);
                                     if (String.IsNullOrEmpty(fvalue) == false)
                                     {
                                         return UMC.Data.Utility.Hex(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                     }
                                     break;
                                 case "b64":
                                     key = key.Substring(4);
                                     fvalue = GetValue(key, FeildConfig, newPass);
                                     if (String.IsNullOrEmpty(fvalue) == false)
                                     {
                                         if (this.Isurlencoded)
                                         {
                                             return Uri.EscapeDataString(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fvalue)));
                                         }
                                         else
                                         {
                                             return (Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fvalue)));
                                         }
                                     }
                                     break;
                                 case "md5":
                                     key = key.Substring(4);
                                     {
                                         var isb64 = false;
                                         if (key.EndsWith(".b64"))
                                         {
                                             isb64 = true;
                                             key = key.Substring(0, key.Length - 4);
                                         }

                                         fvalue = GetValue(key, FeildConfig, newPass);
                                         if (String.IsNullOrEmpty(fvalue) == false)
                                         {
                                             byte[] mdata;
                                             using (var md5 = System.Security.Cryptography.MD5.Create())
                                             {
                                                 mdata = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                             }
                                             if (isb64)
                                             {
                                                 if (this.Isurlencoded)
                                                 {
                                                     return Uri.EscapeDataString(Convert.ToBase64String(mdata));
                                                 }
                                                 else
                                                 {
                                                     return Convert.ToBase64String(mdata);
                                                 }
                                             }
                                             else
                                             {
                                                 return UMC.Data.Utility.Hex(mdata);

                                             }
                                         }
                                     }
                                     break;

                                 case "s256":
                                     key = key.Substring(5);
                                     {
                                         var isb64 = false;
                                         if (key.EndsWith(".b64"))
                                         {
                                             isb64 = true;
                                             key = key.Substring(0, key.Length - 4);
                                         }
                                         fvalue = GetValue(key, FeildConfig, newPass);
                                         if (String.IsNullOrEmpty(fvalue) == false)
                                         {

                                             var mdata = SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                             if (isb64)
                                             {
                                                 if (this.Isurlencoded)
                                                 {
                                                     return Uri.EscapeDataString(Convert.ToBase64String(mdata));
                                                 }
                                                 else
                                                 {
                                                     return Convert.ToBase64String(mdata);
                                                 }
                                             }
                                             else
                                             {
                                                 return UMC.Data.Utility.Hex(mdata);

                                             }
                                         }
                                     }
                                     break;
                                 case "sha1":
                                     key = key.Substring(5);
                                     {
                                         var isb64 = false;
                                         if (key.EndsWith(".b64"))
                                         {
                                             isb64 = true;
                                             key = key.Substring(0, key.Length - 4);
                                         }
                                         fvalue = GetValue(key, FeildConfig, newPass);
                                         if (String.IsNullOrEmpty(fvalue) == false)
                                         {
                                             var mdata = SHA1.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(fvalue));
                                             if (isb64)
                                             {
                                                 if (this.Isurlencoded)
                                                 {
                                                     return Uri.EscapeDataString(Convert.ToBase64String(mdata));
                                                 }
                                                 else
                                                 {
                                                     return Convert.ToBase64String(mdata);
                                                 }
                                             }
                                             else
                                             {
                                                 return UMC.Data.Utility.Hex(mdata);
                                             }

                                         }
                                     }
                                     break;
                                 case "hmac":
                                     key = key.Substring(5);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {

                                         var bpwd = FeildConfig[(key.Substring(0, nIndex))] as string;
                                         if (String.IsNullOrEmpty(bpwd) == false)
                                         {
                                             key = key.Substring(nIndex + 1);

                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }


                                             fvalue = GetValue(key, FeildConfig, newPass);
                                             if (String.IsNullOrEmpty(fvalue) == false)
                                             {
                                                 var mdata = new HMACSHA1(Encoding.UTF8.GetBytes(bpwd)).ComputeHash(Encoding.UTF8.GetBytes(fvalue));

                                                 if (isb64)
                                                 {
                                                     if (this.Isurlencoded)
                                                     {
                                                         return Uri.EscapeDataString(Convert.ToBase64String(mdata));
                                                     }
                                                     else
                                                     {
                                                         return Convert.ToBase64String(mdata);
                                                     }
                                                 }
                                                 else
                                                 {
                                                     return UMC.Data.Utility.Hex(mdata);
                                                 }
                                             }
                                         }
                                     }
                                     break;
                                 case "aes":
                                     key = key.Substring(4);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {
                                         var nkey = FeildConfig[key.Substring(0, nIndex)] as string;
                                         if (String.IsNullOrEmpty(nkey) == false)
                                         {
                                             key = key.Substring(nIndex + 1);
                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }
                                             var keys = key.Split('.');
                                             switch (keys.Length)
                                             {
                                                 case 1:
                                                     fvalue = GetValue(keys[0], FeildConfig, newPass);
                                                     if (String.IsNullOrEmpty(fvalue) == false)
                                                     {
                                                         if (isb64)
                                                         {
                                                             if (this.Isurlencoded)
                                                             {
                                                                 return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey, 1)));
                                                             }
                                                             else
                                                             {
                                                                 return Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey, 1));
                                                             }
                                                         }
                                                         else
                                                         {
                                                             return UMC.Data.Utility.Hex(UMC.Data.Utility.AES(fvalue, nkey, 1));
                                                         }
                                                     }
                                                     break;
                                                 case 2:
                                                     var iv = FeildConfig[keys[0]] as string;
                                                     fvalue = GetValue(keys[1], FeildConfig, newPass);
                                                     if (String.IsNullOrEmpty(fvalue) == false)
                                                     {
                                                         if (String.IsNullOrEmpty(iv))
                                                         {
                                                             var iterations = UMC.Data.Utility.IntParse(keys[0], -1);
                                                             if (iterations > 0)
                                                             {
                                                                 if (isb64)
                                                                 {
                                                                     if (this.Isurlencoded)
                                                                     {
                                                                         return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey, iterations)));
                                                                     }
                                                                     else
                                                                     {
                                                                         return Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey, iterations));
                                                                     }
                                                                 }
                                                                 else
                                                                 {
                                                                     return UMC.Data.Utility.Hex(UMC.Data.Utility.AES(fvalue, nkey, iterations));
                                                                 }
                                                             }
                                                         }
                                                         else
                                                         {
                                                             if (isb64)
                                                             {
                                                                 if (this.Isurlencoded)
                                                                 {
                                                                     return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey, iv)));
                                                                 }
                                                                 else
                                                                 {
                                                                     return Convert.ToBase64String(UMC.Data.Utility.AES(fvalue, nkey, iv));
                                                                 }
                                                             }
                                                             else
                                                             {
                                                                 return UMC.Data.Utility.Hex(UMC.Data.Utility.AES(fvalue, nkey, iv));
                                                             }
                                                         }
                                                     }
                                                     break;
                                             }
                                         }
                                     }

                                     break;
                                 case "des":
                                     key = key.Substring(4);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {
                                         var nkey = FeildConfig[key.Substring(0, nIndex)] as string;
                                         if (String.IsNullOrEmpty(nkey) == false)
                                         {
                                             key = key.Substring(nIndex + 1);
                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }
                                             var keys = key.Split('.');
                                             switch (keys.Length)
                                             {
                                                 case 1:
                                                     fvalue = GetValue(keys[0], FeildConfig, newPass);
                                                     if (String.IsNullOrEmpty(fvalue) == false)
                                                     {
                                                         if (isb64)
                                                         {
                                                             if (this.Isurlencoded)
                                                             {
                                                                 return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey, 1)));
                                                             }
                                                             else
                                                             {
                                                                 return Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey, 1));
                                                             }
                                                         }
                                                         else
                                                         {
                                                             return UMC.Data.Utility.Hex(UMC.Data.Utility.DES(fvalue, nkey, 1));
                                                         }
                                                     }
                                                     break;
                                                 case 2:
                                                     var iv = FeildConfig[keys[0]] as string;
                                                     fvalue = GetValue(keys[1], FeildConfig, newPass);
                                                     if (String.IsNullOrEmpty(fvalue) == false)
                                                     {
                                                         if (String.IsNullOrEmpty(iv))
                                                         {
                                                             var iterations = UMC.Data.Utility.IntParse(keys[0], -1);
                                                             if (iterations > 0)
                                                             {
                                                                 if (isb64)
                                                                 {
                                                                     if (this.Isurlencoded)
                                                                     {
                                                                         return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey, iterations)));
                                                                     }
                                                                     else
                                                                     {
                                                                         return Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey, iterations));
                                                                     }
                                                                 }
                                                                 else
                                                                 {
                                                                     return UMC.Data.Utility.Hex(UMC.Data.Utility.DES(fvalue, nkey, iterations));
                                                                 }
                                                             }
                                                         }
                                                         else
                                                         {
                                                             if (isb64)
                                                             {
                                                                 if (this.Isurlencoded)
                                                                 {
                                                                     return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey, iv)));
                                                                 }
                                                                 else
                                                                 {
                                                                     return Convert.ToBase64String(UMC.Data.Utility.DES(fvalue, nkey, iv));
                                                                 }
                                                             }
                                                             else
                                                             {
                                                                 return UMC.Data.Utility.Hex(UMC.Data.Utility.DES(fvalue, nkey, iv));
                                                             }
                                                         }
                                                     }
                                                     break;
                                             }
                                         }
                                     }

                                     break;
                                 case "pem":
                                     key = key.Substring(4);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {
                                         var pem = FeildConfig[key.Substring(0, nIndex)] as string;
                                         if (String.IsNullOrEmpty(pem) == false)
                                         {
                                             key = key.Substring(nIndex + 1);

                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }
                                             fvalue = GetValue(key, FeildConfig, newPass);
                                             if (String.IsNullOrEmpty(fvalue) == false)
                                             {
                                                 if (isb64)
                                                 {
                                                     if (this.Isurlencoded)
                                                     {
                                                         return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.RSA(pem, fvalue)));
                                                     }
                                                     else
                                                     {
                                                         return Convert.ToBase64String(UMC.Data.Utility.RSA(pem, fvalue));
                                                     }
                                                 }
                                                 else
                                                 {
                                                     return UMC.Data.Utility.Hex(UMC.Data.Utility.RSA(pem, fvalue));
                                                 }
                                             }

                                         }
                                     }
                                     break;
                                 case "rsa":
                                     key = key.Substring(4);
                                     nIndex = key.IndexOf('.');
                                     if (nIndex > 0)
                                     {
                                         var n = FeildConfig[key.Substring(0, nIndex)] as string;
                                         if (String.IsNullOrEmpty(n) == false)
                                         {
                                             var isb64 = false;
                                             if (key.EndsWith(".b64"))
                                             {
                                                 isb64 = true;
                                                 key = key.Substring(0, key.Length - 4);
                                             }
                                             key = key.Substring(nIndex + 1);
                                             nIndex = key.IndexOf('.');

                                             if (nIndex > 0)
                                             {
                                                 var e = FeildConfig[key.Substring(0, nIndex)] as string;
                                                 key = key.Substring(nIndex + 1);

                                                 fvalue = GetValue(key, FeildConfig, newPass);
                                                 if (String.IsNullOrEmpty(fvalue) == false)
                                                 {
                                                     if (isb64)
                                                     {
                                                         if (this.Isurlencoded)
                                                         {
                                                             return Uri.EscapeDataString(Convert.ToBase64String(UMC.Data.Utility.RSA(n, e, fvalue)));
                                                         }
                                                         else
                                                         {
                                                             return Convert.ToBase64String(UMC.Data.Utility.RSA(n, e, fvalue));
                                                         }
                                                     }
                                                     else
                                                     {
                                                         return UMC.Data.Utility.Hex(UMC.Data.Utility.RSA(n, e, fvalue));
                                                     }
                                                 }
                                             }
                                         }
                                     }

                                     break;
                             }
                         }

                         if (FeildConfig.ContainsKey(key))
                         {
                             return Isurlencoded ? Uri.EscapeDataString(FeildConfig[key] as string ?? "") : FeildConfig[key] as string;
                         }
                         else
                         {
                             var cookie = this.Cookies.GetCookie(key);
                             if (cookie != null)
                             {
                                 return Isurlencoded ? Uri.EscapeDataString(cookie.Value) : cookie.Value;
                             }
                         }
                         return null;
                 }
             };

            MatchEvaluator matchEvaluator = r =>
            {

                var key = r.Groups["key"].Value;
                var kIndex = key.IndexOf('[');
                if (kIndex > 0)
                {
                    var value = func(key.Substring(0, kIndex));
                    if (value == null)
                    {
                        return r.Value;
                    }
                    else
                    {
                        var sValue = key.Substring(kIndex + 1).Trim('[', ']').Trim().Split(',');

                        switch (sValue.Length)
                        {
                            case 1:
                                return value.Substring(UMC.Data.Utility.IntParse(sValue[0], 0));
                            case 2:
                                return value.Substring(UMC.Data.Utility.IntParse(sValue[0], 0), UMC.Data.Utility.IntParse(sValue[1], value.Length));

                            default:
                                return r.Value;
                        }

                    }
                }
                else
                {
                    return func(key) ?? r.Value;
                }

            };
            return matchEvaluator;
        }
        string _LoginRedirectLocation;
        bool XHR(Hashtable login, NameValueCollection form, Hashtable FeildConfig, String fieldKey, String newPass, out NetHttpResponse httpResponse)
        {
            errorMsg = String.Empty;
            httpResponse = null;
            if (login.ContainsKey("Finish"))
            {

                if (String.IsNullOrEmpty(this.SiteCookie.Account) && String.IsNullOrEmpty(this.Password))
                {
                    return false;
                }
                var username = this.SiteCookie.Account;
                var Password = this.Password;

                var matchEvaluator = Match(FeildConfig, newPass);
                var list = new List<string>();
                list.Add(username);
                list.Add(Password);
                if (string.IsNullOrEmpty(newPass) == false)
                {
                    list.Add(newPass);
                }
                this.Isurlencoded = true;
                var feilds = login["Feilds"] as Hashtable ?? new Hashtable();
                if (feilds.Count > 0)
                {
                    var fd = feilds.Keys.Cast<String>().OrderBy(r => r).GetEnumerator();
                    while (fd.MoveNext())
                    {
                        var fdKey = fd.Current;

                        var fvalue = form.Get(fdKey);
                        if (String.IsNullOrEmpty(fvalue))
                        {
                            var conf = GetConf(String.Format("SITE_MIME_{0}_{2}_{1}", Site.Root, fdKey, fieldKey).ToUpper());


                            var obj = UMC.Data.JSON.Deserialize(GetConfig(conf, matchEvaluator, list.ToArray()));
                            if (obj is Array)
                            {
                                Array array = obj as Array;
                                switch (array.Length)
                                {
                                    case 0:
                                        return false;
                                    case 1:
                                        var h = array.GetValue(0) as Hashtable;
                                        if (h != null)
                                        {
                                            var fValue = h["Value"] as string;
                                            FeildConfig[fdKey] = h["Value"];
                                            if (String.IsNullOrEmpty(fValue))
                                            {

                                                errorMsg = $"获取到{feilds[fdKey]}的格式不正确";
                                                return false;
                                            }
                                        }
                                        else
                                        {

                                            errorMsg = $"获取到{feilds[fdKey]}的格式不正确";
                                            return false;

                                        }
                                        break;
                                    default:
                                        if (conf.ContainsKey("RememberValue") == false || FeildConfig.ContainsKey(fdKey) == false)
                                        {

                                            if (conf.Contains("DefautValue"))
                                            {
                                                var sKey = SiteConfig.Config(conf["DefautValue"] as string);
                                                if (sKey.Length > 0)
                                                {
                                                    int iNdex = 0;
                                                    for (; iNdex < array.Length; iNdex++) //(var k in array)
                                                    {
                                                        var val = array.GetValue(iNdex) as Hashtable;
                                                        if (val != null)
                                                        {
                                                            var fValue = val["Value"] as string;
                                                            if (String.Equals(fValue, sKey[0]))
                                                            {
                                                                FeildConfig[fdKey] = fValue;
                                                                FeildConfig[fdKey + "_Text"] = val["Text"];
                                                                break;
                                                            }
                                                        }
                                                    }
                                                    if (iNdex == array.Length)
                                                    {
                                                        errorMsg = $"请选择{feilds[fdKey]}";
                                                        return false;
                                                    }
                                                }
                                                else
                                                {
                                                    errorMsg = $"请选择{feilds[fdKey]}";
                                                    return false;
                                                }
                                            }
                                            else
                                            {
                                                errorMsg = $"请选择{feilds[fdKey]}";
                                                return false;
                                            }
                                        }
                                        break;
                                }
                            }
                            else if (obj is Hashtable)
                            {
                                var ms = (obj as Hashtable).GetEnumerator();
                                while (ms.MoveNext())
                                {
                                    FeildConfig[ms.Key] = ms.Value;
                                }
                            }
                            else
                            {
                                errorMsg = $"获取到{feilds[fdKey]}的格式不正确";
                                return false;
                            }
                        }
                        else
                        {
                            String fText = form.Get(fdKey + "_Text");
                            if (String.IsNullOrEmpty(fText) == false)
                            {

                                FeildConfig[fdKey + "_Text"] = fText;
                            }
                            FeildConfig[fdKey] = fvalue;
                        }
                    }

                }
                var rawUrl = login["RawUrl"] as string;
                if (String.IsNullOrEmpty(rawUrl))
                {
                    errorMsg = "接口请求地址未配置";
                    return false;

                }

                var Header = login["Header"] as string;
                if (String.IsNullOrEmpty(Header) == false)
                {
                    this.Isurlencoded = false;
                    Header = Regex.Replace(Header, matchEvaluator);
                }

                this.Isurlencoded = true;


                var PathAndQuery = Regex.Replace(rawUrl, matchEvaluator);

                Uri getUrl = null;

                var sStrDomain = login["Domain"] as string;

                if (String.IsNullOrEmpty(sStrDomain) == false)
                {
                    getUrl = new Uri(new Uri(sStrDomain), PathAndQuery);

                }
                else
                {
                    getUrl = new Uri(Domain, PathAndQuery);
                }


                var Method = login["Method"] as string;
                if (String.IsNullOrEmpty(Method))
                {
                    errorMsg = "接口Method未配置";
                    return false;
                }
                var value = login["Content"] as string;

                switch (Method)
                {
                    case "POST":
                    case "PUT":
                        var ContentType = login["ContentType"] as string;
                        if (String.IsNullOrEmpty(ContentType))
                        {
                            errorMsg = "接口ContentType未配置";
                            return false;
                        }
                        else
                        {
                            this.Isurlencoded = ContentType.Contains("urlencoded");
                            var valResult = Regex.Replace(value, matchEvaluator);

                            var webR = this.Context.Transfer(getUrl, this.Cookies).Header(Header);
                            webR.ContentType = ContentType;
                            httpResponse = this.Reqesut(webR).Net(Method, valResult);
                            if (this.IsLog == true)
                            {
                                this.Loger.Write(Method);
                                this.Loger.Write(":");
                                this.Loger.WriteLine(getUrl.PathAndQuery);
                                this.Loger.WriteLine(Utility.NameValue(webR.Headers));
                                this.Loger.WriteLine(valResult);
                                this.Loger.WriteLine();
                            }
                        }
                        break;
                    case "GET":
                        var webr2 = this.Context.Transfer(getUrl, this.Cookies).Header(Header);
                        httpResponse = this.Reqesut(webr2).Get();

                        if (this.IsLog == true)
                        {
                            this.Loger.Write(Method);
                            this.Loger.Write(":");
                            this.Loger.WriteLine(getUrl.PathAndQuery);
                            this.Loger.WriteLine(webr2.Headers);

                            this.Loger.WriteLine();
                        }
                        break;
                    default:
                        errorMsg = "接口Method不支持";
                        return false;
                }
                if (this.IsLog == true)
                {
                    this.Loger.WriteLine("{0} {1} {2}", httpResponse.ProtocolVersion, (int)httpResponse.StatusCode, httpResponse.StatusDescription);

                    this.Loger.WriteLine(Utility.NameValue(httpResponse.Headers));
                    this.Loger.WriteLine();
                }
                var finish = login["Finish"] as string;

                if (finish.StartsWith("H:"))
                {
                    var key = finish.Substring(2);
                    var keyIndex = key.IndexOf(':');
                    if (keyIndex > 0)
                    {
                        var v = key.Substring(keyIndex + 1).Trim();
                        key = key.Substring(0, keyIndex);
                        var keyValue = httpResponse.Headers.Get(key);
                        if (String.IsNullOrEmpty(keyValue) == false)
                        {
                            if (String.Equals(keyValue, v))
                            {
                                return true;

                            }
                        }
                    }
                    else if (String.IsNullOrEmpty(httpResponse.Headers.Get(key)) == false)
                    {
                        return true;
                    }
                }
                else if (finish.StartsWith("HE:"))
                {
                    var key = finish.Substring(3);
                    var keyIndex = key.IndexOf(':');
                    if (keyIndex > 0)
                    {
                        var v = key.Substring(keyIndex + 1).Trim();
                        key = key.Substring(0, keyIndex);
                        var keyValue = httpResponse.Headers.Get(key);
                        if (String.IsNullOrEmpty(keyValue) == false)
                        {
                            if (String.Equals(keyValue, v) == false)
                            {
                                return true;
                            }

                        }
                    }
                    else if (String.IsNullOrEmpty(httpResponse.Headers.Get(key)))
                    {
                        return true;
                    }
                }
                else
                {
                    switch (httpResponse.StatusCode)
                    {
                        case HttpStatusCode.Redirect:
                        case HttpStatusCode.RedirectKeepVerb:
                        case HttpStatusCode.RedirectMethod:
                            _LoginRedirectLocation = httpResponse.Headers.Get("Location");
                            if (String.Equals("Url", finish))
                            {
                                return true;
                            }
                            break;
                        case HttpStatusCode.OK:

                            _CheckBody = httpResponse.ReadAsString();

                            if (this.IsLog == true)
                            {
                                this.Loger.WriteLine(_CheckBody);
                                this.Loger.WriteLine();
                            }
                            if (finish.StartsWith("E:"))
                            {
                                if (_CheckBody.Contains(finish.Substring(2)) == false)
                                {
                                    return true;

                                }
                            }
                            else if (String.Equals("Url", finish) == false)
                            {
                                if (_CheckBody.Contains(finish))
                                {
                                    return true;

                                }
                            }
                            break;
                        default:

                            int statusCode = Convert.ToInt32(httpResponse.StatusCode);
                            if (statusCode >= 500)
                            {
                                LogWrite(this.Context, this.Site, statusCode, String.Format("{0} {1}", Method, getUrl.PathAndQuery), this.SiteCookie.Account, 0, httpResponse.Headers, _AttachmentFile);

                            }
                            return false;

                    }


                }
                return false;
            }
            else
            {
                errorMsg = "接口配置未完善";
                return false;
            }

        }
        String _CheckBody;

        bool Login(bool isHome, bool isBody, NameValueCollection form, String apiKey)
        {

            var username = form.Get("Username");
            this.Password = form.Get("Password");

            if (String.IsNullOrEmpty(username) == false)
            {
                this.SiteCookie.Account = username;
            }
            var user = this.Context.Token.Identity();

            if (this.Site.Site.UserModel == Entities.UserModel.Quote)
            {
                if (this.SiteCookie.IndexValue == 0)
                {

                    if (String.IsNullOrEmpty(this.Site.Site.Account) == false && this.Site.Site.Account.StartsWith("@"))
                    {
                        var root = this.Site.Site.Account.Substring(1);
                        this.SiteCookie.Account = user.Name;
                        this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(root, this.SiteCookie.user_id.Value, 0));
                        if (String.IsNullOrEmpty(Password))
                        {
                            var home = Data.WebResource.Instance().WebDomain();
                            var union = Data.WebResource.Instance().Provider["union"] ?? "-";

                            this.Context.Redirect($"{this.Context.Url.Scheme}://{this.Site.Site.Account.Substring(1)}{union}{home}/UMC.Login?callback={Uri.EscapeDataString(this.Context.Url.AbsoluteUri)}");
                            return true;
                        }

                    }
                    else
                    {
                        WebServlet.Error(this.Context, "登录异常", String.Format("{0}应用引用模式设置错误，请联系管理员", this.Site.Caption, this.SiteCookie.Account), "");
                        return true;
                    }


                }


            }
            if (String.IsNullOrEmpty(this.SiteCookie.Account) == false && String.IsNullOrEmpty(this.Password))
            {
                this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(Site.Root, this.SiteCookie.user_id.Value, this.SiteCookie.IndexValue ?? 0));
                this.sourceUP = String.Format("{0}{1}", this.SiteCookie.Account, this.Password);

            }
            var login = GetConf(String.Format("SITE_MIME_{0}_LOGIN", Site.Root).ToUpper());
            var autoCheck = false;
            if (String.IsNullOrEmpty(this.SiteCookie.Account) || String.IsNullOrEmpty(this.Password))
            {
                if (this.SiteCookie.IndexValue > 0)
                {
                    LoginHtml("", true);
                    return true;
                }
                switch (this.Site.Site.UserModel ?? Entities.UserModel.Standard)
                {
                    case Entities.UserModel.Standard:
                        LoginHtml("", true);
                        return true;
                    case Entities.UserModel.Checked:

                        this.SiteCookie.Account = user.Name;
                        this.Password = this.ResetPasswork(login, form, this.Site);
                        if (String.IsNullOrEmpty(this.Password))
                        {
                            WebServlet.Error(this.Context, "登录异常", String.IsNullOrEmpty(errorMsg) ? String.Format("应用中未检测到{1}账户，请联系{0}应用管理员确认账户", this.Site.Caption, this.SiteCookie.Account) : errorMsg, "");
                            return true;
                        }
                        autoCheck = true;
                        this.SiteCookie.Model = (this.SiteCookie.Model ?? Entities.AccountModel.Standard) | Entities.AccountModel.Check | Entities.AccountModel.Changed;
                        this.SiteCookie.ChangedTime = 0;

                        break;

                    case Entities.UserModel.Check:

                        switch (apiKey)
                        {
                            case "Input":
                                LoginHtml("", true);
                                return true;
                            case "Auto":
                                this.SiteCookie.Account = user.Name;
                                this.Password = this.ResetPasswork(login, form, this.Site);
                                if (String.IsNullOrEmpty(this.Password))
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.IsNullOrEmpty(errorMsg) ? String.Format("应用中未发现{1}账户，您可联系{0}应用管理员或使用<a href=\"/UMC.Login/Input\">其他账户</a>登录", this.Site.Caption, this.SiteCookie.Account) : errorMsg, "");
                                    return true;
                                }
                                autoCheck = true;
                                this.SiteCookie.Model = (this.SiteCookie.Model ?? Entities.AccountModel.Standard) | Entities.AccountModel.Check | Entities.AccountModel.Changed;
                                this.SiteCookie.ChangedTime = 0;
                                break;
                            default:
                                LoginCheckHtml();
                                return true;
                        }
                        break;
                    case Entities.UserModel.Quote:
                        var quoteRoot = this.Site.Site.Account.Substring(1);
                        var home = Data.WebResource.Instance().WebDomain();
                        var union = Data.WebResource.Instance().Provider["union"] ?? "-";

                        this.Context.Redirect($"{this.Context.Url.Scheme}://{quoteRoot}{union}{home}/UMC.Login?callback={Uri.EscapeDataString(this.Context.Url.AbsoluteUri)}");


                        return true;
                    case Entities.UserModel.Share:
                        if (this.ShareUser() == false)
                        {
                            WebServlet.Error(this.Context, "登录异常", String.Format("{0}采用共享账户，但账户却未设置，请联系管理员", this.Site.Caption), "");

                        }
                        break;
                }
            }

            if (login.ContainsKey("IsLoginHTML") && this.Context.HttpMethod == "GET" && isBody == false)
            {
                LoginHtml("", false);
                return true;
            }
            if (login.ContainsKey("IsNotCookieClear") == false)
            {
                this.Cookies = new NetCookieContainer(this.SetCookie);
            }

            var feildConfig = UMC.Data.JSON.Deserialize<Hashtable>(this.SiteCookie.Config) ?? new Hashtable();
            var lkeyIndex = this.RawUrl.IndexOf('?');
            if (lkeyIndex > 0)
            {
                var qs = System.Web.HttpUtility.ParseQueryString(this.RawUrl.Substring(lkeyIndex));
                for (var i = 0; i < qs.Count; i++)
                {
                    var key = qs.GetKey(i);
                    var value = qs.Get(i);
                    if (String.IsNullOrEmpty(key) == false && String.IsNullOrEmpty(value) == false)
                    {
                        feildConfig[key] = value;

                    }
                }
            }

            if (this.IsLog == true)
                this.Loger.WriteLine("用户登录:");
            NetHttpResponse httpResponse = null;
            try
            {
                var isOk = XHR(login, form, feildConfig, "LOGIN", "", out httpResponse);

                if (isBody)
                {
                    if (httpResponse.IsReadBody)
                    {
                        this.Header(httpResponse);
                        this.Context.Output.Write(_CheckBody);
                    }
                    else
                    {
                        this.Context.UseSynchronousIO(this.ProcessEnd);
                        this.Response(httpResponse);

                    }
                    return true;

                }
                if (isOk)
                {
                    return LoginAtfer(feildConfig, form, isHome, login, httpResponse);
                }
                else
                {
                    if (String.IsNullOrEmpty(errorMsg) == false)
                    {
                        WebServlet.Error(this.Context, "登录配置异常", errorMsg, "");
                        return true;
                    }
                    else if (this.SiteCookie.IndexValue != 0)
                    {
                        LoginHtml("账户或密码不正确", true);
                        return true;
                    }
                    else
                    {
                        switch (this.Site.Site.UserModel ?? Entities.UserModel.Standard)
                        {
                            case Entities.UserModel.Standard:

                                LoginHtml(String.IsNullOrEmpty(errorMsg) ? "账户或密码不正确" : errorMsg, true);
                                return true;
                            case Entities.UserModel.Checked:

                                if (autoCheck)
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.IsNullOrEmpty(errorMsg) ? String.Format("在{0}应用{1}检测账户登录失败，您可联系管理员重置标准账户", this.Site.Caption, user.Name) : errorMsg, "");
                                    return true;
                                }
                                else
                                {
                                    this.Password = this.ResetPasswork(login, form, this.Site);


                                    if (String.IsNullOrEmpty(this.Password))
                                    {
                                        WebServlet.Error(this.Context, "登录异常", String.IsNullOrEmpty(errorMsg) ? String.Format("在{0}应用{1}检测账户登录失败，您可联系管理员重置标准账户", this.Site.Caption, user.Name) : errorMsg, "");
                                        return true;
                                    }
                                    else
                                    {
                                        this.SiteCookie.Account = user.Name;

                                        this.SiteCookie.Model = (this.SiteCookie.Model ?? Entities.AccountModel.Standard) | Entities.AccountModel.Check | Entities.AccountModel.Changed; ;
                                        this.SiteCookie.ChangedTime = 0;

                                        feildConfig = UMC.Data.JSON.Deserialize<Hashtable>(this.SiteCookie.Config) ?? new Hashtable();
                                        httpResponse.ReadAsString();
                                        if (XHR(login, form, feildConfig, "LOGIN", "", out httpResponse))
                                        {
                                            return LoginAtfer(feildConfig, form, isHome, login, httpResponse);
                                        }
                                        else
                                        {
                                            WebServlet.Error(this.Context, "登录异常", String.Format("{0}是采用检测密码账户，却不能正常使用，请联系管理员", this.Site.Caption, this.Site.Site.Account.Substring(1)), "");
                                            return true;
                                        }
                                    }
                                }
                            case Entities.UserModel.Check:

                                var am = this.SiteCookie.Model ?? Entities.AccountModel.Standard;
                                if ((am & Entities.AccountModel.Check) == Entities.AccountModel.Check)
                                {
                                    goto case Entities.UserModel.Checked;
                                }
                                else
                                {
                                    DataFactory.Instance().Delete(this.SiteCookie);
                                    LoginCheckHtml();
                                    return true;
                                }

                            case Entities.UserModel.Quote:
                                var currentTime = UMC.Data.Utility.TimeSpan();
                                var q = UMC.Data.Utility.IntParse(this.Context.Token.Get("DeviceQuote") as string, 0);
                                if (q + 10 > currentTime)
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.Format("{0}是引用账户，多次尝试却没有成功，请联系管理员", this.Site.Caption, this.Site.Site.Account.Substring(1)), "");
                                    return true;
                                }
                                else
                                {
                                    var quoteRoot = this.Site.Site.Account.Substring(1);
                                    this.Context.Token.Put("DeviceQuote", currentTime.ToString()).Commit(Context.UserHostAddress, Context.Server);
                                    var home = Data.WebResource.Instance().WebDomain();
                                    var union = Data.WebResource.Instance().Provider["union"] ?? "-";

                                    this.Context.Redirect($"{this.Context.Url.Scheme}://{quoteRoot}{union}{home}/UMC.Login?callback={Uri.EscapeDataString(this.Context.Url.AbsoluteUri)}");
                                }
                                return true;
                            case Entities.UserModel.Share:

                                if (this.ShareUser() == false)
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.Format("{0}是采用共享账户，却没有设置账户，请联系管理员", this.Site.Caption, this.Site.Site.Account.Substring(1)), "");
                                    return true;
                                }
                                httpResponse.ReadAsString();
                                feildConfig = UMC.Data.JSON.Deserialize<Hashtable>(this.SiteCookie.Config) ?? new Hashtable();
                                if (XHR(login, form, feildConfig, "LOGIN", "", out httpResponse))
                                {
                                    return LoginAtfer(feildConfig, form, isHome, login, httpResponse);
                                }
                                else
                                {
                                    WebServlet.Error(this.Context, "登录异常", String.Format("{0}是采用共享账户，却不能正常使用，请联系管理员", this.Site.Caption, this.Site.Site.Account.Substring(1)), "");
                                    return true;
                                }
                            default:
                                LoginHtml(String.IsNullOrEmpty(errorMsg) ? "账户或密码不正确" : errorMsg, true);
                                return true;
                        }



                    }
                }
            }
            finally
            {
                if (httpResponse != null)
                    httpResponse.ReadAsString();
            }
        }
        public bool ShareUser()
        {
            var user = this.Site.Site.Account;
            if (String.IsNullOrEmpty(user) == false)
            {

                var vindex = user.IndexOf("~");
                if (vindex > -1)
                {
                    var nv = user.Substring(0, vindex);
                    var fv = user.Substring(vindex + 1);
                    int start = UMC.Data.Utility.IntParse(nv.Substring(nv.Length - fv.Length), -1);
                    int end = UMC.Data.Utility.IntParse(fv, 0);

                    var index = "0000000" + (start + (Data.Reflection.TimeSpanMilli(DateTime.Now) % (end - start + 1)));
                    this.SiteCookie.Account = nv.Substring(0, nv.Length - fv.Length) + index.Substring(index.Length - fv.Length);
                    this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(this.Site.Root, this.Site.Site.Account));
                    return true;
                }
                else if (user.IndexOf('|') > 0)
                {
                    var us = user.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    this.SiteCookie.Account = us[Data.Reflection.TimeSpanMilli(DateTime.Now) % us.Length];
                    this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(this.Site.Root, this.Site.Site.Account));
                    return true;
                }
                else
                {
                    this.SiteCookie.Account = user;
                    this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(this.Site.Root, this.Site.Site.Account));
                    return true;
                }
            }
            return false;
        }

        bool LoginAtfer(Hashtable fieldConfig, NameValueCollection form, bool isHome, Hashtable loginConfig, NetHttpResponse httpResponse)
        {
            this.IsChangeUser = true;
            var configValue = new Hashtable();
            var fdcem = fieldConfig.GetEnumerator();
            while (fdcem.MoveNext())
            {
                if (fdcem.Key.ToString().StartsWith("_") == false)
                {
                    configValue[fdcem.Key] = fdcem.Value;
                }
            }
            switch (this.Site.Site.UserModel)
            {
                case Entities.UserModel.Share:
                    break;
                default:
                    if (this.Site.Site.IsAuth == true)
                    {
                        var user = this.User;
                        if (String.Equals(this.SiteCookie.Account, user.Name) || String.IsNullOrEmpty(this.SiteCookie.Account))
                        {
                            configValue["__ORGA"] = user.Organizes;
                            configValue["__ROLE"] = String.Join(",", UMC.Data.DataFactory.Instance().Roles(user.Id.Value, this.Site.Site.SiteKey.Value));//.Join(",");
                        }
                        else
                        {
                            var account = Security.Membership.Instance().Identity(this.Site.Site.SiteKey.Value, this.SiteCookie.Account);
                            if (account != null)
                            {
                                configValue["__ALIAS"] = account.Alias;
                                configValue["__ROLE"] = String.Join(",", account.Roles);
                                configValue["__ORGA"] = String.Join(",", account.Organizes);
                            }
                        }

                    }
                    break;
            }
            this.SiteCookie.Config = UMC.Data.JSON.Serialize(configValue);

            var IsLoginHTML = loginConfig.ContainsKey("IsLoginHTML");
            if (IsLoginHTML)
            {
                var script = loginConfig["Script"] as string;
                if (String.IsNullOrEmpty(script) == false && String.Equals(script, "none", StringComparison.CurrentCultureIgnoreCase) == false)
                {
                    var html = httpResponse.IsReadBody ? this._CheckBody : httpResponse.ReadAsString();

                    var values = this.GetKeyValue(html, script);
                    var nvs = (values as Hashtable).GetEnumerator();

                    while (nvs.MoveNext())
                    {
                        fieldConfig[nvs.Key] = nvs.Value;
                    }
                }

            }


            switch (this.Site.Site.UserModel ?? Entities.UserModel.Standard)
            {
                case Entities.UserModel.Check:
                case Entities.UserModel.Checked:
                case Entities.UserModel.Standard:
                    var AccountModel = this.SiteCookie.Model ?? Entities.AccountModel.Standard;

                    var changeTime = (this.SiteCookie.ChangedTime ?? 0) + 3600 * 24 * 100;

                    if (String.Equals(form.Get("AutoUpdatePwd"), "YES"))
                    {

                        this.Update(fieldConfig, form);
                    }
                    else if ((AccountModel & Entities.AccountModel.Changed) == Entities.AccountModel.Changed && changeTime < UMC.Data.Utility.TimeSpan())
                    {
                        this.Update(fieldConfig, form);

                    }
                    break;

            }
            if (String.Equals("/UMC.Login/New", this.RawUrl))
            {
                this.Context.AddHeader("Cache-Control", "no-store");
                this.Context.ContentType = "text/html; charset=UTF-8";
                this.Context.Output.WriteLine("<script>window.top.postMessage(JSON.stringify({ type: 'msg', value: '设置多账户成功，现在你可以打开了' }), '*');");
                this.Context.Output.WriteLine("window.top.postMessage(JSON.stringify({ type: 'close', value: 'close' }), '*');</script>");
                return true;
            }
            if (CheckBrowser() == false)
            {
                return true;

            }
            var callbackKey = loginConfig["Callback"] as string ?? "callback";
            var callback = this.Context.QueryString.Get(callbackKey);
            if (String.IsNullOrEmpty(callback) == false)
            {
                this.Context.Redirect(callback);
                return true;
            }


            if (IsLoginHTML)
            {
                var mainKey = String.Format("SITE_MIME_{0}_LOGIN_HTML", this.Site.Root).ToUpper();
                var config = UMC.Data.DataFactory.Instance().Config(mainKey);
                if (config != null && String.Equals(config.ConfValue, "none") == false)
                {
                    this.Isurlencoded = false;
                    this.Context.AddHeader("Cache-Control", "no-store");
                    this.Context.ContentType = "text/html; charset=UTF-8";

                    using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                   .GetManifestResourceStream("UMC.Proxy.Resources.login-html.html"))
                    {
                        var str = new System.IO.StreamReader(stream).ReadToEnd();
                        var matchEvaluator = Match(fieldConfig, "");
                        this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                        {
                            var key = g.Groups["key"].Value.ToLower();
                            switch (key)
                            {
                                case "title":
                                    return String.Format("{0}账户对接", this.Site.Caption);
                                case "html":
                                    return Regex.Replace(config.ConfValue, matchEvaluator);

                            }
                            return "";

                        }));

                    }
                    return true;
                }
            }

            if (isHome)
            {

                if (String.IsNullOrEmpty(_LoginRedirectLocation) == false)
                {
                    var path = _LoginRedirectLocation;
                    if (_LoginRedirectLocation.StartsWith("http://") || _LoginRedirectLocation.StartsWith("http://"))
                    {
                        path = new Uri(_LoginRedirectLocation).PathAndQuery;
                    }

                    if (IsLoginPath(this.Site, path))
                    {
                        this.Context.Redirect(new Uri(this.Domain, this.Site.Home ?? "/").PathAndQuery);

                    }
                    else
                    {
                        this.Context.Redirect(ReplaceRedirect(_LoginRedirectLocation));
                    }
                }
                else
                {
                    this.Context.Redirect(new Uri(this.Domain, this.Site.Home ?? "/").PathAndQuery);
                }

            }
            return isHome;

        }

        bool SaveCookie()
        {

            if (IsChangeUser == false || this.StaticModel == 0)
            {
                return false;
            }

            var siteCookie = new Entities.Cookie
            {
                Domain = this.Site.Root,
                Time = DateTime.Now,
                user_id = this.SiteCookie.user_id,
                IndexValue = this.SiteCookie.IndexValue

            };
            String strCol = UMC.Data.JSON.Serialize(this.Cookies);
            var isSaveCookie = false;
            if (String.Equals(strCol, this.SiteCookie.Cookies) == false)
            {
                siteCookie.Cookies = strCol;
                this.SiteCookie.Cookies = strCol;
                isSaveCookie = true;
            }

            if (this.IsChangeUser == true)
            {

                var nUP = String.Format("{0}{1}", this.SiteCookie.Account, this.Password);

                if (String.Equals(nUP, this.sourceUP) == false)
                {
                    switch (this.Site.Site.UserModel)
                    {
                        case Entities.UserModel.Quote:

                            if (this.SiteCookie.IndexValue > 0)
                            {
                                UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(Site.Root, siteCookie.user_id.Value, siteCookie.IndexValue ?? 0), this.Password);
                            }
                            break;
                        default:
                            UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(Site.Root, siteCookie.user_id.Value, siteCookie.IndexValue ?? 0), this.Password);

                            break;
                    }
                    siteCookie.Account = this.SiteCookie.Account;
                    siteCookie.ChangedTime = UMC.Data.Utility.TimeSpan();
                    siteCookie.Model = this.SiteCookie.Model;
                    this.sourceUP = nUP;
                }

                siteCookie.Config = this.SiteCookie.Config;
                siteCookie.LoginTime = UMC.Data.Utility.TimeSpan();
                this.IsChangeUser = null;
                isSaveCookie = true;

            }
            if (isSaveCookie)
            {
                DataFactory.Instance().Put(siteCookie);
                return true;
            }
            else if (this.SiteCookie.Time < DateTime.Now.AddSeconds(-300))
            {
                DataFactory.Instance().Put(siteCookie);
                return true;

            }

            return false;
        }
        public NetCookieContainer Cookies
        {
            get;
            private set;
        }


        public static void LogWrite(NetContext context, SiteConfig config, int statusCode, String pathAndQuery, string account, int duration, NameValueCollection reHeaders, String attachmentFile)
        {
            var logSetting = LogSetting.Instance();
            if (logSetting.IsWriter)
            {
                var webMeta = new WebMeta();
                if (String.IsNullOrEmpty(account) == false)
                {
                    webMeta.Put("Account", account);
                }
                var time = (int)((UMC.Data.Reflection.TimeSpanMilli(DateTime.Now) - duration) / 1000);
                if (context.UrlReferrer != null)
                {
                    webMeta.Put("Referrer", context.UrlReferrer.AbsoluteUri);
                }
                if (String.IsNullOrEmpty(attachmentFile) == false)
                {
                    webMeta.Put("Attachment", attachmentFile);
                }
                foreach (var u in config.LogConf.Cookies)
                {
                    webMeta[u] = context.Cookies[u];
                }

                foreach (var u in config.LogConf.Headers)
                {
                    webMeta[u] = context.Headers[u];
                }
                if (reHeaders != null)
                {
                    foreach (var u in config.LogConf.ResHeaders)
                    {
                        webMeta[u] = reHeaders[u];
                    }
                }

                if (context.Token != null)
                {
                    var username = context.Token.Username;
                    if (String.IsNullOrEmpty(username) || String.Equals(username, "?"))
                    {
                        username = $"G:{UMC.Data.Utility.IntParse((context.Token.UserId ?? context.Token.Device).Value)}";
                    }
                    webMeta.Put("Username", username);
                }
                webMeta.Put("Address", context.UserHostAddress).Put("Server", context.Server).Put("UserAgent", context.UserAgent)
                .Put("Path", pathAndQuery)
                .Put("Duration", duration)
                .Put("Site", config.Root)
                .Put("Time", time)
                .Put("Status", statusCode);

                logSetting.Write(webMeta);
            }

        }
        NameValueCollection m_HttpHeaders;
        String _AttachmentFile;
        void Header(NetHttpResponse httpResponse, bool isContentEncoding, bool isCache)
        {
            _AttachmentFile = httpResponse.AttachmentFile;
            m_HttpHeaders = httpResponse.Headers;
            var statusCode = Convert.ToInt32(httpResponse.StatusCode);
            this.Context.StatusCode = statusCode;
            for (var i = 0; i < m_HttpHeaders.Count; i++)
            {
                var key = m_HttpHeaders.GetKey(i);

                switch (key.ToLower())
                {
                    case "set-cookie":
                        if (this.Site.Site.AuthType == WebAuthType.All)
                        {
                            var vs = m_HttpHeaders.GetValues(i);
                            foreach (var k in vs)
                            {
                                this.Context.AddHeader(key, k);
                            }
                        }
                        break;
                    case "content-encoding":
                        if (isContentEncoding)
                        {
                            this.Context.AddHeader(key, m_HttpHeaders.Get(i));
                        }
                        break;
                    case "location":
                        if (statusCode >= 300 && statusCode < 400)
                        {
                            this.Context.AddHeader(key, ReplaceRedirect(m_HttpHeaders.Get(i)));
                        }
                        else
                        {

                            this.Context.AddHeader(key, m_HttpHeaders.Get(i));
                        }
                        break;
                    case "content-type":
                    case "server":
                    case "connection":
                    case "keep-alive":
                        break;
                    case "content-length":
                    case "transfer-encoding":
                        if (httpResponse.IsHead)
                        {
                            this.Context.AddHeader(key, m_HttpHeaders.Get(i));
                        }
                        break;

                    case "last-modified":
                    case "etag":
                        if (isCache)
                        {
                            this.Context.AddHeader(key, m_HttpHeaders.Get(i));
                        }
                        break;
                    default:
                        this.Context.AddHeader(key, m_HttpHeaders.Get(i));
                        break;
                }
            }
            var ContentType = httpResponse.ContentType;

            if (String.IsNullOrEmpty(ContentType) == false)
            {
                this.Context.ContentType = ContentType;
            }
            else if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                this.Context.ContentType = this.Site.ContentType;
            }
        }
        void Header(NetHttpResponse httpResponse)
        {
            Header(httpResponse, true, true);
        }
        public void Response(NetHttpResponse httpResponse)
        {
            if (httpResponse.IsHead)
            {
                Header(httpResponse);
            }
            else
            {
                var ContentType = httpResponse.ContentType;
                var ContentEncoding = httpResponse.ContentEncoding;
                SiteConfig.ReplaceSetting replaceSetting = null;

                int model = 0;
                if (String.IsNullOrEmpty(ContentType) == false)
                {
                    var vIndex = ContentType.IndexOf(';');
                    if (vIndex > 0)
                    {
                        ContentType = ContentType.Substring(0, vIndex);
                    }
                    var path = this.RawUrl.Split('?')[0];
                    if (String.Equals(ContentType, "text/html", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (CheckPath(path, out var _, this.Site.AppendJSConf))
                        {
                            model = 1;
                        }
                        else if (this.CheckPath(path, ContentType, out replaceSetting))
                        {
                            if (replaceSetting.Model != SiteConfig.HostReplaceModel.Input)
                            {
                                model = 1;
                            }
                        }

                    }
                    else if (this.CheckPath(path, ContentType, out replaceSetting))
                    {
                        model = 2;
                    }

                }
                switch (model)
                {
                    case 1:
                    case 2:
                        Header(httpResponse, false, true);
                        httpResponse.ReadAsStream(content =>
                        {
                            content.Position = 0;
                            switch (model)
                            {
                                case 1:
                                    OuterHTML(content, ContentEncoding, this.Context.OutputStream);
                                    break;
                                default:
                                    this.OuterReplaceHost(content, ContentEncoding, replaceSetting, this.Context.OutputStream);
                                    break;
                            }
                            this.Context.OutputFinish();
                            content.Close();
                        }, this.Context.Error);

                        //}
                        return;
                    default:
                        Header(httpResponse);
                        break;
                }
            }


            if (httpResponse.ContentLength > -1)
            {
                this.Context.ContentLength = httpResponse.ContentLength;

            }

            httpResponse.ReadAsData((b, i, c) =>
            {
                if (c == 0 && b.Length == 0)
                {
                    if (i == -1)
                    {
                        this.Context.Error(httpResponse.Error);
                    }
                    else
                    {
                        this.Context.OutputFinish();
                    }
                }
                else
                {
                    this.Context.OutputStream.Write(b, i, c);
                }
            });


        }
        UMC.Security.Identity User;
        long StartTime;

        public void ProcessEnd()
        {

            LogWrite(this.Context, this.Site, this.Context.StatusCode, String.Format("{0} {1}", Context.HttpMethod, this.RawUrl), this.SiteCookie.Account, (int)(UMC.Data.Reflection.TimeSpanMilli(DateTime.Now) - StartTime), m_HttpHeaders, _AttachmentFile);

            if (this.Site.Site.AuthType > WebAuthType.All)
            {
                this.SaveCookie();
            }
            if (this.IsLog && User.IsAuthenticated)
            {
                this.Loger.WriteLine("Cookie:{0}", this.Cookies.GetCookieHeader(this.Domain));

                var file = UMC.Data.Reflection.ConfigPath(String.Format("Static\\log\\{0}\\{1}.log", Site.Root, User.Name));
                if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(file)))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
                }
                using (FileStream stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    var writer = new System.IO.StreamWriter(stream);
                    writer.Write(this.Loger.ToString());
                    writer.Flush();
                    writer.Close();
                }
            }

            this.Loger.Close();
        }
        public void AuthBridge()
        {
            var nvs = new System.Collections.Specialized.NameValueCollection();


            var user = this.User;
            switch (this.Site.Site.UserModel)
            {
                case Entities.UserModel.Share:
                    break;
                default:
                    if (this.Site.Site.IsAuth == true)
                    {
                        user = this.Account;
                    }
                    break;
            }
            nvs.Add("umc-request-user-name", user.Name);
            nvs.Add("umc-request-user-id", UMC.Data.Utility.Guid(user.Id.Value));

            if (String.IsNullOrEmpty(user.Alias) == false)
            {
                nvs.Add("umc-request-user-alias", user.Alias);
            }
            if (user.Roles.Length > 0)
            {
                nvs.Add("umc-request-user-roles", String.Join(",", user.Roles));
            }

            if (user.Organizes.Length > 0)
            {
                nvs.Add("umc-request-user-organizes", String.Join(",", user.Organizes));
            }

            if (String.IsNullOrEmpty(Site.Site.AppSecret) == false)
            {
                nvs.Add("umc-request-time", Utility.TimeSpan().ToString());
                nvs.Add("umc-request-sign", Utility.Sign(nvs, Site.Site.AppSecret));
            }

            for (var i = 0; i < nvs.Count; i++)
            {
                this.Context.Headers[nvs.GetKey(i)] = Uri.EscapeDataString(nvs.Get(i));
            }

        }

        public void LoginRequest()
        {
            this.Context.ReadAsForm(this.Login);
        }
        void Login(NameValueCollection form)
        {
            String Domain = this.Context.Url.Host;
            var cdmn = Domain;

            String cookieStr = String.Empty;
            if (Regex.IsMatch(Domain, @"^(\d{1,3}.)+\d{1,3}$") == false)
            {

                var ds = cdmn.Split('.');
                if (ds.Length > 2)
                {
                    cdmn = ds[ds.Length - 2] + "." + ds[ds.Length - 1];
                }
                cookieStr = $" Domain={cdmn};";
            }


            var apis = this.Context.Url.AbsolutePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (apis.Length == 1)
            {
                this.Context.AddHeader("Set-Cookie", $"{this.Site.Root}-{DeviceIndex}=0; Expires={DateTime.Now.AddYears(-10).ToString("r")}; HttpOnly;{cookieStr} Path=/");


                if (this.Site.Site.UserModel == Entities.UserModel.Bridge)
                {
                    this.Context.Redirect(this.Site.Site.Home ?? "/");
                }
                else if (CheckAccountSelectHtml() == false)
                {
                    this.Context.Redirect("/UMC.Login/Go" + this.Context.Url.Query);
                }
                this.ProcessEnd();
                return;
            }
            var lv = apis[1];

            switch (lv)
            {
                case "Out":
                    this.Login(true, true, form, String.Empty);
                    return;
                case "Check":
                    if (Site.Site.AuthType >= WebAuthType.User)
                    {

                        if (this.SiteCookie.Time.HasValue)
                        {
                            var authExpire = this.Site.Site.AuthExpire ?? 30;
                            if (authExpire > 0 && this.SiteCookie.Time.Value.AddMinutes(authExpire) < DateTime.Now)
                            {
                                this.Context.Redirect("/UMC.Login/Go" + this.Context.Url.Query);
                            }
                            else
                            {
                                var callback = this.Context.QueryString.Get("callback");
                                if (String.IsNullOrEmpty(callback))
                                {
                                    this.Context.Redirect(callback);
                                }
                                else if (String.IsNullOrEmpty(this.Site.Site.Home))
                                {
                                    this.Context.Redirect("/");
                                }
                                else
                                {

                                    this.Context.Redirect(this.Site.Site.Home);
                                }
                            }
                        }
                        else
                        {

                            this.Context.Redirect("/UMC.Login/Go" + this.Context.Url.Query);
                        }
                    }
                    break;
                case "Auto":
                case "Input":
                case "Go":
                    this.Login(true, false, form, lv);
                    this.ProcessEnd();
                    return;
                case "New":
                    var scookies = DataFactory.Instance().Cookies(this.Site.Root, User.Id.Value).OrderBy(r => r.IndexValue).ToList();

                    foreach (var sc in scookies)
                    {
                        if (String.IsNullOrEmpty(sc.Account))
                        {
                            this.SiteCookie = sc;

                            break;
                        }
                    }
                    this.Login(true, false, form, String.Empty);
                    this.ProcessEnd();
                    return;
                default:
                    if (Utility.IntParse(lv, -1) > -1)
                    {
                        if (String.Equals(lv, "0") == false)
                        {
                            this.Context.AddHeader("Set-Cookie", $"{this.Site.Root}-{DeviceIndex}={lv}; HttpOnly;{cookieStr} Path=/");//, DeviceIndex, lv));

                        }
                        else
                        {
                            this.Context.AddHeader("Set-Cookie", $"{this.Site.Root}-{DeviceIndex}=0; Expires={DateTime.Now.AddYears(-10).ToString("r")}; HttpOnly;{cookieStr} Path=/");//, DeviceIndex, DateTime.Now.AddYears(-10).ToString("r")));
                        }

                        this.Context.Redirect("/UMC.Login/Go");
                        return;
                    }
                    break;
            }
            var login = GetConf(String.Format("SITE_MIME_{0}_LOGIN_{1}", Site.Root, lv).ToUpper());
            if (login != null && login.Count > 0)
            {
                this.Password = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(this.Site.Root, this.SiteCookie.user_id.Value, 0));
                var hash = new Hashtable();
                UMC.Data.Utility.AppendDictionary(hash, form);
                var usernmae = hash["Username"] as string;
                hash.Remove("Username");
                this.Context.ContentType = "application/json; charset=utf-8";
                var json = GetConfig(login, this.Match(hash, usernmae ?? this.SiteCookie.Account, this.Password, ""));
                if (String.Equals("[]", json) || String.Equals("{}", json))
                {
                    if (login.Contains("DefautValue"))
                    {
                        var DefautValue = login["DefautValue"] as string;
                        if (String.IsNullOrEmpty(DefautValue) == false)
                        {
                            var sKey = SiteConfig.Config(DefautValue);
                            switch (sKey.Length)
                            {
                                case 0:
                                    break;
                                case 1:
                                    json = UMC.Data.JSON.Serialize(new WebMeta[] { new WebMeta().Put("Text", sKey[0], "Value", sKey[0]) });
                                    break;
                                default:
                                    var ls = new List<WebMeta>();
                                    int l = sKey.Length / 2;
                                    for (var i = 0; i < l; i++)
                                    {
                                        ls.Add(new WebMeta().Put("Text", sKey[0], "Value", sKey[1]));
                                    }
                                    json = UMC.Data.JSON.Serialize(ls);

                                    break;
                            }
                        }
                    }
                }

                this.Context.Output.Write(json);
            }
            else
            {
                var hash = new Hashtable();
                UMC.Data.Utility.AppendDictionary(hash, this.Context.QueryString);
                lv = this.Context.QueryString.Get("$");
                hash.Remove("$");
                if (String.IsNullOrEmpty(lv) == false)
                {
                    this.Isurlencoded = false;
                    this.Context.ContentType = "application/json; charset=utf-8";
                    this.Context.Output.Write(Regex.Replace(lv, this.Match(hash, "", "", "")));

                }
            }
        }
        public void ProcessRequest()
        {

            if (this.StaticModel != 0)
            {
                if (this.Site.Site.UserModel == Entities.UserModel.Bridge)
                {
                    this.AuthBridge();
                }
                else if (this.Context.HttpMethod == "GET")
                {
                    if (Site.Site.AuthType >= WebAuthType.User)
                    {
                        if (String.IsNullOrEmpty(this.SiteCookie.Account))
                        {

                            this.Context.Redirect("/UMC.Login");
                            this.ProcessEnd();
                            return;
                        }

                    }
                    if (Site.Site.AuthType >= WebAuthType.Guest)
                    {
                        if (IsLoginPath(this.Site, this.RawUrl, out var _go))
                        {
                            if (_go)
                            {
                                if (this.Login(false, false, new NameValueCollection(), String.Empty))
                                {
                                    this.ProcessEnd();
                                    return;
                                }
                            }
                            else
                            {
                                var urlReferrer = this.Context.UrlReferrer;
                                if (urlReferrer == null || String.Equals(this.Context.Url.Host, urlReferrer.Host) == false)
                                {
                                    this.Context.Redirect("/UMC.Login");
                                    this.ProcessEnd();
                                }
                                else
                                {
                                    SignOutHtml();
                                    this.ProcessEnd();
                                }
                                return;
                            }

                        }
                    }

                }

            }
            var getUrl = new Uri(Domain, RawUrl);

            switch (Context.HttpMethod)
            {
                default:
                    {

                        var webReq = this.Reqesut(this.Context.Transfer(getUrl, this.Cookies));

                        SiteConfig.ReplaceSetting replaceSetting;
                        if (this.CheckPath(getUrl.AbsolutePath, this.Context.ContentType, out replaceSetting))
                        {
                            _isInputReplaceHost = (replaceSetting.Model & SiteConfig.HostReplaceModel.Input) == SiteConfig.HostReplaceModel.Input;
                        }
                        Context.UseSynchronousIO(this.ProcessEnd);

                        if (_isInputReplaceHost && (this.Context.ContentLength ?? 0) > 0)
                        {
                            this.Context.ReadAsStream(sms =>
                            {
                                var ms = NetClient.TempStream();
                                InputReplaceHost(ms, sms, replaceSetting);
                                ms.Position = 0;
                                webReq.ContentType = this.Context.ContentType;
                                webReq.Net(this.Context.HttpMethod, ms, ms.Length, r =>
                                {
                                    sms.Close();
                                    sms.Dispose();
                                    ms.Close();
                                    ms.Dispose();
                                    ms.Close();
                                    ms.Dispose();
                                    this.Response(r);
                                });

                            }, this.Context.Error);
                        }
                        else
                        {
                            webReq.Net(this.Context, this.Response);

                        }
                    }
                    break;
                case "GET":
                    {
                        if (getUrl.AbsolutePath.EndsWith("/Site.Conf.js"))
                        {
                            var Key = getUrl.AbsolutePath.Substring(0, getUrl.AbsolutePath.LastIndexOf("/")).Trim('/');
                            var mainKey = String.Format("SITE_JS_CONFIG_{0}{1}", this.Site.Root, Key).ToUpper();
                            var config = UMC.Data.DataFactory.Instance().Config(mainKey);
                            this.Context.ContentType = "text/javascript";
                            this.Context.Output.WriteLine();
                            if (config != null)
                            {
                                this.Context.Output.WriteLine(config.ConfValue.Replace("{webr}", this.WebResource + "/" + this.MD5("") + "/"));
                            }
                            this.Context.Output.Flush();
                            this.ProcessEnd();
                            return;
                        }

                        var pmd5Key = this.Site.Site.Version ?? String.Empty;
                        if (this.StaticModel > 0)
                        {
                            switch (this.StaticModel)
                            {
                                case 0:
                                    break;
                                case 1:
                                    break;
                                case 2:
                                    pmd5Key = this.SiteCookie.Account;
                                    break;
                                default:
                                    pmd5Key = String.Format("{0}_{1}", this.SiteCookie.Account, UMC.Data.Utility.TimeSpan() / 60 / this.StaticModel);
                                    break;
                            }
                        }
                        var IsCache = this.StaticModel >= 0 && IsTest == false;

                        string filename = String.Empty;


                        if (IsCache)
                        {
                            if (this.Context.CheckCache(Site.Root, pmd5Key, out filename))
                            {
                                this.ProcessEnd();
                                return;
                            }
                        }

                        this.Context.UseSynchronousIO(this.ProcessEnd);
                        this.Reqesut(Context.Transfer(getUrl, this.Cookies)).Get(httpResponse =>
                        {
                            var statusCode = Convert.ToInt32(httpResponse.StatusCode);

                            this.Context.StatusCode = statusCode;
                            var contentType = httpResponse.ContentType ?? String.Empty;

                            if (String.IsNullOrEmpty(contentType) == false)
                            {
                                this.Context.ContentType = contentType;
                            }
                            else if (httpResponse.StatusCode == HttpStatusCode.OK)
                            {
                                this.Context.ContentType = Site.ContentType;
                            }

                            var ContentType = contentType.Split(';')[0];
                            String jsAppendKey = null;
                            SiteConfig.ReplaceSetting replaceSetting = null;
                            WebMeta ImageConfig = null;
                            int model = 0;
                            if (String.Equals(ContentType, "text/html", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (CheckPath(getUrl.AbsolutePath, out var _, this.Site.AppendJSConf))
                                {
                                    model = 1;
                                }
                                else if (this.CheckPath(getUrl.AbsolutePath, ContentType, out replaceSetting))
                                {
                                    if (replaceSetting.Model != SiteConfig.HostReplaceModel.Input)
                                    {
                                        model = 1;
                                    }
                                }

                            }
                            else if (CheckPath(getUrl.AbsolutePath, out jsAppendKey, this.Site.AppendJSConf))
                            {
                                model = 2;
                            }
                            else if (this.CheckPath(getUrl.AbsolutePath, ContentType, out replaceSetting))
                            {
                                if ((replaceSetting.Model & SiteConfig.HostReplaceModel.Remove) == SiteConfig.HostReplaceModel.Remove ||
                                     (replaceSetting.Model & SiteConfig.HostReplaceModel.Replace) == SiteConfig.HostReplaceModel.Replace)
                                {
                                    model = 3;
                                }
                            }
                            else if (String.Equals(ContentType, "text/css", StringComparison.CurrentCultureIgnoreCase) && IsCDN)
                            {
                                model = 4;
                            }
                            else if (ContentType.StartsWith("image/") && ContentType.Contains("svg") == false)
                            {
                                var ckey = Context.QueryString.Get("umc-image");
                                if (String.IsNullOrEmpty(ckey))
                                {
                                    CheckPath(getUrl.AbsolutePath, ContentType, out ckey, this.Site.ImagesConf);
                                }
                                if (TryImageConfig(this.Site.Root, ckey, out ImageConfig))
                                {
                                    var format = ImageConfig["Format"] ?? "Src";
                                    if (String.Equals(format, "Src") == false)
                                    {
                                        ContentType = "image/" + format;
                                    }
                                    model = 5;
                                }
                            }

                            if (httpResponse.StatusCode == HttpStatusCode.OK && IsCache)
                            {
                                switch (model)
                                {
                                    case 1:
                                    case 2:
                                    case 3:
                                    case 4:
                                    case 5:
                                        {
                                            var tempFile = System.IO.Path.GetTempFileName();
                                            var etag = Utility.TimeSpan();
                                            var cacheStream = NetClient.MimeStream(tempFile, ContentType, etag);
                                            Header(httpResponse, false, false);

                                            httpResponse.ReadAsStream(content =>
                                            {
                                                content.Position = 0;

                                                switch (model)
                                                {
                                                    case 1:
                                                        this.OuterHTML(content, httpResponse.ContentEncoding, cacheStream);
                                                        break;
                                                    case 2:
                                                        this.OutputAppendJS(content, httpResponse.ContentEncoding, MD5(jsAppendKey, ""), cacheStream);
                                                        break;
                                                    case 3:
                                                    case 4:
                                                        this.OuterReplaceHost(content, httpResponse.ContentEncoding, replaceSetting, cacheStream);
                                                        break;
                                                    case 5:
                                                        SiteImage.Convert(content, cacheStream, ImageConfig, filename);
                                                        break;
                                                }
                                                content.Dispose();
                                                cacheStream.Flush();
                                                cacheStream.Close();
                                                UMC.Data.Utility.Move(tempFile, filename);
                                                using (var fileStream = System.IO.File.OpenRead(filename))
                                                {
                                                    this.Context.OutputCache(cacheStream);
                                                }
                                                this.Context.OutputFinish();



                                            }, e =>
                                            {
                                                cacheStream.Close();
                                                DeleteCache(tempFile);
                                                this.Context.Error(e);
                                            });
                                        }
                                        break;
                                    default:
                                        {
                                            var tempFile = File.Open(System.IO.Path.GetTempFileName(), FileMode.Create);
                                            Header(httpResponse, true, false);
                                            var tag = Utility.TimeSpan();
                                            this.Context.AddHeader("ETag", tag.ToString());

                                            if (httpResponse.ContentLength > -1)
                                            {
                                                this.Context.ContentLength = httpResponse.ContentLength;
                                            }
                                            httpResponse.ReadAsData((b, i, c) =>
                                            {
                                                if (c == 0 && b.Length == 0)
                                                {

                                                    if (i == -1)
                                                    {
                                                        tempFile.Close();
                                                        this.Context.Error(httpResponse.Error);

                                                    }
                                                    else
                                                    {
                                                        this.Context.OutputFinish();
                                                        tempFile.Flush();

                                                        tempFile.Position = 0;
                                                        using (var tem = DataFactory.Instance().Decompress(tempFile, httpResponse.ContentEncoding))
                                                        {
                                                            var cacheStream = NetClient.MimeStream(filename, ContentType, tag);

                                                            tem.CopyTo(cacheStream);
                                                            tempFile.Close();
                                                            cacheStream.Close();
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    tempFile.Write(b, i, c);
                                                    this.Context.OutputStream.Write(b, i, c);
                                                }
                                            });
                                        }
                                        break;
                                }
                                return;

                            }
                            else
                            {
                                switch (model)
                                {
                                    case 1:
                                    case 2:
                                    case 3:
                                    case 4:
                                    case 5:

                                        Header(httpResponse, false, false);
                                        httpResponse.ReadAsStream(content =>
                                        {
                                            content.Position = 0;

                                            switch (model)
                                            {
                                                case 1:
                                                    this.OuterHTML(content, httpResponse.ContentEncoding, this.Context.OutputStream);
                                                    break;
                                                case 2:
                                                    this.OutputAppendJS(content, httpResponse.ContentEncoding, MD5(jsAppendKey, ""), this.Context.OutputStream);
                                                    break;
                                                case 3:
                                                case 4:
                                                    this.OuterReplaceHost(content, httpResponse.ContentEncoding, replaceSetting, this.Context.OutputStream);
                                                    break;
                                                case 5:

                                                    if (ContentType.Contains("Optimal"))
                                                    {
                                                        this.Context.ContentType = "image/webp";
                                                    }
                                                    SiteImage.Convert(content, this.Context.OutputStream, ImageConfig, String.Empty);
                                                    break;
                                            }
                                            this.Context.OutputFinish();

                                        }, this.Context.Error);

                                        return;
                                    default:
                                        Header(httpResponse);
                                        break;

                                }
                            }

                            if (httpResponse.ContentLength > -1)
                            {
                                this.Context.ContentLength = httpResponse.ContentLength;
                            }
                            httpResponse.ReadAsData((b, i, c) =>
                            {
                                if (c == 0 && b.Length == 0)
                                {
                                    if (i == -1)
                                    {
                                        this.Context.Error(httpResponse.Error);
                                    }
                                    else
                                    {
                                        this.Context.OutputFinish();
                                    }
                                }
                                else
                                {
                                    this.Context.OutputStream.Write(b, i, c);
                                }
                            });
                        });
                    }
                    break;
            }
        }
        bool CheckBrowser()
        {
            var uB = Site.Site.UserBrowser ?? Entities.UserBrowser.All;
            var cilentB = Entities.UserBrowser.All;
            var us = this.Context.UserAgent;
            if (uB == Entities.UserBrowser.All)
            {
                return true;

            }
            else if (String.IsNullOrEmpty(us) == false)
            {
                us = us.ToUpper();
                if (us.Contains("CHROME"))
                {
                    cilentB = Entities.UserBrowser.Chrome;
                }
                else if (us.Contains("FIREFOX"))
                {
                    cilentB = Entities.UserBrowser.Firefox;
                }
                else if (us.Contains("MSIE"))
                {
                    cilentB = Entities.UserBrowser.IE;
                }
                else if (us.Contains("DINGTALK"))
                {
                    cilentB = Entities.UserBrowser.Dingtalk;
                }
                else if (us.Contains("WXWORK") || us.Contains("MICROMESSENGER"))
                {
                    cilentB = Entities.UserBrowser.WeiXin;
                }
                else if (us.Contains("WEBKIT"))
                {
                    cilentB = Entities.UserBrowser.WebKit;
                }
                if ((uB & cilentB) != cilentB)
                {
                    var ts = UMC.Data.Utility.Enum(uB);

                    var sb = new List<String>();
                    foreach (var k in ts)
                    {
                        switch (k)
                        {
                            case Entities.UserBrowser.Chrome:
                                sb.Add("谷歌");
                                break;
                            case Entities.UserBrowser.IE:
                                sb.Add("IE");
                                break;
                            case Entities.UserBrowser.Firefox:
                                sb.Add("火狐");
                                break;
                            case Entities.UserBrowser.WebKit:
                                sb.Add("WebKit");
                                break;
                            case Entities.UserBrowser.Dingtalk:
                                sb.Add("钉钉");
                                break;
                            case Entities.UserBrowser.WeiXin:
                                sb.Add("微信");
                                break;
                        }
                    }

                    this.Context.AddHeader("Cache-Control", "no-store");
                    this.Context.ContentType = "text/html; charset=UTF-8";
                    using (System.IO.Stream stream = typeof(HttpProxy).Assembly
                                                          .GetManifestResourceStream("UMC.Proxy.Resources.check.html"))
                    {

                        var str = new System.IO.StreamReader(stream).ReadToEnd();
                        this.Context.Output.Write(new System.Text.RegularExpressions.Regex("\\{(?<key>\\w+)\\}").Replace(str, g =>
                        {
                            var key = g.Groups["key"].Value.ToLower();
                            switch (key)
                            {
                                case "authurl":
                                    return new Uri(this.Context.Url, String.Format("/!/{0}/UMC.Login", Utility.MD5(this.Context.Token.Device.Value))).AbsoluteUri;

                                case "authkey":
                                    return "/UMC?_mode=Proxy&_cmd=Auth";

                                case "isie":
                                    return uB == Entities.UserBrowser.IE ? "yes" : "no";

                                case "desc":
                                    return String.Format("{0}只支持在{1}中使用", Site.Caption, String.Join(",", sb.ToArray()));
                            }
                            return "";

                        }));

                    }




                    return false;

                }
            }


            return true;

        }
        static bool IsLoginPath(SiteConfig config, String rawUrl, out bool isGo)
        {
            isGo = false;
            for (int i = 0, l = config.LogoutPath.Length; i < l; i++)
            {
                var path = config.LogoutPath[i];
                if (path.StartsWith('@'))
                {
                    isGo = true;
                    path = path.Substring(1);
                }
                else
                {
                    isGo = false;
                }
                if (path.EndsWith("$"))
                {
                    if ((rawUrl + "$").EndsWith(path, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                }
                else if (path.StartsWith("/"))
                {
                    if (String.Equals(rawUrl, path, StringComparison.CurrentCultureIgnoreCase) || rawUrl.StartsWith(path + "?", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }

                }
            }
            return false;
        }
        public static bool IsLoginPath(SiteConfig config, String rawUrl)
        {
            return IsLoginPath(config, rawUrl, out var _);
        }
        public static void DeleteCache(String cacheKey)
        {
            File.Delete(cacheKey);
        }
        public HttpWebRequest Reqesut(HttpWebRequest webr)
        {
            if (this.StaticModel != 0)
            {
                WebServlet.WebHeaderConf(webr, this.Site, this.Context, this.SiteCookie.Account);
            }
            if (String.IsNullOrEmpty(this.Host) == false)
            {
                var h = this.Host;
                webr.Headers[HttpRequestHeader.Host] = h;
                if (String.Equals(h, this.Context.Url.Authority) == false)
                {
                    var Referer = webr.Headers[HttpRequestHeader.Referer];
                    if (String.IsNullOrEmpty(Referer) == false)
                    {
                        webr.Headers[HttpRequestHeader.Referer] = String.Format("{0}://{1}{2}", this.Domain.Scheme, h, Referer.Substring(Referer.IndexOf('/', 8)));

                    }
                    var Origin = webr.Headers["Origin"];
                    if (String.IsNullOrEmpty(Origin) == false)
                    {
                        webr.Headers["Origin"] = String.Format("{0}://{1}/", this.Domain.Scheme, h);
                    }
                }

            }
            webr.Timeout = (this.Site.Site.Timeout ?? 100) * 1000;
            return webr;
        }

        bool CheckPath(String path, String ctype, out SiteConfig.ReplaceSetting replaceSetting)
        {
            replaceSetting = null;
            if (String.IsNullOrEmpty(ctype) == false && this.Site.HostPage.ContainsKey(ctype))
            {
                replaceSetting = this.Site.HostPage[ctype];
            }
            var mv = this.Site.HostPage.GetEnumerator();
            while (mv.MoveNext())
            {
                var d = mv.Current.Key;
                int splitIndex = d.IndexOf('*');
                bool isOk;
                switch (splitIndex)
                {
                    case -1:
                        isOk = String.Equals(path, d, StringComparison.CurrentCultureIgnoreCase);
                        break;
                    case 0:
                        isOk = path.EndsWith(d.Substring(1), StringComparison.CurrentCultureIgnoreCase);
                        break;
                    default:
                        if (splitIndex == d.Length - 1)
                        {
                            isOk = path.StartsWith(d.Substring(0, d.Length - 1), StringComparison.CurrentCultureIgnoreCase);
                        }
                        else
                        {
                            isOk = path.StartsWith(d.Substring(0, splitIndex), StringComparison.CurrentCultureIgnoreCase) && path.EndsWith(d.Substring(splitIndex + 1), StringComparison.CurrentCultureIgnoreCase);
                        }

                        break;

                }
                if (isOk)
                {
                    if (replaceSetting != null)
                    {
                        var setting = new SiteConfig.ReplaceSetting() { Hosts = new Dictionary<string, Uri>(replaceSetting.Hosts) };

                        setting.Model = replaceSetting.Model | mv.Current.Value.Model;

                        var mm = mv.Current.Value.Hosts.GetEnumerator();
                        while (mm.MoveNext())
                        {
                            setting.Hosts[mm.Current.Key] = mm.Current.Value;
                        }
                        replaceSetting = setting;
                    }
                    else
                    {
                        replaceSetting = mv.Current.Value;
                    }
                    return true;
                }

            }
            return replaceSetting != null;
        }
        internal static bool CheckPath(String path, String ctype, out String key, String[] cfs)
        {

            if (CheckPath(path, out key, cfs) == false)
            {
                if (cfs.Contains(ctype))
                {
                    key = ctype;
                    return true;
                }
                return false;
            }
            return true;
        }
        internal static bool CheckPath(String path, out String key, String[] cfs)
        {

            key = null;
            foreach (String d in cfs)
            {
                int splitIndex = d.IndexOf('*');
                switch (splitIndex)
                {
                    case -1:
                        if (String.Equals(path, d, StringComparison.CurrentCultureIgnoreCase))
                        {
                            key = d;
                            return true;
                        }
                        break;
                    case 0:
                        if (path.EndsWith(d.Substring(1), StringComparison.CurrentCultureIgnoreCase))
                        {
                            key = d;
                            return true;
                        }
                        break;
                    default:
                        if (path.Length > splitIndex)
                        {
                            if (splitIndex == d.Length - 1)
                            {
                                if (path.StartsWith(d.Substring(0, d.Length - 1), StringComparison.CurrentCultureIgnoreCase))
                                {

                                    key = d;
                                    return true;
                                }
                            }
                            else if (path.StartsWith(path.Substring(0, splitIndex)) && path.EndsWith(d.Substring(splitIndex + 1)))
                            {
                                key = d;
                                return true;
                            }
                        }
                        break;

                }
            }
            return false;
        }
        public string MD5(String src)
        {
            return MD5(src, this.Site.Site.Version);
        }
        public static string MD5(String src, String cap)
        {
            var md5 = System.Security.Cryptography.MD5.Create();
            byte[] md = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(src + cap));

            return UMC.Data.Utility.Parse36Encode(UMC.Data.Utility.IntParse(md));
        }
        public static long Int64MD5(String src)
        {
            var md5 = System.Security.Cryptography.MD5.Create();
            byte[] md = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(src));

            var b = new byte[8];
            for (var i = 0; i < 8; i++)
            {
                b[i] = md[i * 2 + 1];
            }
            return BitConverter.ToInt64(b, 0);
        }


        void OutputAppendJS(Stream response, string encoding, String key, Stream output)
        {
            var webResource = this.WebResource;

            var mainKey = String.Format("SITE_JS_CONFIG_{0}{1}", this.Site.Root, key).ToUpper();
            var config = UMC.Data.DataFactory.Instance().Config(mainKey);

            if (config == null)
            {
                response.CopyTo(output);
                return;
            }
            var reader = new System.IO.StreamReader(DataFactory.Instance().Decompress(response, encoding));
            var writer = new System.IO.StreamWriter(output);

            int row = -1;
            var isEnd = false;
            if (config != null && String.IsNullOrEmpty(config.ConfValue) == false)
            {
                if (config.ConfValue.Trim().StartsWith(":"))
                {
                    var s = config.ConfValue.IndexOf('\n');
                    var su = config.ConfValue.Substring(0, s).Trim().Trim(':');
                    if (su.EndsWith("$"))
                    {

                        isEnd = true;
                    }
                    row = UMC.Data.Utility.IntParse(su.Trim('$'), -1);
                    config.ConfValue = config.ConfValue.Substring(s);
                }
            }
            var bf = new Char[1];
            var is_tr = false;
            int index = 1;
            if (row == 0)
            {
                is_tr = true;
                writer.WriteLine(config.ConfValue.Replace("{webr}", webResource + "/" + this.MD5("") + "/")); ;
                if (isEnd)
                {
                    writer.Flush();
                    return;
                }
            }

            while (reader.ReadBlock(bf, 0, 1) > 0)
            {
                writer.Write(bf[0]);
                if (bf[0] == '\n')
                {
                    if (index == row)
                    {
                        is_tr = true;
                        writer.WriteLine(config.ConfValue.Replace("{webr}", webResource + "/" + this.MD5("") + "/")); ;
                        if (isEnd)
                        {
                            break;
                        }
                    }
                    index++;
                }
            }
            if (is_tr == false)
            {
                writer.WriteLine(config.ConfValue.Replace("{webr}", webResource + "/" + this.MD5("") + "/")); ;
            }

            writer.Flush();

        }
        bool _isInputReplaceHost;
        String ReplaceRawUrl(String rawUrl)
        {

            var sb = new System.Text.StringBuilder();
            char last = char.MinValue;

            var l = rawUrl.Length;
            for (var i = 0; i < l; i++)
            {
                var c = rawUrl[i];
                switch (c)
                {
                    case '?':

                        sb.Append(c);
                        if (i + 1 < l)
                        {
                            SiteConfig.ReplaceSetting replaceSetting;
                            if (this.CheckPath(sb.ToString(0, sb.Length - 1), String.Empty, out replaceSetting))
                            {
                                if ((replaceSetting.Model & SiteConfig.HostReplaceModel.Input) == SiteConfig.HostReplaceModel.Input)
                                {
                                    _isInputReplaceHost = true;
                                    var writer = new System.IO.MemoryStream();
                                    var reader = new System.IO.MemoryStream(UTF8Encoding.UTF8.GetBytes(rawUrl.Substring(i + 1)));
                                    try
                                    {
                                        InputReplaceHost(writer, reader, replaceSetting);
                                        sb.Append(UTF8Encoding.UTF8.GetString(writer.ToArray()));
                                    }
                                    finally
                                    {
                                        writer.Close();
                                        reader.Close();
                                    }
                                }
                                else
                                {
                                    sb.Append(rawUrl.Substring(i + 1));

                                }
                            }
                            else
                            {

                                sb.Append(rawUrl.Substring(i + 1));
                            }

                        }
                        return sb.ToString();


                    case '/':

                        if (last != c)
                        {
                            sb.Append(c);
                            last = c;
                        }
                        break;
                    default:

                        sb.Append(c);
                        last = c;
                        break;
                }
            }
            return sb.ToString();

        }
        void InputReplaceHost(System.IO.Stream writer, System.IO.Stream reader, SiteConfig.ReplaceSetting rpsetting)
        {
            var host = this.Context.Url.Host;
            var scheme = String.Format("{0}://", this.Context.Url.Scheme);
            var dScheme = Uri.EscapeDataString(scheme);

            var host2 = this.Domain.Authority;
            if (String.IsNullOrEmpty(Site.Site.Host) == false && String.Equals(Site.Site.Host, "*") == false)
            {
                if (host2.IndexOf(':') > -1)
                {
                    host2 = String.Format("{0}:{1}", Site.Site.Host, this.Domain.Port);
                }
                else
                {
                    host2 = Site.Site.Host;
                }
            }


            var hash = new List<Tuple<String, String>>();
            hash.Add(new Tuple<string, string>(host, host2));

            if (rpsetting.Hosts.Count > 0)
            {
                var hem = rpsetting.Hosts.GetEnumerator();
                while (hem.MoveNext())
                {
                    hash.Add(new Tuple<string, string>(hem.Current.Key, hem.Current.Value.Authority));
                }
            }
            ReplaceHost(writer, reader, hash, (x, y) => false);


        }

        void ReplaceHost(System.IO.Stream writer, System.IO.Stream reader, List<Tuple<String, String>> shosts, Func<Stream, List<byte>, bool> func)
        {
            var hosts = new List<Tuple<byte[], byte[], byte[]>>();
            var hLength = 0;
            foreach (var em in shosts)
            {
                hosts.Add(new Tuple<byte[], byte[], byte[]>(UTF8Encoding.ASCII.GetBytes(em.Item1), UTF8Encoding.ASCII.GetBytes(em.Item2), UTF8Encoding.ASCII.GetBytes(Uri.EscapeDataString(em.Item2))));
                var Ht = em.Item1;
                if (hLength < Ht.Length)
                {
                    hLength = Ht.Length;
                }
            }

            var strHttps = UTF8Encoding.ASCII.GetBytes("https://");
            var strHttp = UTF8Encoding.ASCII.GetBytes("http://");
            var nowScheme = UTF8Encoding.ASCII.GetBytes(String.Format("{0}://", this.Context.Url.Scheme));
            var nowSchemeEncode = UTF8Encoding.ASCII.GetBytes(Uri.EscapeDataString(String.Format("{0}://", this.Context.Url.Scheme)));
            var httpsEncode = UTF8Encoding.ASCII.GetBytes(Uri.EscapeDataString("https://"));
            var httpEncode = UTF8Encoding.ASCII.GetBytes(Uri.EscapeDataString("http://"));
            var strEncodePort = UTF8Encoding.ASCII.GetBytes("%3A");

            var bsize = 14 + hLength;

            var bf = new byte[1];
            var isFind = false;
            var isPort = false;
            var isEncodePort = false;
            var buffer = new List<byte>();

            while (reader.Read(bf, 0, 1) > 0)
            {
                switch ((char)bf[0])
                {
                    case ' ':
                    case '\r':
                    case '\t':
                    case '\n':
                        isPort = false;
                        writer.Write(buffer.ToArray(), 0, buffer.Count);
                        writer.WriteByte(bf[0]);
                        buffer.Clear();
                        isFind = false;
                        continue;
                    case ':':
                        if (isFind)
                        {
                            isPort = true;
                        }
                        else
                        {
                            if (buffer.Count == bsize)
                            {
                                writer.WriteByte(buffer[0]);
                                buffer.RemoveAt(0);
                            }

                            buffer.Add(bf[0]);

                        }
                        continue;
                    default:
                        if (isPort)
                        {
                            if (bf[0] > 47 && bf[0] < 58)
                            {
                                continue;
                            }
                            else
                            {
                                isPort = false;
                            }
                        }
                        else if (isFind && bf[0] == '%')
                        {
                            isEncodePort = true;
                        }
                        if (buffer.Count == bsize)
                        {
                            writer.WriteByte(buffer[0]);
                            buffer.RemoveAt(0);
                        }

                        buffer.Add(bf[0]);
                        if (isEncodePort && buffer.Count == 3)
                        {
                            isEncodePort = false;
                            if (EndsWith(buffer, strEncodePort))
                            {
                                isPort = true;

                                buffer.Clear();
                                continue;
                            }
                        }
                        if (func(writer, buffer))
                        {
                            continue;
                        }

                        break;

                }
                isFind = false;

                foreach (var hem in hosts)
                {
                    var khost = hem.Item1;
                    if (EndsWith(buffer, khost))
                    {
                        isFind = true;
                        buffer.RemoveRange(buffer.Count - khost.Length, khost.Length);
                        var isEncode = false;
                        if (EndsWith(buffer, strHttps))
                        {

                            buffer.RemoveRange(buffer.Count - strHttps.Length, strHttps.Length);

                            writer.Write(buffer.ToArray());
                            writer.Write(nowScheme);
                        }
                        else if (EndsWith(buffer, strHttp))
                        {

                            buffer.RemoveRange(buffer.Count - strHttp.Length, strHttp.Length);

                            writer.Write(buffer.ToArray());
                            writer.Write(nowScheme);
                        }
                        else if (EndsWith(buffer, httpsEncode))
                        {
                            isEncode = true;
                            buffer.RemoveRange(buffer.Count - httpsEncode.Length, httpsEncode.Length);

                            writer.Write(buffer.ToArray());
                            if (hem.Item2.Length > 0)
                                writer.Write(nowSchemeEncode);
                        }
                        else if (EndsWith(buffer, httpEncode))
                        {
                            isEncode = true;
                            buffer.RemoveRange(buffer.Count - httpEncode.Length, httpEncode.Length);

                            writer.Write(buffer.ToArray());
                            if (hem.Item2.Length > 0)
                                writer.Write(nowSchemeEncode);
                        }
                        else
                        {
                            writer.Write(buffer.ToArray());
                        }
                        if (isEncode)
                        {
                            writer.Write(hem.Item3);
                        }
                        else
                        {
                            writer.Write(hem.Item2);
                        }
                        buffer.Clear();
                        break;
                    }
                }
            }
            if (buffer.Count > 0)
            {
                writer.Write(buffer.ToArray());
            }
            writer.Flush();

        }

        String ReplaceRedirect(String redirect)
        {

            SiteConfig.ReplaceSetting rpsetting;
            if (this.Site.HostPage.TryGetValue("Redirect", out rpsetting) == false)
            {
                rpsetting = new SiteConfig.ReplaceSetting() { Model = SiteConfig.HostReplaceModel.Replace };
            }

            var writer = new System.IO.MemoryStream();
            var reader = new System.IO.MemoryStream(UTF8Encoding.UTF8.GetBytes(redirect));
            try
            {
                OuterReplaceHost(writer, String.Empty, rpsetting, reader);
                return UTF8Encoding.UTF8.GetString(writer.ToArray());
            }
            finally
            {
                writer.Close();
                reader.Close();
            }

        }


        void OuterReplaceHost(Stream response, string encoding, SiteConfig.ReplaceSetting rpsetting, Stream output)
        {
            if (rpsetting == null)
            {
                response.CopyTo(output);
                return;
            }
            var host = this.Domain.Host;
            var host2 = this.Context.Url.Authority;
            var rp = rpsetting.Model;
            if ((rp & SiteConfig.HostReplaceModel.Remove) == SiteConfig.HostReplaceModel.Remove)
            {
                host2 = "";

            }
            else if ((rp & SiteConfig.HostReplaceModel.Replace) != SiteConfig.HostReplaceModel.Replace)
            {
                response.CopyTo(output);
                return;
            }
            var hosts = new List<Tuple<String, String>>();
            hosts.Add(new Tuple<String, String>(host, host2));

            hosts.Add(new Tuple<String, String>(this.Context.Url.Host, host2));

            if (String.IsNullOrEmpty(this.Host) == false && String.Equals(Site.Site.Host, "*") == false)
            {
                var hStr = Site.Site.Host;
                if (String.IsNullOrEmpty(hStr) == false)
                {
                    hosts.Add(new Tuple<String, String>(hStr, host2));
                }
            }
            if (rpsetting.Hosts.Count > 0)
            {
                var em = rpsetting.Hosts.GetEnumerator();
                while (em.MoveNext())
                {
                    hosts.Add(new Tuple<String, String>(em.Current.Value.Host, em.Current.Key));
                }

            }


            var reader = DataFactory.Instance().Decompress(response, encoding);

            ReplaceHost(output, reader, hosts, (x, y) => false);
        }

        public string WebResource => $"/UMC.CDN/{this.Site.Root}";

        void OuterHTML(Stream response, string encoding, Stream output)
        {

            var pathKey = this.RawUrl.Split('?')[0];
            String jsKey;
            var isAppendJS = CheckPath(pathKey, out jsKey, this.Site.AppendJSConf);

            SiteConfig.HostReplaceModel hostRpMode = SiteConfig.HostReplaceModel.Input;
            SiteConfig.ReplaceSetting replaceSetting;
            if (this.CheckPath(pathKey, "text/html", out replaceSetting))
            {
                hostRpMode = replaceSetting.Model;

            }
            if (isAppendJS == false && hostRpMode == SiteConfig.HostReplaceModel.Input)
            {
                response.CopyTo(output);
                return;
            }
            var host = this.Domain.Host;
            var host2 = this.Context.Url.Host;
            switch (hostRpMode)
            {
                case SiteConfig.HostReplaceModel.Remove:
                    host2 = String.Empty;
                    break;
            }


            var hosts = new List<Tuple<String, String>>();
            hosts.Add(new Tuple<string, string>(host, host2));


            if (String.IsNullOrEmpty(this.Host) == false)
            {
                hosts.Add(new Tuple<string, string>(Site.Site.Host, host2));

            }
            if (replaceSetting != null)
            {
                if (replaceSetting.Hosts.Count > 0)
                {
                    var em = replaceSetting.Hosts.GetEnumerator();
                    while (em.MoveNext())
                    {

                        hosts.Add(new Tuple<string, string>(em.Current.Value.Host, em.Current.Key));
                    }

                }
            }

            if (isAppendJS && String.IsNullOrEmpty(jsKey) == false)
            {
                var isHead = true;
                var headEnd = UTF8Encoding.ASCII.GetBytes("</head>");
                ReplaceHost(output, DataFactory.Instance().Decompress(response, encoding), hosts, (w, b) =>
                {

                    if (isHead && EndsWith(b, headEnd))
                    {
                        isHead = false;
                        b.RemoveRange(b.Count - headEnd.Length, headEnd.Length);

                        w.Write(b.ToArray());
                        w.Write(UTF8Encoding.ASCII.GetBytes(String.Format("<script src=\"{0}/{1}/{2}/Site.Conf.js\"></script>\r\n</head>", this.WebResource, this.MD5(""), MD5(jsKey, ""))));

                        b.Clear();
                        return true;
                    }
                    return false;
                });


            }
            else
            {
                ReplaceHost(output, DataFactory.Instance().Decompress(response, encoding), hosts, (x, r) => false);
            }
        }
        static bool EndsWith(List<byte> buffer, byte[] end)
        {
            var el = end.Length;
            var bl = buffer.Count;
            if (el <= bl)
            {
                for (var i = 0; i < el; i++)
                {
                    if (end[el - 1 - i] != buffer[bl - 1 - i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
        public static bool TryImageConf(string confKey, out WebMeta confValue)
        {
            confValue = null;
            if (String.IsNullOrEmpty(confKey) == false)
            {

                var match = System.Text.RegularExpressions.Regex.Match(confKey, "^[h|w|c|t|m|b](\\d+)([-|x]\\d+)?([g|p|j|w|a|o]?)$");

                if (match.Success)
                {
                    confValue = new WebMeta();
                    switch (match.Groups[3].Value)
                    {
                        case "g":
                            confValue.Put("Format", "gif");
                            break;
                        case "j":
                            confValue.Put("Format", "jpeg");
                            break;
                        case "w":
                            confValue.Put("Format", "webp");
                            break;
                        case "p":
                            confValue.Put("Format", "png");
                            break;
                        case "a":
                            confValue.Put("Format", "avif");
                            break;
                        case "o":
                            confValue.Put("Format", "Optimal");
                            break;

                    }
                    confValue.Put("Width", match.Groups[1].Value);
                    if (match.Groups[2].Length > 0)
                    {
                        switch (match.Groups[0].Value[0])
                        {
                            case '-':
                                confValue.Put("Width", "-" + match.Groups[1].Value);
                                confValue.Put("Height", "-" + match.Groups[0].Value);
                                break;
                            default:
                                confValue.Put("Height", match.Groups[2].Value.Substring(1));
                                break;
                        }
                    }
                    else
                    {

                        confValue.Put("Height", match.Groups[1].Value);
                    }
                    switch (confKey[0])
                    {
                        case 'w':
                            confValue.Remove("Height");
                            break;
                        case 'h':
                            confValue.Remove("Width");
                            break;
                        case 'c':
                            confValue["Model"] = "0";
                            break;
                        case 't':
                            confValue["Model"] = "1";
                            break;
                        case 'm':
                            confValue["Model"] = "2";
                            break;
                        case 'b':
                            confValue["Model"] = "3";
                            break;
                    }
                    return true;
                }
                else
                {
                    switch (confKey)
                    {
                        case "g":
                            confValue = new WebMeta().Put("Format", "gif");
                            return true;
                        case "j":
                            confValue = new WebMeta().Put("Format", "jpeg");
                            return true;
                        case "w":
                            confValue = new WebMeta().Put("Format", "webp");
                            return true;
                        case "p":
                            confValue = new WebMeta().Put("Format", "png");
                            return true;

                    }
                }
            }
            return false;

        }

        public static bool TryImageConfig(string rook, string confKey, out WebMeta confValue)
        {
            confValue = null;
            if (String.IsNullOrEmpty(confKey) == false)
            {

                if (TryImageConf(confKey, out confValue))
                {
                    return true;
                }
                var mainKey = String.Format("SITE_IMAGE_CONFIG_{0}{1}", rook, MD5(confKey, "")).ToUpper();
                var config = UMC.Data.DataFactory.Instance().Config(mainKey);
                if (config != null)
                {
                    confValue = UMC.Data.JSON.Deserialize<WebMeta>(config.ConfValue);
                    if (confValue != null)
                    {
                        return true;
                    }
                }
            }
            return false;

        }
    }
}
