using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UMC.Proxy
{
    public class SiteConfig
    {
        public enum HostReplaceModel
        {
            Replace = 1,
            Remove = 2,
            Input = 4,
            CDN = 8,
            Script = 16
        }
        public class ReplaceSetting
        {
            public HostReplaceModel Model
            {
                get;
                set;
            }
            public System.Collections.Generic.Dictionary<String, Uri> Hosts
            {
                get;
                set;
            }
        }
        public class LogSetting
        {
            public String[] Cookies
            {
                get;
                set;
            }
            public String[] Headers
            {
                get;
                set;
            }
            public string[] ResHeaders
            {
                get;
                set;
            }
        }
        public class TestUrl
        {
            public String[] Users
            {
                get;
                set;
            }
            public String[] Auths
            {
                get;
                set;
            }
            public string Url
            {

                get;
                set;
            }
        }
        public class KeyValue
        {

            public string Key
            {

                get;
                set;
            }
            public string Value
            {

                get;
                set;
            }
            public bool IsDel
            {
                get; set;
            }
        }

        public int WeightTotal
        {
            get;
            private set;
        }
        public int[] Weights
        {
            get;
            private set;
        }
        public string Caption
        {
            get; private set;
        }
        public bool IsFile
        {

            get; private set;
        }

        public SiteConfig() { }
        public SiteConfig(Entities.Site site)
        {
            this.Caption = site.Caption;
            var vindex = this.Caption.IndexOf("v.", StringComparison.CurrentCultureIgnoreCase);
            if (vindex > -1)
            {
                this.Caption = this.Caption.Substring(0, vindex);
            }
            if (String.IsNullOrEmpty(site.Home) == false && (site.Home.StartsWith("https:") || site.Home.StartsWith("http:")))
            {
                this.Home = new Uri(site.Home).PathAndQuery;
            }
            else
            {
                this.Home = site.Home;
            }

            this.Root = site.Root;
            this.Site = site;

            var dom = site.Domain.Split(',', '\n');
            List<string> domains = new List<string>();
            var ls = new List<int>();
            var total = 0;
            var test = new Dictionary<String, TestUrl>();
            for (var i = 0; i < dom.Length; i++)
            {
                var v = dom[i].Trim();
                if (String.IsNullOrEmpty(v) == false)
                {
                    if (v.EndsWith("]"))
                    {
                        var vin = v.LastIndexOf("[");
                        var tUrl = v;
                        if (vin > -1)
                        {
                            ls.Add(UMC.Data.Utility.IntParse(v.Substring(vin + 1).Trim(']', '[').Trim(), 1));

                            total += ls[ls.Count - 1];
                            tUrl = v.Substring(0, vin).TrimEnd(']', ' ').Trim();
                        }
                        else
                        {
                            total++;
                            ls.Add(1);
                        }

                        var sIndex = tUrl.LastIndexOf('/');
                        if (sIndex > 0)
                        {
                            tUrl = tUrl.Substring(0, sIndex);
                        }
                        domains.Add(tUrl);
                    }
                    else
                    {
                        var tIndex = v.IndexOf('@');
                        var tUrl = v;
                        if (tIndex > 0)
                        {
                            tUrl = v.Substring(0, tIndex);
                            var uvs = v.Substring(tIndex + 1).Split(',', ' ');
                            var tUsers = new List<String>();
                            var tAuth = new List<String>();
                            foreach (var uv in uvs)
                            {
                                var uname = uv.Trim();
                                if (String.IsNullOrEmpty(uname) == false)
                                {
                                    if (uname.IndexOf('/') == -1)
                                    {
                                        tUsers.Add(uname);
                                    }
                                    else
                                    {
                                        tAuth.Add(uname);
                                    }
                                }
                            }
                            if (tUsers.Count > 0 || tAuth.Count > 0)
                            {
                                var sIndex = tUrl.LastIndexOf('/');//, tIndex);
                                if (sIndex > 0)
                                {
                                    tUrl = tUrl.Substring(0, sIndex);
                                }
                                test[tUrl.Trim()] = new TestUrl { Auths = tAuth.ToArray(), Users = tUsers.ToArray(), Url = tUrl };
                            }
                        }
                        else
                        {
                            if (tUrl.StartsWith("file://") == false)
                            {
                                var sIndex = tUrl.LastIndexOf('/');
                                if (sIndex > 0)
                                {
                                    tUrl = tUrl.Substring(0, sIndex);
                                }
                            }
                            domains.Add(tUrl);
                            total++;
                            ls.Add(1);
                        }
                    }
                }

                if (domains.Count > 0)
                {
                    if (IsFile == false)
                    {
                        var url = domains.Last();
                        IsFile = url.StartsWith("file://", StringComparison.CurrentCultureIgnoreCase);
                        if (IsFile)
                        {
                            domains.Clear();
                            domains.Add(url);
                            break;
                        }
                    }
                }
            }
            this.Domains = domains.ToArray();
            this._test = test.Values.ToArray();

            this.WeightTotal = total;
            this.Weights = ls.ToArray();

            this.AllowPath = Config(site.AuthConf);
            this.OutputCookies = Config(site.OutputCookies);
            this.LogoutPath = Config(site.LogoutPath);
            this.AppendJSConf = Config(site.AppendJSConf);
            this.RedirectPath = Config(site.RedirectPath);
            this.ImagesConf = Config(site.ImagesConf);
            // this.EventsConf = Config(site.EventsConf);
            var subSite = new List<KeyValue>();
            if (String.IsNullOrEmpty(site.Conf) == false)
            {
                var v = UMC.Data.JSON.Deserialize(site.Conf) as Hashtable;
                if (v != null)
                {
                    var pem = v.GetEnumerator();
                    while (pem.MoveNext())
                    {
                        var key = pem.Key as string;
                        if (key.EndsWith("*"))
                        {
                            subSite.Add(new KeyValue { Key = key.Substring(0, key.Length - 1), Value = pem.Value.ToString(), IsDel = true });
                        }
                        else
                        {
                            subSite.Add(new KeyValue { Key = key, Value = pem.Value.ToString() });
                        }

                    }
                }
            }
            _subSite = subSite.ToArray();
            InitStatic(site.StaticConf);
            InitHost(site.HostReConf);
            InitHeader(site.HeaderConf);
            InitLogConf(site.LogConf);


            this.AllowAllPath = this.AllowPath.Contains("*");

            // var proxy = UMC.Data.Reflection.Configuration("proxy")[this.Site.Root];
            // if (proxy != null)
            // {
            //     this.Proxy = UMC.Data.Reflection.CreateObject(proxy) as UMC.Proxy.SiteProxy;
            // }
        }
        public bool AllowAllPath
        {
            get; set;
        }
        // public UMC.Proxy.SiteProxy Proxy
        // {

        //     get; set;
        // }
        /// <summary>
        /// 默认ContentType类型
        /// </summary>
        public string ContentType
        {
            get; set;
        }
        public static bool CheckMime(Hashtable login)
        {
            var rawUrl = login["RawUrl"] as string;
            if (String.IsNullOrEmpty(rawUrl))
            {
                return false;

            }


            var Method = login["Method"] as string;
            if (String.IsNullOrEmpty(Method))
            {
                return false;
            }

            switch (Method)
            {
                case "POST":
                case "PUT":
                    var ContentType = login["ContentType"] as string;
                    if (String.IsNullOrEmpty(ContentType))
                    {
                        return false;
                    }
                    var value = login["Content"] as string;
                    if (String.IsNullOrEmpty(value))
                    {
                        return false;
                    }
                    break;
            }
            var Finish = login["Finish"] as string;
            if (String.IsNullOrEmpty(Finish))
            {
                return false;
            }
            return true;
        }
        void InitLogConf(String sConf)
        {

            this.LogConf = new LogSetting();

            var cs = new List<String>();
            var hs = new List<String>();
            var rhs = new List<String>();
            if (String.IsNullOrEmpty(sConf) == false)
            {
                foreach (var k in sConf.Split('\n', ','))
                {

                    var v = k.Trim();
                    if (String.IsNullOrEmpty(v) == false && String.Equals(k, "none") == false)
                    {
                        if (v.StartsWith(":"))
                        {
                            hs.Add(v.Substring(1));
                        }
                        else if (v.EndsWith(":"))
                        {

                            rhs.Add(v.Substring(0, v.Length - 1));
                        }
                        else
                        {
                            cs.Add(v);
                        }
                    }

                }
            }

            this.LogConf.Headers = hs.ToArray();
            this.LogConf.ResHeaders = rhs.ToArray();
            this.LogConf.Cookies = cs.ToArray();
        }
        void InitHeader(String sConf)
        {


            if (String.IsNullOrEmpty(sConf) == false)
            {

                foreach (var k in sConf.Split('\n', ','))
                {

                    var v = k.Trim();
                    if (String.IsNullOrEmpty(v) == false && String.Equals(k, "none") == false)
                    {

                        var nindex = v.IndexOf(':');
                        if (nindex == -1)
                        {
                            nindex = v.IndexOf(' ');
                            if (nindex == -1)
                            {
                                nindex = v.IndexOf('\t');
                            }
                        }
                        //var key = v;
                        if (nindex > -1)
                        {
                            var mv = v.Substring(nindex + 1).Trim();//.ToLower();
                            var key = v.Substring(0, nindex).Trim();
                            switch (key.ToLower())
                            {
                                case "content-type":
                                    this.ContentType = mv;
                                    break;
                                default:
                                    _HeaderConf[key] = mv;
                                    break;
                            }

                        }
                    }

                }




            }
        }

        void InitStatic(String sConf)
        {


            if (String.IsNullOrEmpty(sConf) == false)
            {
                if (sConf.Trim().StartsWith("{"))
                {
                    var v = UMC.Data.JSON.Deserialize(sConf) as Hashtable;
                    if (v != null)
                    {
                        var aem = v.GetEnumerator();
                        while (aem.MoveNext())
                        {
                            _StatusPage[aem.Key as String] = -1;
                        }
                    }
                }
                else
                {
                    foreach (var k in sConf.Split('\n', ','))
                    {

                        var v = k.Trim();
                        if (String.IsNullOrEmpty(v) == false && String.Equals(k, "none") == false)
                        {

                            var nindex = v.IndexOf(':');
                            if (nindex == -1)
                            {
                                nindex = v.IndexOf(' ');
                                if (nindex == -1)
                                {
                                    nindex = v.IndexOf('\t');
                                }
                            }
                            var key = v;
                            if (nindex > -1)
                            {
                                var mv = v.Substring(nindex + 1).Trim().ToLower();
                                key = v.Substring(0, nindex).Trim();
                                switch (mv)
                                {
                                    case "a":
                                    case "all":
                                        _StatusPage[key] = 0;
                                        break;
                                    case "u":
                                    case "user":
                                        _StatusPage[key] = 2;
                                        break;
                                    case "one":
                                        _StatusPage[key] = 1;
                                        break;
                                    default:
                                        _StatusPage[key] = UMC.Data.Utility.IntParse(mv, -1);
                                        break;
                                }
                            }
                            else
                            {
                                _StatusPage[key] = -1;
                            }
                        }

                    }


                }

            }
        }

        void InitHost(String sConf)
        {
            var domain = Data.WebResource.Instance().Provider["domain"];
            var union = Data.WebResource.Instance().Provider["union"] ?? ".";

            if (String.IsNullOrEmpty(sConf) == false)
            {
                if (sConf.Trim().StartsWith("{"))
                {
                    var v = UMC.Data.JSON.Deserialize(Site.StaticConf) as Hashtable;
                    if (v != null)
                    {
                        var aem = v.GetEnumerator();
                        while (aem.MoveNext())
                        {
                            if (_HostPage.ContainsKey(aem.Key as String) == false)
                            {
                                _HostPage[aem.Key as String] = new ReplaceSetting() { Model = HostReplaceModel.Replace, Hosts = new Dictionary<string, Uri>() };
                            }
                            ReplaceSetting replaceSetting = _HostPage[aem.Key as String];
                            HostReplaceModel hostReplace = replaceSetting.Model;
                            var mv = aem.Value.ToString().Split(',', ' ', '\t');

                            foreach (var kv in mv)
                            {
                                if (String.IsNullOrEmpty(kv) == false)
                                {
                                    var vk = kv.Trim();
                                    switch (vk)
                                    {
                                        default:
                                            break;
                                        case "rp":
                                            hostReplace |= HostReplaceModel.Replace;
                                            break;
                                        case "rm":
                                            hostReplace |= HostReplaceModel.Remove;
                                            break;
                                        case "input":
                                        case "in":
                                            hostReplace |= HostReplaceModel.Input;
                                            break;
                                        case "cdn":
                                            hostReplace |= HostReplaceModel.CDN;
                                            break;
                                        case "CDN":
                                            hostReplace |= HostReplaceModel.Script;
                                            break;


                                    }
                                }
                            }
                            replaceSetting.Model = hostReplace;


                        }
                    }
                }
                else
                {
                    foreach (var k in sConf.Split('\n'))
                    {

                        var v = k.Trim();
                        if (String.IsNullOrEmpty(v) == false && String.Equals(k, "none") == false)
                        {

                            var nindex = v.IndexOf(':');
                            if (nindex == -1)
                            {
                                nindex = v.IndexOf(' ');
                                if (nindex == -1)
                                {
                                    nindex = v.IndexOf('\t');
                                }
                            }
                            var key = v;
                            if (nindex > -1)
                            {
                                var mv = v.Substring(nindex + 1).Split(',', ' ', '\t');
                                key = v.Substring(0, nindex).Trim();
                                if (_HostPage.ContainsKey(key) == false)
                                {
                                    _HostPage[key] = new ReplaceSetting() { Model = HostReplaceModel.Replace, Hosts = new System.Collections.Generic.Dictionary<String, Uri>() };
                                }
                                ReplaceSetting replaceSetting = _HostPage[key];
                                HostReplaceModel hostReplace = replaceSetting.Model;
                                var list = replaceSetting.Hosts;

                                foreach (var kv in mv)
                                {
                                    if (String.IsNullOrEmpty(kv) == false)
                                    {

                                        var vk = kv.Trim();
                                        switch (vk)
                                        {
                                            default:
                                                if (String.IsNullOrEmpty(vk))
                                                {
                                                    hostReplace |= HostReplaceModel.Replace;
                                                }
                                                else
                                                {
                                                    var sit = DataFactory.Instance().Site(vk);
                                                    if (sit != null)
                                                    {
                                                        var doms = sit.Domain.Split(',', '\n');
                                                        foreach (var dName in doms)
                                                        {
                                                            var dName2 = dName.Trim();
                                                            var url = String.Empty;
                                                            if (String.IsNullOrEmpty(dName2) == false)
                                                            {
                                                                if (dName2.EndsWith("]"))
                                                                {
                                                                    var vin = dName2.LastIndexOf("[");
                                                                    if (vin > -1)
                                                                    {
                                                                        url = dName2.Substring(0, vin).TrimEnd(']', ' ').Trim();
                                                                    }
                                                                    else
                                                                    {
                                                                        url = dName2.Substring(0, vin).TrimEnd(']', ' ').Trim();
                                                                    }
                                                                    var sIndex = url.LastIndexOf('/');
                                                                    if (sIndex > 0)
                                                                    {

                                                                        url = url.Substring(0, sIndex);
                                                                    }
                                                                }
                                                                else if (v.IndexOf('@') == -1)
                                                                {
                                                                    url = dName2;
                                                                    var sIndex = url.LastIndexOf('/');
                                                                    if (sIndex > 0)
                                                                    {
                                                                        url = url.Substring(0, sIndex);
                                                                    }

                                                                }
                                                            }
                                                            if (String.IsNullOrEmpty(url) == false)
                                                            {
                                                                var surl = new Uri(url);
                                                                if (String.IsNullOrEmpty(sit.Host) == false)
                                                                {
                                                                    surl = new Uri(url.Replace(surl.Host, sit.Host));
                                                                }


                                                                list[String.Format("{0}{1}{2}", sit.Root, union, domain)] = surl;// new Uri(String.f);

                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                                break;
                                            case "rp":
                                                hostReplace |= HostReplaceModel.Replace;
                                                break;
                                            case "rm":
                                                hostReplace |= HostReplaceModel.Remove;
                                                break;
                                            case "input":
                                            case "in":
                                                hostReplace |= HostReplaceModel.Input;
                                                break;
                                        }
                                    }
                                }
                                replaceSetting.Model = hostReplace;

                            }
                        }

                    }


                }

            }
        }

        public static Guid MD5Key(params object[] keys)
        {
            var md5 = System.Security.Cryptography.MD5.Create();
            return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(String.Join(",", keys))));


        }
        public static String[] Config(String sConf)
        {
            var saticPagePath = new HashSet<String>();

            if (String.IsNullOrEmpty(sConf) == false)
            {
                if (sConf.Trim().StartsWith("{"))
                {
                    var auth = new Hashtable();

                    var v = UMC.Data.JSON.Deserialize(sConf) as Hashtable;
                    if (v != null)
                    {
                        auth = v;
                    }


                    var aem = auth.GetEnumerator();
                    while (aem.MoveNext())
                    {
                        saticPagePath.Add((aem.Key as string).Trim());
                    }

                }
                else
                {
                    foreach (var k in sConf.Split(',', ' ', '\t', '\n'))
                    {

                        var v = k.Trim();
                        if (String.IsNullOrEmpty(v) == false && String.Equals("none", v) == false)
                        {
                            saticPagePath.Add(v);
                        }

                    }

                }
            }
            return saticPagePath.ToArray();
        }
        public String Home
        {
            get;
            private set;
        }
        public String Root
        {
            get; set;
        }

        public Entities.Site Site
        {
            get; private set;
        }

        public String[] Domains
        {
            get;
            private set;
        }
        public LogSetting LogConf
        {
            get;
            private set;
        }

        public String[] AllowPath
        {
            get;
            private set;
        }
        public String[] LogoutPath
        {
            get;
            private set;
        }
        public String[] OutputCookies
        {
            get;
            private set;
        }
        public String[] ImagesConf
        {
            get;
            private set;
        }
        // public String[] EventsConf
        // {
        //     get;
        //     private set;
        // }
        public String[] AppendJSConf
        {

            get;
            private set;
        }
        public string[] RedirectPath
        {
            get;
            private set;
        }


        public System.Collections.Generic.Dictionary<String, String> HeaderConf
        {
            get
            {
                return _HeaderConf;
            }
        }
        System.Collections.Generic.Dictionary<String, String> _HeaderConf = new Dictionary<string, String>();

        public System.Collections.Generic.Dictionary<String, int> StatusPage
        {
            get
            {
                return _StatusPage;
            }
        }
        System.Collections.Generic.Dictionary<String, int> _StatusPage = new Dictionary<string, int>();


        TestUrl[] _test;// new System.Collections.Generic.Dictionary<string, TestUrl>();

        public TestUrl[] Test
        {

            get
            {
                return _test;
            }
        }
        System.Collections.Generic.Dictionary<String, ReplaceSetting> _HostPage = new System.Collections.Generic.Dictionary<string, ReplaceSetting>();

        public System.Collections.Generic.Dictionary<String, ReplaceSetting> HostPage
        {

            get
            {
                return _HostPage;
            }
        }

        KeyValue[] _subSite;
        public KeyValue[] SubSite
        {
            get
            {
                return _subSite;

            }
        }
    }
}
