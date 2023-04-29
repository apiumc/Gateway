using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UMC.Data;
using UMC.Net;
using UMC.Web;
using UMC.Security;
using UMC.Proxy.Entities;
using System.Collections;

namespace UMC.Proxy
{

    [Mapping(Weight = 0, Desc = "云模块服务组件")]
    public class WebFactory : IWebFactory
    {
        internal static Dictionary<String, Dictionary<String, WebAuthType>> Auths = new Dictionary<string, Dictionary<string, WebAuthType>>();

        class XHRActivity : WebActivity
        {
            public XHRActivity(SiteConfig site)
            {
                this.site = site;

            }
            SiteConfig site;
            public override void ProcessActivity(WebRequest request, WebResponse response)
            {
                var IsAuth = false;
                var user = this.Context.Token.Identity();
                switch (site.Site.AuthType ?? Web.WebAuthType.User)
                {
                    case Web.WebAuthType.Admin:
                        if (user.IsInRole(UMC.Security.Membership.AdminRole))
                        {
                            IsAuth = true;
                        }
                        else if (UMC.Data.DataFactory.Instance().Roles(user.Id.Value, site.Site.SiteKey.Value).Contains(UMC.Security.Membership.AdminRole))
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
                            if (Security.AuthManager.IsAuthorization(user, 0, $"Desktop/{site.Root}"))
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
                            if (AuthManager.IsAuthorization(user, 0, $"Desktop/{site.Root}"))
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
                    var httpProxy = new HttpProxy(site, this.Context.Client.Context, -1, false, "/");
                    if (httpProxy.Domain == null)
                    {
                        this.Prompt("安全审记", $"此应用临时关闭，请联系应用管理员");
                    }
                    switch (site.Site.UserModel)
                    {
                        case UserModel.Bridge:
                            break;
                        default:
                            this.Prompt("UMC云模块只支持权限桥接模式");
                            break;
                    }

                    if (Auths.TryGetValue(site.Root, out var _dic) == false)
                    {
                        _dic = new Dictionary<string, WebAuthType>();
                        Auths[site.Root] = _dic;

                        var ds = JSON.Deserialize(new Uri(httpProxy.Domain, "/UMC/System/Setup/Mapping").WebRequest().Get().ReadAsString()) as Hashtable;

                        if (ds.ContainsKey("data"))
                        {
                            var data = ds["data"] as Array;
                            foreach (var o in data)
                            {
                                var dic = o as Hashtable;
                                if (dic.ContainsKey("model"))
                                {
                                    var model = dic["model"] as string;
                                    var auth = dic["auth"] as string;
                                    WebAuthType authType = WebAuthType.All;
                                    switch (auth)
                                    {
                                        case "all":
                                            continue;
                                        case "admin":
                                            authType = WebAuthType.Admin;
                                            break;
                                        case "guest":
                                            authType = WebAuthType.Guest;
                                            break;
                                        case "user":
                                            authType = WebAuthType.User;
                                            break;
                                        case "usercheck":
                                            authType = WebAuthType.UserCheck;
                                            break;
                                        case "check":
                                            authType = WebAuthType.Check;
                                            break;
                                    }
                                    if (dic.ContainsKey("cmd"))
                                    {
                                        _dic[$"{model}.{dic["cmd"]}"] = authType;
                                    }
                                    else
                                    {
                                        _dic[model] = authType;
                                    }
                                }

                            }
                        }
                    }
                    if (site.Site.IsAuth == true)
                    {
                        if (WebClient.Verify(httpProxy.Account, site.Site.SiteKey.Value, request.Model, request.Command, _dic) == false)
                        {
                            this.Prompt("安全审记", "此云模块受保护，请联系应用管理员");
                        }
                    }
                    else
                    {
                        if (WebClient.Verify(user, 0, request.Model, request.Command, _dic) == false)
                        {
                            this.Prompt("安全审记", "此云模块受保护，请联系应用管理员");
                        }
                    }


                    StringBuilder sb = new StringBuilder();
                    sb.Append("/UMC/?_model=");
                    sb.Append(request.Model);
                    sb.Append("&_cmd=");
                    sb.Append(request.Command);
                    var sv = request.SendValues;
                    if (sv != null)
                    {
                        var em = sv.GetDictionary().GetEnumerator();
                        while (em.MoveNext())
                        {
                            sb.Append("&");
                            sb.Append(Uri.EscapeDataString(em.Key.ToString()));
                            sb.Append("=");
                            sb.Append(Uri.EscapeDataString(em.Value.ToString()));

                        }
                    }
                    if (String.IsNullOrEmpty(request.SendValue) == false)
                    {
                        sb.Append("&");
                        sb.Append(Uri.EscapeDataString(request.SendValue));
                    }

                    httpProxy.AuthBridge();
                    var getUrl = new Uri(httpProxy.Domain, sb.ToString());
                    var content = this.Context.Client.Context;
                    var webReq = httpProxy.Reqesut(content.Transfer(getUrl, httpProxy.Cookies));

                    webReq.Get(res =>
                    {
                        int StatusCode = (int)res.StatusCode;

                        if (StatusCode > 300 && StatusCode < 400)
                        {
                            httpProxy.ProcessEnd();
                            var url = res.Headers.Get("Location");

                            response.ClientEvent |= (WebEvent)131072;
                            response.Headers.Put("Data", new Uri(content.Url, url));

                            this.Context.OutputFinish();
                        }
                        else
                        {
                            res.ReadAsString(xhr =>
                            {
                                httpProxy.ProcessEnd();
                                String eventPfx = "{\"ClientEvent\":";
                                if (xhr.StartsWith(eventPfx))
                                {
                                    var xData = JSON.Deserialize(xhr) as Hashtable;

                                    var webEvent = (WebEvent)Utility.Parse(xData["ClientEvent"].ToString(), 0);

                                    response.ClientEvent = webEvent;
                                    if (xData.ContainsKey("Headers"))
                                    {
                                        var header = xData["Headers"] as Hashtable;
                                        var m = header.GetEnumerator();
                                        while (m.MoveNext())
                                        {
                                            response.Headers.Put(m.Key as string, m.Value);
                                        }
                                    }
                                    if (xData.ContainsKey("Redirect"))
                                    {
                                        var redirect = xData["Redirect"] as Hashtable;
                                        var model = redirect["model"] as string;
                                        var cmd = redirect["cmd"] as string;
                                        if (String.IsNullOrEmpty(model) == false && String.IsNullOrEmpty(cmd) == false)
                                        {
                                            var send = redirect["send"];
                                            if (send is IDictionary)
                                            {
                                                response.Redirect(model, cmd, new WebMeta(send as IDictionary), false);
                                            }
                                            else if (send is string)
                                            {

                                                response.Redirect(model, cmd, send as string, false);
                                            }
                                            else
                                            {
                                                response.Redirect(model, cmd, false);
                                            }
                                        }

                                    }

                                }
                                else
                                {
                                    response.Headers.Put("Data", Data.JSON.Expression(xhr));
                                    response.ClientEvent |= (WebEvent)131072;
                                }

                                this.Context.OutputFinish();
                            }, error =>
                            {
                                if (error is WebAbortException)
                                {
                                    this.Context.OutputFinish();
                                }
                                else
                                {
                                    this.Context.Client.Context.Error(error);
                                }
                            });
                        }
                    });
                    response.Redirect(Empty);

                }
                else
                {
                    this.Prompt("安全审记", $"你没有权限访问此应用，请联系应用管理员");
                }


            }
        }
        class XHRFlow : WebFlow
        {
            private SiteConfig root;

