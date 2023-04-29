using System;
using System.Collections.Generic;
using UMC.Data;
using UMC.Web;
using UMC.Web.UI;

namespace UMC.Proxy
{
    [Mapping("Proxy", "LogConf", Auth = WebAuthType.Admin, Desc = "服务器日志")]
    public class SiteLogConfActivity : WebActivity
    {

        public override void ProcessActivity(WebRequest request, WebResponse response)
        {

            var assConf = Reflection.Configuration("assembly");

            var provider = assConf["Log"] ?? Data.Provider.Create("Log", "none");

            var model = this.AsyncDialog("Id", akey =>
            {
                var form = request.SendValues ?? new WebMeta();
                if (form.ContainsKey("limit") == false)
                {
                    this.Context.Send(new UISectionBuilder(request.Model, request.Command)
                        .RefreshEvent($"{request.Model}.{request.Command}")
                        .Builder(), true);

                }

                var ui = UISection.Create(new UITitle("日志组件"));



                switch (provider.Type)
                {
                    case "csv":
                        ui.AddCell("日志类型", "文本格式", new UIClick("CHANGE").Send(request.Model, request.Command));
                        var fui = ui.NewSection();
                        var fields = provider["field"];

                        var fs = new List<string>(new string[] { "Address", "Site", "Path", "Username", "Duration", "Time", "Status", "UserAgent", "Account", "Referrer", "Attachment", "Server" });

                        if (String.IsNullOrEmpty(fields) == false)
                        {

                            foreach (var c in fields.Split(','))
                            {
                                var k = c.Trim();
                                if (String.IsNullOrEmpty(k) == false)
                                {
                                    if (fs.Exists(r => String.Equals(r, k)) == false)
                                    {
                                        fs.Add(k);
                                    }
                                }
                            }

                        }
                        foreach (var k in fs)
                        {
                            switch (k)
                            {
                                case "Address":
                                    fui.AddCell(k, "客户端IP");
                                    break;
                                case "Site":
                                    fui.AddCell(k, "应用名称");
                                    break;
                                case "Path":
                                    fui.AddCell(k, "请求路径");
                                    break;
                                case "Username":
                                    fui.AddCell(k, "所属账户");
                                    break;
                                case "Duration":
                                    fui.AddCell(k, "请求耗时");
                                    break;
                                case "Time":
                                    fui.AddCell(k, "发生时间");
                                    break;
                                case "Status":
                                    fui.AddCell(k, "响应状态");
                                    break;
                                case "UserAgent":
                                    fui.AddCell(k, "终端设备");
                                    break;
                                case "Account":
                                    fui.AddCell(k, "应用账户");
                                    break;
                                case "Referrer":
                                    fui.AddCell(k, "所在页面");
                                    break;
                                case "Attachment":
                                    fui.AddCell(k, "下载文件");
                                    break;
                                case "Server":
                                    fui.AddCell(k, "服务器名");
                                    break;
                                default:

                                    fui.AddCell(k, new UIClick(k).Send(request.Model, request.Command));
                                    break;
                            }
                        }

                        break;

                    case "json":
                        ui.AddCell("日志类型", "Json格式", new UIClick("CHANGE").Send(request.Model, request.Command));
                        ui.NewSection().AddCell("发送网址", provider["url"]).AddCell("发送方式", provider["method"]);
                        break;
                    default:
                        ui.AddCell("日志类型", "未启用", new UIClick("CHANGE").Send(request.Model, request.Command));

                        UIDesc desc = new UIDesc("日志记录未启用");
                        desc.Desc("{icon}\n{desc}").Put("icon", "\uf24a");
                        desc.Style.Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60));
                        ui.NewSection().Add(desc);
                        break;
                }




                ui.UIFootBar = new UIFootBar() { IsFixed = true };
                ui.UIFootBar.AddText(new UIEventText("新增字段").Click(new UIClick("ADD").Send(request.Model, request.Command)),
                    new UIEventText("重新加载").Click(new UIClick("LoadConf").Send(request.Model, request.Command)).Style(new UIStyle().BgColor()));
                response.Redirect(ui);

                return this.DialogValue("none");
            });

