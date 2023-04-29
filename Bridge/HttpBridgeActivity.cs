using System;
using UMC.Data;
using UMC.Net;
using UMC.Web;


namespace UMC.Host
{
    [Mapping("Http", "Bridge", Auth = WebAuthType.Admin, Desc = "Web VPN")]
    class HttpBridgeActivity : WebActivity
    {

        public override void ProcessActivity(WebRequest request, WebResponse response)
        {

            var provider = Data.WebResource.Instance().Provider;

            var secret = provider["appSecret"];
            var model = this.AsyncDialog("Model", r => this.DialogValue("Info"));
            switch (model)
            {
                case "Reload":
                    var msg = HttpMimeServier.Load(UMC.Data.Reflection.Configuration("host"));
                    if (msg.Length > 0)
                    {
                        this.Prompt("提示", msg);
                    }
                    else
                    {
                        this.Prompt("已经成功加载");
                    }
                    break;
                case "BridgeSrc":
                    {
                        this.Prompt("Web VPN", "正在准备其他Web VPN节点服务器。");
                    }
                    break;
                case "Info":
                    {
                        if (String.IsNullOrEmpty(secret))
                        {
                            response.Redirect(new WebMeta().Put("msg", "应用未注册", "status", "未开通"));

                        }
                        var bridge = provider["bridge"];
                        if (String.IsNullOrEmpty(bridge))
                        {
                            var webr = new Uri(APIProxy.Uri, "Transfer").WebRequest();

                            var ns = new System.Collections.Specialized.NameValueCollection();

                            UMC.Proxy.Utility.Sign(webr, ns, secret);
                            var data = JSON.Deserialize<WebMeta>(webr.Get().ReadAsString());//?? new WebMeta();
                            if ((data?.ContainsKey("domain") ?? false) == false)
                            {
                                response.Redirect(new WebMeta().Put("msg", "未注册域名", "status", "未开通"));
                            }
                            else
                            {
                                var domain = data["domain"];
                                var scheme = data["scheme"] ?? "http";
                                bridge = $"{scheme}://{domain}";

                                var pc = Reflection.Configuration("assembly");
                                provider.Attributes["bridge"] = bridge;
                                pc.Add(provider);
                                Reflection.Configuration("assembly", pc);
                            }
                        }
                        var meta = new WebMeta("domain", bridge);
                        if (HttpBridgeClient.IsRunning)
                        {
                            meta.Put("status", "已开启");
                            meta.Put("bridge", true);
                        }
                        else
                        {
                            meta.Put("status", "未开启");
                        }
                        response.Redirect(meta);

                    }
                    break;
                case "Stop":
                    {
                        if (HttpBridgeClient.IsRunning)
                        {

                            var meta = new WebMeta();
                            this.AsyncDialog("Confirm", r => new UIConfirmDialog("你需要关停Web VPN服务吗"));
                            HttpBridgeClient.Stop();
                            meta.Put("domain", provider["bridge"]);
                            meta.Put("status", "未开启");
                            this.Prompt("Web VPN已经关停", false);

                            this.Context.Send($"{request.Model}.{request.Command}", meta, true);
                        }
                        else
                        {

                            this.Prompt("Web VPN已经关停");
                        }
                    }
                    break;
                case "Recharge":
                    {
                        var ComboValue = UMC.Data.Utility.IntParse(UMC.Web.UIDialog.AsyncDialog(this.Context, "Combo", gg =>
                        {
                            var webr = new Uri(APIProxy.Uri, "Transfer").WebRequest();
                            var ns = new System.Collections.Specialized.NameValueCollection();

                            UMC.Proxy.Utility.Sign(webr, ns, secret);
                            var data = JSON.Deserialize<System.Collections.Hashtable>(webr.Post(new WebMeta().Put("type", "Bridge")).ReadAsString());

                            request.Arguments["API"] = data["src"] as string;
                            var Combo = data[gg] as Array;

                            var fom = new Web.UIFormDialog() { Title = "流量充值" };
                            var style = new UIStyle();
                            style.Name("icon").Color(0x09bb07).Size(84).Font("wdk");
                            style.Name("title").Color(0x333).Size(20);
                            style.BgColor(0xfafcff).Height(200).AlignCenter();
                            var desc = new UMC.Web.WebMeta().Put("title", "Web VPN").Put("icon", "\uf0ee");
                            fom.Config.Put("Header", new UIHeader().Desc(desc, "{icon}\n{title}", style));

                            var f = fom.AddRadio("充值套餐", "Combo");
                            var cl = Combo.Length;
                            for (var i = 0; i < cl; i++)
                            {
                                var hash = Combo.GetValue(i) as System.Collections.Hashtable;
                                f.Put(hash["Text"] as string, hash["Value"] as string, i == cl - 1);
                            }
                            fom.AddPrompt("每充值1G流量，则延长1个月的过期时长");

                            fom.Config.Put("Action", true);

                            fom.Submit("确认充值");
                            return fom;
                        }), 0);
                        var src = this.AsyncDialog("API", r =>
                        {
                            var appId = provider["appId"];
                            return this.DialogValue($"https://api.apiumc.com/UMC/Platform/Alipay/Bridge?AuthKey={appId}");

                        });
                        response.Redirect(new Uri($"{src}&Combo={ComboValue}"));
                    }
                    break;
                case "Start":
                    {
                        if (String.IsNullOrEmpty(secret))
                        {
                            this.Prompt("当前版本未登记注册", false);
                            response.Redirect("System", "License");

                        }
                        var webr = new Uri(APIProxy.Uri, "Transfer").WebRequest();
                        var ns = new System.Collections.Specialized.NameValueCollection();
                        UMC.Proxy.Utility.Sign(webr, ns, secret);
                        var xhr = webr.Get();
                        switch (xhr.StatusCode)
                        {
                            case System.Net.HttpStatusCode.Unauthorized:
                            case System.Net.HttpStatusCode.Forbidden:
                                response.Redirect("System", "License", new UIConfirmDialog("检验不通过或注册信息有误,请从新注册") { DefaultValue = "Select" });
                                break;
                        }
                        var data = JSON.Deserialize<WebMeta>(xhr.ReadAsString()) ?? new WebMeta();
                        if (data.ContainsKey("msg"))
                        {
                            this.Prompt(data["msg"]);
                        }
                        var isbridge = HttpBridgeClient.IsRunning;
                        if (data.ContainsKey("ip"))
                        {
                            var domain = data["domain"];
                            if (HttpBridgeClient.IsRunning == false)
                            {
                                isbridge = true;
                                try
                                {
                                    HttpBridgeClient.Start(domain, data["ip"], UMC.Data.Utility.IntParse(data["port"], 0), 4);
                                }
                                catch (Exception ex)
                                {
                                    this.Prompt("连接错误", ex.Message);
                                }
                                var scheme = data["scheme"] ?? "http";
                                var bridgeUrl = $"{scheme}://{domain}";
                                var meta = new WebMeta();
                                meta.Put("domain", bridgeUrl);

                                meta.Put("bridge", true);
                                meta.Put("status", "已开启");
                                this.Context.Send($"{request.Model}.{request.Command}", meta, false);

                                var isTag = false;

                                if (String.IsNullOrEmpty(provider["domain"]))
                                {
                                    provider.Attributes["scheme"] = scheme;
                                    provider.Attributes["domain"] = domain;
                                    isTag = true;
                                }
                                if (String.Equals(provider.Attributes["bridge"], bridgeUrl) == false)
                                {
                                    provider.Attributes["bridge"] = bridgeUrl;
                                    isTag = true;
                                }
                                if (isTag)
                                {
                                    var pc = Reflection.Configuration("assembly") ?? new ProviderConfiguration();
                                    pc.Add(provider);
                                    Reflection.Configuration("assembly", pc);
                                }
                            }
                        }
                        else
                        {
                            isbridge = false;
                            if (HttpBridgeClient.IsRunning)
                            {
                                HttpBridgeClient.Stop();
                            }
                        }

                        UMC.Web.UIDialog.AsyncDialog(this.Context, "Info", gg =>
                        {
                            var style = new UIStyle();
                            style.Name("icon").Color(isbridge ? 0x09bb07 : 0xead848).Size(104).Font("wdk");
                            style.Name("title").Color(0x333).Size(20);
                            style.BgColor(0xfafcff).Height(200).AlignCenter();

                            var fom = new Web.UIFormDialog() { Title = "Web VPN" };
                            if (HttpBridgeClient.IsRunning)
                                fom.Menu("关停", request.Model, request.Command, "Stop");
                            var desc = new UMC.Web.WebMeta().Put("title", isbridge ? "Web VPN已经开启" : (data["tip"] ?? "无可用的流量，请充值")).Put("icon", isbridge ? "\uEA06" : "\uEA05");

                            fom.Config.Put("Header", new UIHeader().Desc(desc, "{icon}\n{title}", style));

                            var caption = data["caption"];

                            fom.Add(UICell.UI("所属账户", String.IsNullOrEmpty(caption) ? "[点击完善]" : caption, new UIClick("Name").Send("System", "License")));

                            var bridgeSrc = data["bridgeSrc"];

                            if (String.IsNullOrEmpty(bridgeSrc) == false)
                            {

                                fom.Add(UICell.UI("VPN节点", data["bridgeNode"] ?? "default", UIClick.Url(new Uri(bridgeSrc))));//.Send("System", "License")));
                            }
                            else
                            {
                                fom.Add(UICell.UI("VPN节点", data["bridgeNode"] ?? "default", new UIClick("BridgeSrc").Send(request.Model, request.Command))); ;
                            }

                            var opts = new ListItemCollection();
                            fom.AddTextValue("Web VPN", opts);

                            fom.Config.Put("Action", new UIClick("Recharge").Send(request));

                            opts.Put("剩余流量", data["allowSize"])
                                .Put("上行流量", data["inputSize"])
                                .Put("下行流量", data["outputSize"])
                                .Put("流量过期", data["expireTime"]);
                            fom.AddPrompt("注意：流量过期后，剩余流量将会清零");
                            fom.Submit("去充值", $"{request.Model}.{request.Command}");

                            fom.Add(UICell.UI("联系官方", "让天才工程师为你服务", new UIClick("Contact").Send("System", "License")));

                            return fom;
                        });



                    }
                    break;
            }

        }
    }
}