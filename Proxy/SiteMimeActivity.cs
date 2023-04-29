using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Reflection;
using UMC.Web;
using UMC.Data.Entities;
using UMC.Web.UI;
using UMC.Proxy.Entities;

namespace UMC.Proxy.Activities
{
    /// <summary>
    /// 邮箱账户
    /// </summary>
    [UMC.Web.Mapping("Proxy", "Mime", Auth = WebAuthType.User)]
    public class SiteMimeActivity : WebActivity
    {
        bool Check(Hashtable login)
        {
            var rawUrl = login["RawUrl"] as string;
            if (String.IsNullOrEmpty(rawUrl))
            {
                this.Prompt("接口请求路径未配置");
                return false;

            }


            var Method = login["Method"] as string;
            if (String.IsNullOrEmpty(Method))
            {
                this.Prompt("接口提交方式未配置");
                return false;
            }

            switch (Method)
            {
                case "POST":
                case "PUT":
                    var ContentType = login["ContentType"] as string;
                    if (String.IsNullOrEmpty(ContentType))
                    {

                        this.Prompt("接口提交类型未配置");
                        return false;
                    }
                    var value = login["Content"] as string;
                    if (String.IsNullOrEmpty(value))
                    {

                        this.Prompt("接口提交内容未配置");
                        return false;
                    }
                    break;
            }
            var Finish = login["Finish"] as string;
            if (String.IsNullOrEmpty(Finish))
            {
                this.Prompt("未配置效验格式");
                return false;
            }
            return true;
        }
        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var Key = this.AsyncDialog("Key", g =>
            {
                this.Prompt("请传入KEY");
                return this.DialogValue("none");
            });
            var mainKey = String.Format("SITE_MIME_{0}", Key).ToUpper();
            var config = UMC.Data.DataFactory.Instance().Config(mainKey);
            var value = new Hashtable();
            if (config != null)
            {
                var v = UMC.Data.JSON.Deserialize(config.ConfValue) as Hashtable;
                if (v != null)
                {
                    value = v;
                }

            }

            var ms = request.SendValues ?? request.Arguments;
            var Model = this.AsyncDialog("Model", g =>
            {
                if (ms.ContainsKey("limit") == false)
                {
                    this.Context.Send(new UISectionBuilder(request.Model, request.Command, request.Arguments)
                        .RefreshEvent("Mime.Config")
                        .Builder(), true);

                }


                var title = UITitle.Create();

                title.Title = "接口MIME";
                title.Right(new UIEventText("跨域").Click(new UIClick(new WebMeta(request.Arguments).Put(g, "Domain")).Send(request.Model, request.Command)));


                var ui = UISection.Create(title);
                if (mainKey.EndsWith("_LOGIN"))
                {
                    title.Title = "登录接口";
                }
                else if (mainKey.EndsWith("_UPDATE"))
                {
                    title.Title = "密码托管接口";
                }
                else if (mainKey.EndsWith("_CHECK"))
                {
                    title.Title = "账户检测接口";

                }

                var Method = value["Method"] as string;
                var RawUrl = value["RawUrl"] as string;


                var Domain = value["Domain"] as string;
                if (String.IsNullOrEmpty(Domain) == false)
                {
                    ui.AddCell("跨域域名", Domain, new UIClick(new WebMeta(request.Arguments).Put(g, "UnDomain")).Send(request.Model, request.Command));
                    ui.AddCell("提交路径", String.IsNullOrEmpty(RawUrl) ? "未设置" : RawUrl, new UIClick(new WebMeta(request.Arguments).Put(g, "RawUrl")).Send(request.Model, request.Command));

                }
                else
                {

                    ui.AddCell("提交路径", String.IsNullOrEmpty(RawUrl) ? "未设置" : RawUrl, new UIClick(new WebMeta(request.Arguments).Put(g, "RawUrl")).Send(request.Model, request.Command));
                }
                ui.NewSection().AddCell("提交方式", String.IsNullOrEmpty(Method) ? "未设置" : Method, new UIClick(new WebMeta(request.Arguments).Put(g, "Method")).Send(request.Model, request.Command));
                var Header = value["Header"] as string;
                ui.NewSection().AddCell("提交表头", String.IsNullOrEmpty(Header) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "Header")).Send(request.Model, request.Command));


