using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.IO.Compression;
using UMC.Data;
using UMC.Net;
using UMC.Proxy.Entities;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using UMC.Data.Caches;
using System.Security.Cryptography.X509Certificates;

namespace UMC.Proxy
{
    public class DataFactory : IStringSubscribe
    {
        static DataFactory()
        {
            HotCache.Register<Site>("Root").Register("SiteKey");
            HotCache.Register<Cookie>("user_id", "Domain", "IndexValue");
            HotCache.Register<SiteHost>("Host").Register("Root", "Host");
        }
        public static DataFactory Instance()
        {
            if (_Instance == null)
            {
                _Instance = new DataFactory();
                NetSubscribe.Subscribe("SiteConfig", _Instance);
            }
            return _Instance;
        }
        static DataFactory _Instance;// = new DataFactory();
        public static void Instance(DataFactory dataFactory)
        {
            _Instance = dataFactory;
            NetSubscribe.Subscribe("SiteConfig", _Instance);
        }


        public virtual Site[] Site()
        {
            int index;
            return HotCache.Cache<Site>().Find(new Entities.Site(), 0, out index);

        }


        public virtual Site Site(String root)
        {
            return HotCache.Cache<Site>().Get(new Proxy.Entities.Site { Root = root });

        }
        public virtual Site Site(int siteKey)
        {
            return HotCache.Cache<Site>().Get(new Proxy.Entities.Site { SiteKey = siteKey });

        }

        public virtual void Put(SiteHost host)
        {
            HotCache.Cache<SiteHost>().Put(host);
        }
        public virtual SiteHost HostSite(string host)
        {
            return HotCache.Cache<SiteHost>().Get(new Entities.SiteHost { Host = host });
        }
        public virtual SiteHost[] Host(string root)
        {
            int index;
            return HotCache.Cache<SiteHost>().Find(new Entities.SiteHost { Root = root }, 0, out index);
        }
        public virtual void Delete(SiteHost host)
        {
            HotCache.Cache<SiteHost>().Delete(host);
        }
        public virtual Cookie Cookie(String domain, Guid user_id, int index)
        {
            return HotCache.Cache<Cookie>().Get(new Proxy.Entities.Cookie { Domain = domain, user_id = user_id, IndexValue = index });


        }
        public virtual Cookie[] Cookies(String domain, Guid user_id)
        {
            int index;
            return HotCache.Cache<Cookie>().Find(new Entities.Cookie { user_id = user_id, Domain = domain }, 0, out index);
        }
        public virtual void Put(Site site)
        {
            site.Root = site.Root.ToLower();
            HotCache.Cache<Site>().Put(site);
        }
        public virtual bool IsRegister()
        {
            var appId = WebResource.Instance().Provider["appId"];
            var secret = Data.WebResource.Instance().Provider["appSecret"];
            if (String.IsNullOrEmpty(secret) == false && String.IsNullOrEmpty(appId) == false)
            {
                var webr4 = new Uri(APIProxy.Uri, "Transfer").WebRequest();// Utility.Parse36Encode(Utility.Guid(appId).Value))).WebRequest();
                var nvs = new System.Collections.Specialized.NameValueCollection();
                Utility.Sign(webr4, nvs, secret);
                return webr4.Get().StatusCode == System.Net.HttpStatusCode.OK;
            }
            return false;
        }
        public virtual void Delete(Site site)
        {
            HotCache.Cache<Site>().Delete(site);
        }
        public virtual void Delete(Cookie cookie)
        {
            if (String.IsNullOrEmpty(cookie.Domain) == false && cookie.user_id.HasValue)
            {
                if (cookie.IndexValue.HasValue == false)
                {
                    cookie.IndexValue = 0;
                }
                HotCache.Cache<Cookie>().Delete(cookie);
            }
        }
        public virtual void Put(Cookie cookie)
        {
            if (cookie.IndexValue.HasValue == false)
            {
                cookie.IndexValue = 0;
            }
            if (String.IsNullOrEmpty(cookie.Domain) == false && cookie.user_id.HasValue)
            {
                HotCache.Cache<Cookie>().Put(cookie);
            }

        }
        public virtual String Evaluate(String js, params string[] args)
        {
            return "";
        }
        public virtual Stream Decompress(Stream response, string encoding)
        {
            switch (encoding)
            {
                case "gzip":
                    return new GZipStream(response, CompressionMode.Decompress);
                case "deflate":
                    return new DeflateStream(response, CompressionMode.Decompress);
                case "br":
                    return new BrotliStream(response, System.IO.Compression.CompressionMode.Decompress);
                default:
                    return response;
            }
        }
        public virtual Stream Compress(Stream response, string encoding)
        {
            switch (encoding)
            {
                case "gzip":
                    return new GZipStream(response, CompressionMode.Compress);
                case "deflate":
                    return new DeflateStream(response, CompressionMode.Compress);
                case "br":
                    return new BrotliStream(response, System.IO.Compression.CompressionLevel.Fastest);
                default:
                    return response;
            }
        }

        Dictionary<String, SiteConfig> siteConfigs = new Dictionary<string, SiteConfig>();
        public virtual SiteConfig SiteConfig(String root)
        {
            SiteConfig config;
            if (siteConfigs.TryGetValue(root, out config))
            {
                return config;
            }
            else
            {

                var site = this.Site(root);
                if (site != null)
                {
                    config = new SiteConfig(site);
                    siteConfigs[root] = config;
                    return config;
                }
                return null;

            }
        }

        public virtual void Delete(SiteConfig siteConfig)
        {
            siteConfigs.Remove(siteConfig.Root);
            WebFactory.Auths.Remove(siteConfig.Root);
            NetSubscribe.Publish("SiteConfig", siteConfig.Root);

        }

        void IStringSubscribe.Subscribe(string message)
        {
            WebFactory.Auths.Remove(message);
            siteConfigs.Remove(message);
        }
    }
}
