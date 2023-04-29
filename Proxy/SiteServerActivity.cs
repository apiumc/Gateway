using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using UMC.Data;
using UMC.Data.Entities;
using UMC.Net;
using UMC.Security;
using UMC.Web;
using UMC.Web.UI;


namespace UMC.Proxy.Activities
{
    [Mapping("Proxy", "Server", Auth = WebAuthType.Admin, Desc = "Http服务配置")]
    public class SiteServerActivity : WebActivity
    {

        public override void ProcessActivity(WebRequest request, WebResponse response)
        {

            var hosts = UMC.Data.Reflection.Configuration("host");
            var model = this.AsyncDialog("Model", akey =>
            {
                var form = request.SendValues ?? new WebMeta();
                if (form.ContainsKey("limit") == false)
                {
                    this.Context.Send(new UISectionBuilder(request.Model, request.Command)
                        .RefreshEvent($"{request.Model}.{request.Command}")
                        .Builder(), true);

                }

                var ui = UISection.Create(new UITitle("网关服务"));

                var unix = hosts.Providers.Where(r =>
                {
                    switch (r.Type)
                    {
                        case "unix":
                            return true;
                        default:
                            return false;
                    }
                });
                var provider = Data.WebResource.Instance().Provider;
                ui.AddCell("主协议", provider["scheme"] ?? "http", new UIClick("Domain").Send(request.Model, request.Command))
               .AddCell("主域名", provider["domain"] ?? "未设置", new UIClick("Domain").Send(request.Model, request.Command)).AddCell("连接符", provider["union"] ?? ".", new UIClick("Domain").Send(request.Model, request.Command));

                ui.NewSection().AddCell("日志组件", new UIClick().Send("Proxy", "LogConf"));




                var http = hosts.Providers.Where(r =>
                {
                    switch (r.Type)
                    {
                        case "unix":
                        case "https":
                            return false;
                        default:
                            return true;
                    }
                });

                var httpUI = ui.NewSection();
                httpUI.AddCell("Http", new UIClick("Http").Send(request.Model, request.Command));
                if (http.Count() > 0)
                {
                    foreach (var p in http)
                    {
                        var cell = UI.UI("端口", p.Attributes["port"] ?? "80");
                        httpUI.Delete(cell, new UIEventText().Click(new UIClick(p.Name).Send(request.Model, request.Command)));
                    }
                }
                else
                {
                    UIDesc desc = new UIDesc("未配置Http端口");
                    desc.Desc("{icon}\n{desc}").Put("icon", "\uf24a");
                    desc.Style.Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60));
                    httpUI.Add(desc);

                }

                var https = hosts.Providers.Where(r =>
                {
                    switch (r.Type)
                    {
                        case "https":
                            return true;
                        default:
                            return false;
                    }
                });
                var httpsUI = ui.NewSection();
                httpsUI.AddCell("Https", new UIClick("Https").Send(request.Model, request.Command));
                if (https.Count() > 0)
                {
                    foreach (var p in https)
                    {
                        var cell = UI.UI("端口", p.Attributes["port"] ?? "80");//, new UIClick(p.Name).Send(request.Model, request.Command));
                        httpsUI.Delete(cell, new UIEventText().Click(new UIClick(akey, p.Name, "Type", "Del").Send(request.Model, request.Command)));
                    }

                }
                else
                {
                    UIDesc desc = new UIDesc("未配置Https端口");
                    desc.Desc("{icon}\n{desc}").Put("icon", "\uf24a");
                    desc.Style.Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60));
                    httpsUI.Add(desc);
                }
                var sslUI = ui.NewSection();