            switch (model)
            {
                case "JSON":
                    var Domains = this.AsyncDialog("JSON", r =>
                    {
                        var fm = new UIFormDialog() { Title = "JSON日志格式" };

                        fm.AddText("发送网址", "url", provider["url"]);
                        fm.AddRadio("发送方式", "method").Put("POST", "POST", provider["method"] == "POST").Put("PUT", "PUT", provider["method"] == "PUT");
                        fm.Submit("确认", $"{request.Model}.{request.Command}");
                        return fm;
                    });
                    var provider2 = Data.Provider.Create("Log", "json");
                    provider2.Attributes.Add(provider.Attributes);
                    provider2.Attributes["method"] = Domains["method"];

                    provider2.Attributes["url"] = new Uri(Domains["url"]).AbsoluteUri;
                    
                    assConf.Add(provider2);
                    UMC.Data.Reflection.Configuration("assembly", assConf);
                    this.Context.Send($"{request.Model}.{request.Command}", true);
                    break;

                case "ADD":
                    if (String.Equals(provider.Type, "csv") == false)
                    {
                        this.Prompt("不是文本格式不支持添加字段");
                    }
                    var aField = this.AsyncDialog("Field", r =>
                    {
                        var fm = new UIFormDialog() { Title = "新增文本日志字段" };

                        fm.AddText("字段", "field", provider["url"]);
                        fm.Submit("确认", $"{request.Model}.{request.Command}");
                        return fm;
                    })["field"];
                    {
                        var fields = provider["field"];

                        var fs = new List<string>();

                        if (String.IsNullOrEmpty(fields) == false)
                        {

                            foreach (var c in fields.Split(','))
                            {
                                var k = c.Trim();
                                if (String.IsNullOrEmpty(k) == false)
                                {
                                    if (fs.Exists(r => String.Equals(r, k)) == false)
                                    {
                                        fs.Add(k);
                                    }
                                }
                            }

                        }
                        fs.Add(aField);
                        provider.Attributes["field"] = String.Join(",", fs.ToArray());
                        
                    assConf.Add(provider);
                        UMC.Data.Reflection.Configuration("assembly", assConf);
                        this.Context.Send($"{request.Model}.{request.Command}", true);
                    }
                    break;
                case "CHANGE":
                    var changeType = this.AsyncDialog("Change", r =>
                    {
                        var uis = new UISheetDialog();
                        switch (provider.Type)
                        {
                            case "json":
                                uis.Put("文本格式", "csv").Put("关闭日志", "none");
                                break;
                            case "csv":
                                uis.Put(new UIClick("JSON") { Text = "JSON格式" }.Send(request.Model, request.Command)).Put("关闭日志", "none");
                                break;
                            default:
                            case "none":
                                uis.Put(new UIClick("JSON") { Text = "JSON格式" }.Send(request.Model, request.Command)).Put("文本格式", "csv");
                                break;
                        }
                        return uis;
                    });

                    var provider3 = Data.Provider.Create("Log", changeType);
                    provider3.Attributes.Add(provider.Attributes);
                    
                    assConf.Add(provider3);
                    UMC.Data.Reflection.Configuration("assembly", assConf);
                    this.Context.Send($"{request.Model}.{request.Command}", true);
                    break;
                default:

                    {
                        var fields = provider["field"];

                        var fs = new List<string>();

                        if (String.IsNullOrEmpty(fields) == false)
                        {

                            foreach (var c in fields.Split(','))
                            {
                                var k = c.Trim();
                                if (String.IsNullOrEmpty(k) == false)
                                {
                                    if (fs.Exists(r => String.Equals(r, k)) == false)
                                    {
                                        fs.Add(k);
                                    }
                                }
                            }

                        }
                        fs.Remove(model);
                        provider.Attributes["field"] = String.Join(",", fs.ToArray());

                        assConf.Add(provider);//["Log"] = provider;
                        UMC.Data.Reflection.Configuration("assembly", assConf);
                        this.Context.Send($"{request.Model}.{request.Command}", true);
                    }
                    break;
                case "LoadConf":
                    UMC.Proxy.LogSetting.Instance().LoadConf();
                    this.Prompt("已经从新加载日志组件参数");
                    break;
            }

        }
    }
}