            public XHRFlow(SiteConfig root)
            {
                this.root = root;

            }
            public XHRFlow(SiteConfig root, params string[] cmds)
            {

                this.root = root;
                this.cmds.AddRange(cmds);
            }
            private System.Text.RegularExpressions.Regex regex;
            public XHRFlow(SiteConfig root, System.Text.RegularExpressions.Regex regex)
            {

                this.root = root;
                this.regex = regex;

            }
            private List<String> cmds = new List<string>();
            public override WebActivity GetFirstActivity()
            {
                var cmd = this.Context.Request.Command;
                if (this.cmds.Count > 0)
                {
                    if (String.Equals("*", this.cmds[0]))
                    {
                        if (this.cmds.Exists(g => g == cmd))
                        {
                            return WebActivity.Empty;
                        }
                    }
                    else if (this.cmds.Exists(g => g == cmd) == false)
                    {
                        return WebActivity.Empty;
                    }
                }
                else if (regex != null && regex.IsMatch(cmd) == false)
                {
                    return WebActivity.Empty;
                }

                return new XHRActivity(root);

            }
        }
        public virtual WebFlow GetFlowHandler(string mode)
        {

            var cgf = Data.Reflection.Configuration("UMC");


            var provder = cgf[mode];
            if (provder != null)
            {
                var root = provder["root"];
                if (String.IsNullOrEmpty(root) == false)
                {
                    var psite = UMC.Proxy.DataFactory.Instance().SiteConfig(root);
                    if (psite == null)
                    {
                        return XHRFlow.Empty;
                    }

                    if (String.IsNullOrEmpty(provder.Type) == false)
                    {
                        if (provder.Type.StartsWith("/") && provder.Type.EndsWith("/"))
                        {
                            return new XHRFlow(psite, new System.Text.RegularExpressions.Regex(provder.Type.Trim('/')));
                        }
                        else if (String.Equals("*", provder.Type) == false)
                        {
                            return new XHRFlow(psite, provder.Type.Split(','));
                        }
                        else
                        {
                            return new XHRFlow(psite);
                        }
                    }
                    else
                    {
                        return new XHRFlow(psite);
                    }
                }
            }

            return WebFlow.Empty;
        }
        /// <summary>
        /// 请在此方法中完成url与model的注册,即调用registerModel方法
        /// </summary>
        /// <param name="context"></param>
        public virtual void OnInit(WebContext context)
        {

        }
    }
}