                sslUI.AddCell("证书", new UIClick("Cert").Send(request.Model, request.Command));
                var now = UMC.Data.Utility.TimeSpan();
                var ls = Certificater.Certificates.Values.OrderBy(r =>
                {

                    if (r.Certificate != null)
                    {
                        r.Time = Utility.TimeSpan(Convert.ToDateTime(r.Certificate.GetExpirationDateString()));

                    }
                    return r.Time;
                });
                foreach (var r in ls)
                {
                    var cell = UI.UI(r.Name, Utility.Expire(now, r.Time, "正签发"), new UIClick(akey, "CSR", "Domain", r.Name).Send(request.Model, request.Command));
                    sslUI.Delete(cell, new UIEventText().Click(new UIClick(akey, "Del", "Domain", r.Name).Send(request.Model, request.Command)));

                }
                if (ls.Count() == 0)
                {
                    UIDesc desc = new UIDesc("未有SSL/TLS证书");
                    desc.Desc("{icon}\n{desc}").Put("icon", "\uf24a");
                    desc.Style.Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60));
                    sslUI.Add(desc);

                }


                ui.UIFootBar = new UIFootBar() { IsFixed = true };
                ui.UIFootBar.AddText(new UIEventText("申请免费证书").Click(new UIClick("ApplyCert").Send(request.Model, request.Command)),
                    new UIEventText("重新加载").Click(new UIClick("Reload").Send("Http", "Bridge")).Style(new UIStyle().BgColor()));


                response.Redirect(ui);

                return this.DialogValue("none");
            });
            switch (model)
            {
                case "Unix":
                    hosts.Add(UMC.Data.Provider.Create("unix", "unix"));
                    UMC.Data.Reflection.Configuration("host", hosts);
                    this.Context.Send($"{request.Model}.{request.Command}", true);
                    break;
                case "Domain":
                    var provider = Data.WebResource.Instance().Provider;
                    var Domains = this.AsyncDialog("Domain", r =>
                    {
                        var fm = new UIFormDialog() { Title = "网关参数" };

                        fm.AddText("主域名", "domain", provider["domain"]);
                        var union = provider["union"] ?? ".";
                        var scheme = provider["scheme"] ?? "http";
                        fm.AddRadio("主协议", "scheme").Put("http", "http", scheme == "http").Put("https", "https", scheme == "https");
                        fm.AddRadio("连接符", "union").Put("-", "-", union == "-").Put(".", ".", union == ".");
                        fm.Submit("确认", $"{request.Model}.{request.Command}");
                        return fm;
                    });
                    provider.Attributes["union"] = Domains["union"];
                    provider.Attributes["scheme"] = Domains["scheme"];
                    provider.Attributes["domain"] = Domains["domain"];

                    var pc = UMC.Data.Reflection.Configuration("assembly") ?? new ProviderConfiguration();

                    pc.Add(provider);
                    UMC.Data.Reflection.Configuration("assembly", pc);
                    this.Context.Send($"{request.Model}.{request.Command}", true);
                    break;
                case "Share":
                    {
                        var secret = WebResource.Instance().Provider["appSecret"];
                        if (String.IsNullOrEmpty(secret))
                        {
                            this.Prompt("当前版本未登记注册", false);
                            response.Redirect("System", "License");
                        }
                        var webr2 = new Uri(APIProxy.Uri, "Certificater").WebRequest();
                        UMC.Proxy.Utility.Sign(webr2, new System.Collections.Specialized.NameValueCollection(), secret);

                        var webr = webr2.Post(new WebMeta().Put("type", "share"));

                        var str = webr.ReadAsString();

                        var hs = JSON.Deserialize<WebMeta>(str);
                        this.Context.Send("Clipboard", new WebMeta().Put("text", hs["text"]).Put("msg", hs["msg"]), true);

                    }

                    break;
                case "Prom":
                    {

                        if ((request.SendValues?.ContainsKey("limit") ?? false) == false)
                        {
                            this.Context.Send(new UISectionBuilder(request.Model, request.Command, request.Arguments)

                                    .Builder(), true);
                        }

                        var webr = new Uri(APIProxy.Uri, "/UMC/System/Docs/apiumc?limit=30").WebRequest(); 

                        response.Redirect(JSON.Expression(webr.Get().ReadAsString()));
                        
                    }
                    break;
                case "BuyAll":
                    response.Redirect(request.Model, request.Command, new WebMeta().Put("Model", "Recharge", "Code", "*"), true);
                    break;
                case "Site":
                    var Site = this.AsyncDialog("Site", r =>
                    {
                        return new UITextDialog(hosts.ProviderType) { Title = "服务站点" };
                    });
                    hosts.ProviderType = Site;
                    UMC.Data.Reflection.Configuration("host", hosts);
                    this.Context.Send($"{request.Model}.{request.Command}", true);
                    break;
                case "Cert":
                    {

                        var httpPorts2 = this.AsyncDialog("Cert", r =>
                        {
                            var fm = new UIFormDialog() { Title = "证书" };
                            fm.AddText("域名", "Domain", String.Empty);
                            fm.AddTextarea("公钥", "publicKey", String.Empty).Put("Rows", 10).PlaceHolder("以-----BEGIN CERTIFICATE-----开始的证书").Put("tip", "公钥证书");
                            fm.AddTextarea("私钥", "privateKey", String.Empty).Put("Rows", 10).PlaceHolder("以-----BEGIN RSA PRIVATE KEY-----开始的证书").Put("tip", "私钥证书");
                            fm.Submit("确认添加", $"{request.Model}.{request.Command}");
                            return fm;
                        });

                        var certs = UMC.Data.Reflection.Configuration("certs");
                        try
                        {
                            var x509 = X509Certificate2.CreateFromPem(httpPorts2["publicKey"], httpPorts2["privateKey"]);
                            if (Utility.Parse(x509.GetExpirationDateString(), DateTime.MinValue) < DateTime.Now)
                            {
                                x509.Dispose();
                                this.Prompt("此证书已过期");
                            }
                            var p = UMC.Data.Provider.Create(httpPorts2["Domain"], "Cert");
                            p.Attributes["publicKey"] = httpPorts2["publicKey"];
                            p.Attributes["privateKey"] = httpPorts2["privateKey"];
                            certs.Add(p);
                            UMC.Net.Certificater.Certificates[p.Name] = new Certificater
                            {
                                Name = p.Name,
                                Status = 1,
                                Certificate = x509
                            };
                            UMC.Data.Reflection.Configuration("certs", certs);
                            this.Context.Send($"{request.Model}.{request.Command}.Cert", true);
                        }
                        catch
                        {
                            this.Prompt("证书不正确");
                        }
                        break;
                    }
                case "Del":
                    {
                        var host = this.AsyncDialog("Domain", "none");
                        if (UMC.Net.Certificater.Certificates.TryGetValue(host, out var _v))
                        {
                            if (UMC.Net.Certificater.Certificates.Remove(host))
                            {
                                var certs = UMC.Data.Reflection.Configuration("certs");
                                certs.Remove(host);
                                UMC.Data.Reflection.Configuration("certs", certs);
                            }
                        }

                        this.Context.Send($"{request.Model}.{request.Command}.Del", true);

                    }
                    break;
                case "ApplyCert":
                    {
                        var host = UIDialog.AsyncDialog(this.Context, "Domain", g =>
                        {
                            var fm = new UIFormDialog() { Title = "申请证书" };
                            fm.AddText("域名", "Domain", String.Empty);
                            fm.Submit("确认申请", $"{request.Model}.{request.Command}");
                            return fm;
                        });

                        if (System.Text.RegularExpressions.Regex.IsMatch(host, @"^([a-z0-9\*]([a-z0-9\-]{0,61}[a-z0-9])?\.)+[a-z0-9]{1,6}$") == false)
                        {
                            this.Prompt("域名格式不正确");
                        }

                        var secret = WebResource.Instance().Provider["appSecret"];
                        if (String.IsNullOrEmpty(secret))
                        {
                            this.Prompt("当前版本未登记注册", false);
                            response.Redirect("System", "License");
                        }
                        var webr2 = new Uri(APIProxy.Uri, "Certificater").WebRequest();
                        UMC.Proxy.Utility.Sign(webr2, new System.Collections.Specialized.NameValueCollection(), secret);

                        var webr = webr2.Post(new WebMeta().Put("type", "apply", "domain", host));

                        var str = webr.ReadAsString();
                        if (webr.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            var hs = JSON.Deserialize<WebMeta>(str);
                            this.Context.Send($"{request.Model}.{request.Command}", false);
                            if (string.Equals(hs["code"], "success"))
                            {
                                if (UMC.Net.Certificater.Certificates.TryGetValue(hs["domain"] ?? host, out var _v) == false)
                                {
                                    _v = new Certificater() { Name = hs["domain"] ?? host, Status = 0 };
                                    UMC.Net.Certificater.Certificates[_v.Name] = _v;
                                }
                                //verifing
                                _v.Status = -1;
                                response.Redirect(request.Model, request.Command, new UIConfirmDialog("正在签发证书，确认进入证书签发详情", "CSR"), new WebMeta("Domain", host), true);

                            }
                            else if (string.Equals(hs["code"], "completed"))
                            {
                                if (Certificater.Certificates.TryGetValue(host, out var _cert) == false || _cert.Certificate == null)
                                {
                                    webr2.Post(new WebMeta().Put("type", "cert", "domain", host), UMC.Proxy.Utility.Certificate);
                                }
                                this.Prompt(hs["msg"]);
                            }
                            else if (string.Equals(hs["code"], "verifing"))
                            {

                                response.Redirect(request.Model, request.Command, new WebMeta("Domain", host).Put("Model", "CSR"), true);
                            }
                            else
                            {
                                this.Prompt("提示", hs["msg"], false);

                            }

                        }
                        else
                        {
                            this.Prompt("错误", $"请确保域名“{host}”解释到服务器，并开放80端口");
                        }
                    }
                    break;
                case "CSR":
                    {
                        var host = UIDialog.AsyncDialog(this.Context, "Domain", g =>
                         {
                             var fm = new UIFormDialog() { Title = "申请证书" };
                             fm.AddText("域名", "Domain", String.Empty);
                             return fm;
                         });
                        var webr2 = new Uri(APIProxy.Uri, "Certificater").WebRequest();

                        var secret = WebResource.Instance().Provider["appSecret"];
                        if (String.IsNullOrEmpty(secret))
                        {
                            this.Prompt("当前版本未登记注册", false);
                            response.Redirect("System", "License");
                        }
                        UMC.Proxy.Utility.Sign(webr2, new System.Collections.Specialized.NameValueCollection(), secret);

                        var certmodel = this.AsyncDialog("CertModel", rm =>
                        {
                            var form = request.SendValues ?? new UMC.Web.WebMeta();
                            if (form.ContainsKey("limit") == false)
                            {
                                this.Context.Send(new UISectionBuilder(request.Model, request.Command, request.Arguments)
                                    .RefreshEvent($"{request.Model}.{request.Command}")
                                    .Builder(), true);

                            }

                            var json = webr2.Post(new WebMeta().Put("type", "info", "domain", host)).ReadAsString();


                            var hash = JSON.Deserialize<Hashtable>(json);

                            var ui = UISection.Create(new UITitle("SSL/TLS证书"));
                            ui.AddCell("域名", hash["domain"] as string);

                            var strBtn = "从新签发";
                            if (hash.ContainsKey("order"))
                            {
                                ui.AddCell("单号", hash["order"] as string);

                                var csr = ui.NewSection();
                                switch (hash["status"] as string)
                                {
                                    case "domain_verifing":
                                        csr.AddCell("证书状态", hash["state"] as string, new UIClick(new WebMeta(request.Arguments).Put(rm, "verifing")).Send(request.Model, request.Command));
                                        break;
                                    case "check":
                                        csr.AddCell("证书状态", hash["state"] as string, new UIClick(new WebMeta(request.Arguments).Put(rm, "check")).Send(request.Model, request.Command));

                                        break;
                                    case "cname":
                                        // strBtn = "域名验证";
                                        //
                                        //  this.Context.Send("Clipboard", new WebMeta().Put("text", cookie), true);
                                        csr.AddCell("证书状态", hash["state"] as string);
                                        csr.NewSection()//.AddCell("主域名", hash["cname"] as string)
                                         .AddCell("记录类型", "CNAME")
                                        .AddCell("主机记录", "点击复制", new UIClick(new WebMeta().Put("text", hash["auth_path"] as string)) { Key = "Clipboard" })
                                        .AddCell("记录值", "点击复制", new UIClick(new WebMeta().Put("text", hash["auth_val"] as string)) { Key = "Clipboard" })
                                        .Header.Put("text", "请在主域名解释添加如下记录");
                                        break;
                                    default:

                                        csr.AddCell("证书状态", hash["state"] as string);
                                        break;
                                }

                                if (hash.ContainsKey("expire"))
                                {
                                    csr.AddCell("证书过期", hash["expire"] as string);
                                }
                            }

                            if (UMC.Net.Certificater.Certificates.TryGetValue(host, out var _v))
                            {
                                if (_v.Certificate != null)
                                {
                                    var cn = _v.Certificate.Subject.Split(',').First(r => r.Trim().StartsWith("CN=")).Substring(3);
                                    if (hash.ContainsKey("expire"))
                                    {
                                        ui.NewSection().AddCell("证书公用名", cn);
                                    }
                                    else
                                    {
                                        ui.NewSection().AddCell("证书公用名", cn).AddCell("证书过期", Utility.Expire(Utility.TimeSpan(), Utility.TimeSpan(Convert.ToDateTime(_v.Certificate.GetExpirationDateString())), "正签发"));
                                    }
                                }
                                else if (hash.ContainsKey("order"))
                                {
                                    strBtn = "下载证书";
                                }
                            }
                            else if (hash.ContainsKey("order"))
                            {
                                strBtn = "下载证书";
                            }
                            else
                            {
                                strBtn = "免费申请";


                                UIDesc desc = new UIDesc("未有证书，请申请");
                                desc.Desc("{icon}\n{desc}").Put("icon", "\uf24a");
                                desc.Style.Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60));
                                ui.NewSection().Add(desc);


                            }
                            ui.NewSection().AddCell("自动续签", hash["contract"] as string, new UIClick("Model", "Recharge", "Code", hash["domain"] as string).Send(request.Model, request.Command))
                                .AddCell("续签优惠", hash["renewalCount"] as string, new UIClick("Prom").Send(request.Model, request.Command));//.Header.Put("text", "公共服务");




                            ui.NewSection().AddCell("联系官方", "让天才工程师为你服务", new UIClick("Contact").Send("System", "License"));

                            ui.UIFootBar = new UIFootBar() { IsFixed = true };
                            switch (hash["status"] as string)
                            {
                                case "cname":
                                    ui.UIFootBar.AddText(new UIEventText("验证域名记录").Click(new UIClick(new WebMeta(request.Arguments).Put(rm, "verifing")).Send(request.Model, request.Command)),
                                               new UIEventText("订阅自动续签").Click(new UIClick("Model", "Recharge", "Code", hash["root"] as string).Send(request.Model, request.Command)).Style(new UIStyle().BgColor()));

                                    break;
                                default:
                                    ui.UIFootBar.AddText(new UIEventText(strBtn).Click(new UIClick("Model", "ApplyCert", "Domain", hash["domain"] as string).Send(request.Model, request.Command)),
                                            new UIEventText("订阅自动续签").Click(new UIClick("Model", "Recharge", "Code", hash["root"] as string).Send(request.Model, request.Command)).Style(new UIStyle().BgColor()));
                                    break;
                            }
                            response.Redirect(ui);
                            return this.DialogValue("none");
                        });

                        switch (certmodel)
                        {
                            case "verifing":
                                var json = webr2.Post(new WebMeta().Put("type", "verify", "domain", host)).ReadAsString();
                                var hash = JSON.Deserialize<WebMeta>(json);
                                if (String.Equals(hash?["code"], "success") == false)
                                {
                                    this.Prompt(hash["msg"]);
                                }
                                break;
                            case "check":
                                webr2.Post(new WebMeta().Put("type", "cert", "domain", host), Utility.Certificate);
                                break;
                        }
                        this.Context.Send($"{request.Model}.{request.Command}", true);
                    }
                    break;
                case "Http":
                    var httpPort = UIDialog.AsyncDialog(this.Context, "Port", g =>
                       {
                           var fm = new UIFormDialog() { Title = "Http服务" };
                           fm.AddNumber("端口", "Port", String.Empty);
                           fm.Submit("确认", $"{request.Model}.{request.Command}");
                           return fm;
                       });
                    if (Utility.IntParse(httpPort, 0) > 0)
                    {
                        var p = UMC.Data.Provider.Create(httpPort, "http");
                        p.Attributes["port"] = httpPort;
                        hosts.Add(p);
                        UMC.Data.Reflection.Configuration("host", hosts);
                        this.Context.Send($"{request.Model}.{request.Command}", true);
                    }
                    else
                    {
                        this.Prompt("请输入正确的端口号");
                    }
                    break;

                case "Recharge":
                    {
                        var Code = UIDialog.AsyncDialog(this.Context, "Code", g =>
                       {
                           var fm = new UIFormDialog() { Title = "域名" };
                           fm.AddText("域名", "Code", String.Empty);
                           return fm;
                       });
                        var ComboValue = UMC.Data.Utility.IntParse(UMC.Web.UIDialog.AsyncDialog(this.Context, "Combo", gg =>
                        {
                            var webr = new Uri(APIProxy.Uri, "Transfer").WebRequest();
                            var ns = new System.Collections.Specialized.NameValueCollection();

                            var secret = WebResource.Instance().Provider["appSecret"];
                            UMC.Proxy.Utility.Sign(webr, ns, secret);
                            var data = JSON.Deserialize<System.Collections.Hashtable>(webr.Post(new WebMeta().Put("type", "Cert").Put("code", Code)).ReadAsString());

                            request.Arguments["API"] = data["src"] as string;
                            var Combo = data[gg] as Array;

                            var fom = new Web.UIFormDialog() { Title = "订阅" };
                            var style = new UIStyle();
                            style.Name("icon").Color(0x09bb07).Size(84).Font("wdk");
                            style.Name("title").Color(0x333).Size(20);
                            style.BgColor(0xfafcff).Height(200).AlignCenter();
                            var desc = new UMC.Web.WebMeta().Put("title", "证书自动续签服务").Put("icon", "\uf0ee");
                            fom.Config.Put("Header", new UIHeader().Desc(desc, "{icon}\n{title}", style));
                            fom.AddTextValue().Put("订阅域名", data["Text"] as string ?? Code);
                            var f = fom.AddRadio("订阅套餐", "Combo");
                            var cl = Combo.Length;
                            for (var i = 0; i < cl; i++)
                            {
                                var hash = Combo.GetValue(i) as System.Collections.Hashtable;
                                f.Put(hash["Text"] as string, hash["Value"] as string, i == cl - 1);
                            }
                            fom.Config.Put("Action", true);

                            fom.Submit("确认订阅");
                            return fom;
                        }), 0);
                        var src = this.AsyncDialog("API", r =>
                        {
                            var appId = WebResource.Instance().Provider["appId"];
                            return this.DialogValue($"https://api.apiumc.com/UMC/Platform/Alipay/Cert?AuthKey={appId}");

                        });
                        response.Redirect(new Uri($"{src}&Combo={ComboValue}&Code={Code}"));
                    }
                    break;
                case "Https":

                    var httpsPort = UIDialog.AsyncDialog(this.Context, "Port", g =>
                       {
                           var fm = new UIFormDialog() { Title = "Https服务" };
                           fm.AddNumber("端口", "Port", String.Empty);
                           fm.Submit("确认", $"{request.Model}.{request.Command}");
                           return fm;
                       });
                    if (Utility.IntParse(httpsPort, 0) > 0)
                    {
                        var p = UMC.Data.Provider.Create(httpsPort, "https");
                        p.Attributes["port"] = httpsPort;

                        hosts.Add(p);
                        UMC.Data.Reflection.Configuration("host", hosts);
                        this.Context.Send($"{request.Model}.{request.Command}", true);
                    }
                    else
                    {
                        this.Prompt("请输入正确的端口号");
                    }
                    break;
                default:

                    var pr = hosts[model];
                    if (pr != null)
                    {
                        hosts.Remove(model);
                        UMC.Data.Reflection.Configuration("host", hosts);
                        this.Context.Send($"{request.Model}.{request.Command}", true);
                    }
                    break;
            }

        }
    }
}