                if (String.Equals("GET", Method) == false)
                {
                    var ContentType = value["ContentType"] as string ?? "";
                    if (ContentType.EndsWith("urlencoded"))
                    {
                        ContentType = "表单格式";
                    }
                    else if (ContentType.EndsWith("json"))
                    {
                        ContentType = "JSON格式";
                    }
                    else if (ContentType.EndsWith("xml"))
                    {
                        ContentType = "Xml格式";

                    }
                    else if (String.IsNullOrEmpty(ContentType))
                    {
                        ContentType = "未设置";

                    }

                    ui.NewSection().AddCell("提交类型", ContentType, new UIClick(new WebMeta(request.Arguments).Put(g, "ContentType")).Send(request.Model, request.Command));

                    var content = value["Content"] as string;

                    ui.NewSection().AddCell("提交内容", String.IsNullOrEmpty(content) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "Content")).Send(request.Model, request.Command));
                }
                else if (mainKey.EndsWith("_LOGIN") == false && mainKey.EndsWith("_UPDATE") == false && mainKey.EndsWith("_CHECK") == false)
                {
                    var content = value["Content"] as string;

                    ui.NewSection().AddCell("脚本环境", String.IsNullOrEmpty(content) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "Content")).Send(request.Model, request.Command));

                }

                if (mainKey.EndsWith("_LOGIN") || mainKey.EndsWith("_UPDATE") || mainKey.EndsWith("_CHECK"))
                {
                    var Finish = value["Finish"] as string;
                    var root = Key.Substring(0, Key.LastIndexOf('_'));

                    ui.NewSection().AddCell("检测格式", String.IsNullOrEmpty(Finish) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "Finish")).Send(request.Model, request.Command));

                    var fui = ui.NewSection();
                    fui.AddCell("扩展字段", "新增", new UIClick(new WebMeta(request.Arguments).Put(g, "Feilds")).Send(request.Model, request.Command));
                    var feilds = value["Feilds"] as Hashtable;
                    if (feilds != null && feilds.Count > 0)
                    {
                        var fd = feilds.Keys.Cast<String>().OrderBy(r => r).GetEnumerator();

                        while (fd.MoveNext())
                        {
                            var cell = new WebMeta().Put("value", fd.Current).Put("text", feilds[fd.Current]);

                            cell.Put("click", new UIClick(new WebMeta().Put("Key", Key + "_" + fd.Current)).Send(request.Model, request.Command));


                            fui.Delete(UICell.Create("Cell", cell), new UIEventText("移除").Click(new UIClick(new WebMeta(request.Arguments).Put(g, fd.Current)).Send(request.Model, request.Command)));

                        }

                    }
                    if (mainKey.EndsWith("_LOGIN"))
                    {

                        ui.NewSection().AddCell("登录清空会话", value.ContainsKey("IsNotCookieClear") ? "不清空" : "清空", new UIClick(new WebMeta(request.Arguments).Put(g, "IsNotCookieClear")).Send(request.Model, request.Command));


                        if (value.ContainsKey("IsLoginHTML"))
                        {

                            ui.NewSection()
                            .AddCell("内容转化配置", new UIClick(new WebMeta(request.Arguments).Put(g, "Script")).Send(request.Model, request.Command))
                            .NewSection().AddCell("前端页面登录", "已启用", new UIClick(new WebMeta(request.Arguments).Put(g, "IsLoginHTML")).Send(request.Model, request.Command))

                            .AddCell("前端页面内容配置", new UIClick(String.Format("{0}_HTML", mainKey).ToUpper()).Send(request.Model, "Conf"));


                        }
                        else
                        {
                            ui.NewSection().AddCell("前端页面登录", "未启用", new UIClick(new WebMeta(request.Arguments).Put(g, "IsLoginHTML")).Send(request.Model, request.Command));
                        }

                        var Callback = value["Callback"] as string;
                        ui.NewSection().AddCell("跳转参数", String.IsNullOrEmpty(Callback) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "Callback")).Send(request.Model, request.Command));


                    }
                    else if (mainKey.EndsWith("_UPDATE"))
                    {
                        var UpdateModel = value["UpdateModel"] as String ?? "Selected";
                        switch (UpdateModel)
                        {
                            case "Selected":
                                UpdateModel = "默认选中";
                                break;
                            case "Select":
                                UpdateModel = "默认不选中";
                                break;
                            case "Compel":
                                UpdateModel = "强制托管";
                                break;
                            case "Disable":
                                UpdateModel = "禁用托管";
                                break;

                        }
                        ui.NewSection().AddCell("密码托管模式", UpdateModel, new UIClick(new WebMeta(request.Arguments).Put(g, "UpdateModel")).Send(request.Model, request.Command));

                    }
                    else if (mainKey.EndsWith("_CHECK"))
                    {
                        var site = DataFactory.Instance().Site(root.ToLower());
                        if (site != null)
                        {
                            var userM = "未启用";
                            switch (site.UserModel ?? UserModel.Standard)
                            {
                                case UserModel.Check:
                                    userM = "自主选择";
                                    break;
                                case UserModel.Checked:
                                    userM = "自动检测";
                                    break;

                            }
                            ui.NewSection()
                            .AddCell("功能启用", userM, new UIClick(new WebMeta(request.Arguments).Put(g, "UserModel")).Send(request.Model, request.Command))
                              .AddCell("检测账户", String.IsNullOrEmpty(site.Account) ? "未设置" : site.Account, new UIClick("Key", site.Root, "Model", "Account").Send(request.Model, "Site"))
                              .AddCell("检测登录", value.ContainsKey("IsNotLoginApi") ? "不是" : "是", new UIClick(new WebMeta(request.Arguments).Put(g, "IsNotLoginApi")).Send(request.Model, request.Command));


                        }

                    }
                }
                else
                {
                    ui.NewSection().AddCell("内容转化配置", new UIClick(new WebMeta(request.Arguments).Put(g, "Script")).Send(request.Model, request.Command));


                    ui.NewSection().AddCell("记住选择内容", value.ContainsKey("RememberValue") ? "记住" : "不记住", new UIClick(new WebMeta(request.Arguments).Put(g, "RememberValue")).Send(request.Model, request.Command));

                    var defautValue = value["DefautValue"] as string;
                    ui.NewSection().AddCell("内容默认值", String.IsNullOrEmpty(defautValue) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "DefautValue")).Send(request.Model, request.Command));

                }




                response.Redirect(ui);
                return this.DialogValue("none");
            });
            switch (Model)
            {
                case "UserModel":
                    if (Check(value))
                    {
                        var root = Key.Substring(0, Key.LastIndexOf('_'));
                        var site = DataFactory.Instance().Site(root.ToLower());
                        if (site != null)
                        {
                            if (String.IsNullOrEmpty(site.Account))
                            {
                                this.Prompt("未设置检测账户或密码");
                            }
                            var tValue = this.AsyncDialog(Model, g =>
                            {
                                var sheet = new UMC.Web.UISheetDialog() { Title = "账户检测功能" };
                                sheet.Put("启用自动检测", "Checked");
                                sheet.Put("启用自主选择", "Check");
                                sheet.Put("关闭账户检测", "Standard");
                                //sheet.Options.Add(new UIClick(new WebMeta(request.Arguments).Put(g, "Checked")) { Text = "启用自动检测" }.Send(request.Model, request.Command));
                                //sheet.Options.Add(new UIClick(new WebMeta(request.Arguments).Put(g, "Check")) { Text = "启用自主选择" }.Send(request.Model, request.Command));
                                //sheet.Options.Add(new UIClick(new WebMeta(request.Arguments).Put(g, "Standard")) { Text = "关闭账户检测" }.Send(request.Model, request.Command)); //sheet.Options.Add(new UIClick(new WebMeta(request.Arguments).Put(g, "PUT")) { Text = "PUT" }.Send(request.Model, request.Command));
                                return sheet;
                            });
                            var userM = UMC.Data.Utility.Parse(tValue, UserModel.Standard);

                            DataFactory.Instance().Put(new Site
                            {
                                Root = site.Root,
                                UserModel = userM
                            });

                            this.Context.Send("Mime.Config", true);
                        }

                    }
                    this.Context.End();
                    break;
                case "UnDomain":
                    this.AsyncDialog("Confirm", g => new UIConfirmDialog("您确认移除此跨域网址吗"));
                    value.Remove("Domain");
                    break;
                case "Feilds":
                    var t = this.AsyncDialog(Model, g =>
                    {

                        var from4 = new UIFormDialog() { Title = "新增扩展字段" };
                        from4.AddText("字段标题", "Value", "");
                        from4.AddText("字段标识", "Name", "");
                        from4.Submit("确认", "Mime.Config");
                        return from4;
                    });
                    var feilds = value["Feilds"] as Hashtable ?? new Hashtable();
                    feilds[t["Name"]] = t["Value"];
                    value["Feilds"] = feilds;
                    break;

                case "RememberValue":
                    if (value.ContainsKey("RememberValue"))
                    {
                        value.Remove("RememberValue");
                    }
                    else
                    {
                        value["RememberValue"] = "YES";
                    }
                    break;
                default:
                    var sValue = this.AsyncDialog(Model, g =>
                    {
                        switch (Model)
                        {
                            case "ContentType":

                                var sheet = new UMC.Web.UISheetDialog() { Title = "提交方式" };
                                sheet.Put("启用自动检测", "application/x-www-form-urlencoded");
                                sheet.Put("JSON格式", "application/json");
                                sheet.Put("XML格式", "application/xml");
                               return sheet;


                            case "Method":

                                var sheet2 = new UMC.Web.UISheetDialog() { Title = "提交方式" };
                                sheet2.Put("GET").Put("POST").Put("PUT");// "application/json");
                               return sheet2;


                            case "Domain":
                                var fromDomain = new UIFormDialog() { Title = "跨域网址" };
                                fromDomain.AddText("跨域网址", "Domain", value["Domain"] as string);
                                fromDomain.Submit("确认", "Mime.Config");
                                return fromDomain;
                            case "RawUrl":
                                var from = new UIFormDialog() { Title = "提交路径" };
                                from.AddText("提交路径", "RawUrl", value["RawUrl"] as string);
                                from.Submit("确认", "Mime.Config");
                                return from;
                            case "Script":
                                var from5 = new UIFormDialog() { Title = "内容转化" };
                                from5.AddTextarea("脚本或者标识", "Script", value["Script"] as string).Put("Rows", 20);
                                from5.AddPrompt("可配置内容表单name、属性key或者js脚本");
                                from5.Submit("确认", "Mime.Config");
                                return from5;
                            case "Header":
                                var from6 = new UIFormDialog() { Title = "Header字典对" };
                                from6.AddTextarea("字典对", "Header", value["Header"] as string).Put("Rows", 20).PlaceHolder("H:V");

                                from6.Submit("确认", "Mime.Config");
                                return from6;


                            case "Content":

                                var from2 = new UIFormDialog() { Title = "提交内容" };
                                if (String.Equals(value["Method"] as string, "GET"))
                                {
                                    from2.Title = "脚本网址";
                                    from2.AddTextarea("脚本网址", "Content", value["Content"] as string).Put("Rows", 10);

                                    from2.AddPrompt("多项用换行、空格或逗号符分割");
                                }
                                else
                                {
                                    from2.AddTextarea("内容格式", "Content", value["Content"] as string).Put("Rows", 20);

                                }
                                from2.Submit("确认", "Mime.Config");
                                return from2;



                            case "UpdateModel":
                                if (SiteConfig.CheckMime(value) == false)
                                {
                                    this.Prompt("请完成托管密码配置，再来设置此属性");
                                }
                                var from9 = new UIFormDialog() { Title = "密码托管模式" };

                                var UpdateModel = value["UpdateModel"] as String;
                                from9.AddRadio("", "UpdateModel").Put("默认不选中", "Select", String.Equals(UpdateModel, "Select"))
                                .Put("默认选中", "Selected", String.Equals(UpdateModel, "Selected")).Put("强制托管", "Compel", String.Equals(UpdateModel, "Compel")).Put("禁用托管", "Disable", String.Equals(UpdateModel, "Disable"));


                                from9.Submit("确认", "Mime.Config");
                                return from9;


                            case "DefautValue":

                                var from8 = new UIFormDialog() { Title = "内容默认值" };

                                from8.AddText("默认值", "DefautValue", value["DefautValue"] as string).Put("Rows", 20);


                                from8.Submit("确认", "Mime.Config");
                                return from8;
                            case "Callback":

                                var fromCallback = new UIFormDialog() { Title = "跳转参数" };

                                fromCallback.AddText("跳转参数", "Callback", value["Callback"] as string).Put("Rows", 20);


                                fromCallback.Submit("确认", "Mime.Config");
                                return fromCallback;
                            case "IsLoginHTML":
                                return this.DialogValue(value.ContainsKey("IsLoginHTML") ? "none" : "true");
                            case "IsNotCookieClear":
                                return this.DialogValue(value.ContainsKey("IsNotCookieClear") ? "none" : "true");

                            case "IsNotLoginApi":
                                return this.DialogValue(value.ContainsKey("IsNotLoginApi") ? "none" : "true");


                            case "Finish":
                                var from3 = new UIFormDialog() { Title = "检测格式" };
                                var fh = (value["Finish"] as string) ?? "";
                                var isBody = true;
                                var fhv = fh;
                                if (String.IsNullOrEmpty(fh) == false)
                                {
                                    if (fh.StartsWith("E:") || fh.StartsWith("HE:") || fh.StartsWith("H:"))
                                    {
                                        isBody = false;
                                        fhv = fh.Substring(fh.IndexOf(':') + 1);
                                    }
                                }

                                from3.AddRadio("检测模式", "Format").Put("成功正文", "B", isBody)
                                .Put("失败正文", "E", fh.StartsWith("E:")).Put("成功表头", "H", fh.StartsWith("H:")).Put("失败表头", "HE", fh.StartsWith("HE:"));

                                from3.AddPrompt("当模式为成功正文时，内容是“Url”时，表示检测是否重定向");

                                from3.AddText("检测内容", "Finish", fhv);
                                from3.Submit("确认", "Mime.Config");
                                return from3;
                            default:
                                var feilds4 = value["Feilds"] as Hashtable;
                                if (feilds4 != null && feilds4.Count > 0)
                                {
                                    feilds4.Remove(Model);
                                }

                                return this.DialogValue("none");

                        }
                    });

                    if (String.Equals(sValue, "none") == false)
                    {
                        switch (Model)
                        {
                            case "Domain":

                                try
                                {
                                    var Domain = new Uri(sValue);

                                    sValue = new Uri(Domain, "/").AbsoluteUri;
                                }
                                catch
                                {

                                    this.Prompt("跨域格式不正确");
                                }
                                break;
                            case "RawUrl":
                                if (sValue.StartsWith("/") == false || sValue.StartsWith("//"))
                                {
                                    this.Prompt("提交路径格式错误，请确认");
                                }
                                break;
                            case "Finish":
                                if (request.SendValues != null)
                                {
                                    var fFormat = request.SendValues["Format"];
                                    switch (fFormat)
                                    {
                                        case "E":
                                        case "H":
                                        case "HE":
                                            sValue = fFormat + ":" + sValue;
                                            break;
                                    }
                                }
                                break;
                        }
                        value[Model] = sValue;
                    }
                    else
                    {
                        value.Remove(Model);

                    }
                    break;
            }
            Config platformConfig = new Config();
            platformConfig.ConfKey = mainKey;
            platformConfig.ConfValue = UMC.Data.JSON.Serialize(value);
            UMC.Data.DataFactory.Instance().Put(platformConfig);
            this.Context.Send("Mime.Config", true);

        }
    }
}