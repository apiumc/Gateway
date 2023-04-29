using System;
using System.Collections.Generic;
using System.Linq;
using UMC.Web;
using UMC.Data.Entities;
using UMC.Proxy.Entities;
using System.IO;
using UMC.Data;
using System.Text.RegularExpressions;

namespace UMC.Proxy.Activities
{
    [UMC.Web.Mapping("Proxy", "App", Auth = WebAuthType.User)]
    public class SiteAppActivity : UMC.Web.WebActivity
    {
        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var home = request.Url.Authority;//

            var union = Data.WebResource.Instance().Provider["union"] ?? ".";
            if (Regex.IsMatch(request.Url.Host, @"^(\d{1,3}.)+\d{1,3}$"))
            {
                home = WebResource.Instance().WebDomain();
                if (home == "localhost")
                {
                    union = ".";
                }
            }
            var Key = this.AsyncDialog("Key", g =>
            {
                var auth = String.Empty;
                var type = this.AsyncDialog("Type", gt =>
                {
                    if (request.UserAgent.Contains("DingTalk"))
                    {
                        if (request.UserAgent.Contains("Windows NT") || request.UserAgent.Contains("Mac OS X"))
                        {
                            var seesionKey = Utility.MD5(this.Context.Token.Device.Value);

                            var sesion = UMC.Data.DataFactory.Instance().Session(this.Context.Token.Device.ToString());

                            if (sesion != null)
                            {
                                sesion.SessionKey = seesionKey;

                                UMC.Data.DataFactory.Instance().Put(sesion);
                            }
                            return this.DialogValue("Auth");
                        }
                    }

                    return this.DialogValue("ALL");

                });
                switch (type)
                {
                    case "Auth":
                        auth = $"/!/{Utility.MD5(this.Context.Token.Device.Value)}";
                        break;
                }

                var sts = new System.Data.DataTable();
                sts.Columns.Add("title");
                sts.Columns.Add("root");
                sts.Columns.Add("url");
                sts.Columns.Add("src");
                sts.Columns.Add("target");
                sts.Columns.Add("badge");
                sts.Columns.Add("desktop", typeof(bool));


                if (request.IsMaster)
                {
                    sts.Rows.Add("应用设置", "Settings", "/Setting/",
                      "/css/images/icon/prefapp.png", "max", "", true);

                    sts.Rows.Add("新增应用", "Add", "javascript:void(0)",
                   "/css/images/icon/add.png", "max", "", true);

                    sts.Rows.Add("帮助文档", "Docs", "/Docs/",
                   "/css/images/icon/ibooks.png", "max", "", true);
                }

                var keys = new List<String>();
                var user = this.Context.Token.Identity();



                UMC.Data.Session<UMC.Web.WebMeta> session = new Data.Session<WebMeta>($"{user.Id}_Desktop");

                var desktop = session.Value ?? new WebMeta();
                var sites = DataFactory.Instance().Site().Where(r => r.Flag != -1).Where(r => (r.IsModule ?? false) == false)
                .OrderBy((arg) => arg.Caption).ToList();

                Utility.Each(sites, r => keys.Add($"Desktop/{r.Root}"));
                var auths = UMC.Security.AuthManager.IsAuthorization(user, 0, keys.ToArray());

                var webr = UMC.Data.WebResource.Instance();
                var ds = sites.ToArray();
                for (var i = 0; i < ds.Length; i++)
                {
                    var d = ds[i];

                    if (auths[i])
                    {
                        var strUrl = $"{request.Url.Scheme}://{d.Root}{union}{home}{auth}/UMC.For/{request.Server}";

                        var title = d.Caption ?? ""; ;


                        var badge = "";

                        var target = "_blank";
                        switch (d.OpenModel ?? 0)
                        {
                            case 1:
                                target = "normal";
                                break;
                            case 2:
                                target = "max";
                                break;
                        }
                        if ((d.OpenModel ?? 0) == 3)
                        {
                            strUrl = new Uri(new Uri(SiteConfig.Config(d.Domain)[0]), d.Home ?? "/").AbsoluteUri;
                        }
                        else if (SiteConfig.Config(d.AuthConf).Contains("*") || d.AuthType == WebAuthType.All)
                        {
                            strUrl = $"{request.Url.Scheme}://{d.Root}{union}{home}{d.Home}";
                        }
                        var isDesktop = desktop.ContainsKey(d.Root);
                        if (d.IsDesktop == true)
                        {
                            if (String.Equals("hide", desktop[d.Root]))
                            {
                                isDesktop = false;
                            }
                            else
                            {
                                isDesktop = true;
                            }

                        }


                        sts.Rows.Add(title.Trim(), d.Root, strUrl, webr.ImageResolve(Data.Utility.Guid(d.Root, true).Value, "1", 4) + $"&_t={d.ModifyTime}", target, badge, isDesktop);
                    }
                }
                if (String.IsNullOrEmpty(WebResource.Instance().Provider["appId"]))
                {
                    response.Redirect("System", "License", new Web.UIConfirmDialog("当前版本未注册，请完成登记注册")
                    {
                        Title = "应用注册",
                        DefaultValue = "Select"
                    }, false);
                }

                this.Context.Send("Desktop", new WebMeta().Put("apps", sts), true);
                return this.DialogValue("none");
            });

            var site = DataFactory.Instance().Site(Key);

            var Model = this.AsyncDialog("Model", gkey =>
            {
                if (site == null)
                {
                    return this.DialogValue(Key);
                }
                if (request.IsMaster)
                {
                    //this.Prompt("进入应用配置管理界面", false);
                    response.Redirect(request.Model, "Site", site.Root);
                }
                WebMeta form = request.SendValues ?? new UMC.Web.WebMeta();

                if (form.ContainsKey("limit") == false)
                {
                    this.Context.Send(new UISectionBuilder(request.Model, request.Command, request.Arguments)
                            .Builder(), true);

                }
                var title = new UITitle("关于应用");
                var ui = UMC.Web.UISection.Create(title);// new UITitle("关于应用"));

                title.Style.BgColor(0x28c7ca);
                title.Style.Color(0xfff);



                var Discount = new UIHeader.Portrait(Data.WebResource.Instance().ImageResolve(Data.Utility.Guid(site.Root, true).Value, "1", 4) + $"&_t={site.ModifyTime}");


                Discount.Value(site.Caption);


                //var color = 0x28CA40;
                Discount.Gradient(0x28c7ca, 0x0eaee3);

                var header = new UIHeader();

                var style = new UIStyle();
                header.AddPortrait(Discount);
                header.Put("style", style);



                ui.UIHeader = header;
                ui.AddCell("版本", site.Version ?? "01");

                switch (site.UserModel ?? UserModel.Standard)
                {
                    default:
                    case UserModel.Standard:
                        ui.AddCell("账户对接", "标准模式");
                        break;
                    case UserModel.Share:
                        ui.AddCell("账户对接", "共享模式");
                        break;
                    case UserModel.Quote:
                        ui.AddCell("账户对接", "引用模式");
                        break;
                    case UserModel.Bridge:
                        ui.AddCell("账户对接", "桥接模式");
                        break;
                }

                var ui3 = ui.NewSection();
                ui3.Header.Put("text", "应用管理员");

                var user = this.Context.Token.Identity();
                var isAdmin = false;
                var admins = Data.DataFactory.Instance().Users(site.SiteKey.Value, UMC.Security.Membership.AdminRole);
                if (admins.Length > 0)
                {
                    foreach (var v in admins)
                    {
                        ui3.AddCell('\uf2c0', v.Alias, "");
                        if (isAdmin == false)
                        {
                            isAdmin = v.Id == user.Id;
                        }
                    }
                }
                else
                {
                    ui3.Add("Desc", new UMC.Web.WebMeta().Put("desc", "未设置应用管理员").Put("icon", "\uEA05"), new UMC.Web.WebMeta().Put("desc", "{icon}\n{desc}"),
                  new UIStyle().Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60)));//.Name 

                }

                if (request.IsCashier || isAdmin)
                    ui3.NewSection().AddCell('\uf085', "应用配置", String.Empty, new UIClick(site.Root).Send(request.Model, "Site"));


                response.Redirect(ui);
                return this.DialogValue("none");
            });
            switch (Model)
            {
                case "Settings":
                    this.AsyncDialog(Model, r =>
                    {
                        var sheet = new UISheetDialog();
                        sheet.Put(new UIClick() { Text = "网关服务" }.Send("Proxy", "Server"))
                        .Put(new UIClick() { Text = "高速存储" }.Send("System", "Cache"))
                        .Put(new UIClick("account") { Text = "账户配置" }.Send("System", "Config"));
                        return sheet;
                    });
                    break;
                case "Desktop":
                    {
                        if (request.IsMaster)
                        {
                            var media_id = this.AsyncDialog("media_id", g =>
                            {
                                if (request.IsApp)
                                {
                                    return Web.UIDialog.CreateDialog("File");
                                }
                                else
                                {
                                    var from = new Web.UIFormDialog() { Title = "设置登录背景图" };
                                    from.AddFile("选择图片", "media_id", String.Empty);
                                    return from;
                                }
                            });
                            var bgSrc = String.Empty;
                            if (media_id.StartsWith("/TEMP/"))
                            {
                                bgSrc = String.Format("/UserResources/BgSrc{0}", media_id.Substring(5));
                                string filename = UMC.Data.Reflection.ConfigPath(String.Format("Static{0}", media_id));
                                if (System.IO.File.Exists(filename))
                                {
                                    using (System.IO.Stream sWriter = File.OpenRead(filename))
                                    {
                                        UMC.Data.Utility.Copy(sWriter, UMC.Data.Reflection.ConfigPath($"Static{bgSrc}"));
                                        sWriter.Close();
                                    }

                                    var provider = UMC.Data.WebResource.Instance().Provider;
                                    var pc = Reflection.Configuration("assembly") ?? new ProviderConfiguration();
                                    pc.Add(provider);//["WebResource"] = provider;
                                    provider.Attributes["bgsrc"] = bgSrc;
                                    Reflection.Configuration("assembly", pc);
                                    this.Prompt("设置成功");
                                }
                            }
                            else if (media_id.StartsWith("http://") || media_id.StartsWith("https://"))
                            {
                                var url = new Uri(media_id);
                                bgSrc = String.Format("/UserResources/BgSrc{0}", url.AbsolutePath.Substring(5));
                                WebResource.Instance().Transfer(url, bgSrc);

                                var provider = UMC.Data.WebResource.Instance().Provider;
                                var pc = Reflection.Configuration("assembly") ?? new ProviderConfiguration();

                                pc.Add(provider);
                                provider.Attributes["bgsrc"] = bgSrc;
                                Reflection.Configuration("assembly", pc);
                                this.Prompt("设置成功");
                            }
                        }
                    }
                    break;
                case "BgSrc":
                    {
                        var media_id = this.AsyncDialog("media_id", "none");
                        if (media_id.StartsWith("/TEMP/"))
                        {
                            string filename = UMC.Data.Reflection.ConfigPath(String.Format("Static{0}", media_id));
                            if (System.IO.File.Exists(filename))
                            {
                                var bgSrc = String.Format("/UserResources/BgSrc{0}", media_id.Substring(5));
                                using (System.IO.Stream sWriter = File.OpenRead(filename))
                                {
                                    UMC.Data.Utility.Copy(sWriter, UMC.Data.Reflection.ConfigPath($"Static{bgSrc}"));
                                    sWriter.Close();
                                }

                                var user = this.Context.Token.Identity();
                                UMC.Data.Session<UMC.Web.WebMeta> session = new Data.Session<WebMeta>($"{user.Id}_Desktop");
                                var value = session.Value ?? new WebMeta();
                                value.Put("BgSrc", bgSrc);
                                session.ContentType = "Settings";
                                session.Commit(value, user.Id.Value, true, request.UserHostAddress);

                                this.Context.Send("BgSrc", new WebMeta().Put("src", bgSrc), true);

                                response.Redirect(request.Model, request.Command, new WebMeta().Put("Key", "LoginBgSrc", "BgSrc", bgSrc), true);


                            }
                        }
                    }
                    break;
                case "PlusDesktop":
                    {
                        var user = this.Context.Token.Identity();
                        UMC.Data.Session<UMC.Web.WebMeta> session = new Data.Session<WebMeta>($"{user.Id}_Desktop");
                        var value = session.Value ?? new WebMeta();
                        value.Put(site.Root, true);
                        session.ContentType = "Settings";
                        session.Commit(value, user.Id.Value, true, request.UserHostAddress);
                        response.Redirect(new WebMeta().Put("Desktop", true));
                    }
                    break;
                case "RemoveDesktop":
                    {
                        var user = this.Context.Token.Identity();
                        UMC.Data.Session<UMC.Web.WebMeta> session = new Data.Session<WebMeta>($"{user.Id}_Desktop");
                        var value = session.Value ?? new WebMeta();
                        if (site.IsDesktop == true)
                        {
                            value.Put(site.Root, "hide");
                        }
                        else
                        {
                            value.Remove(site.Root);

                        }
                        session.ContentType = "Settings";
                        session.Commit(value, user.Id.Value, true, request.UserHostAddress);
                        response.Redirect(new WebMeta().Put("Desktop", true));
                    }

                    break;
                case "Account":
                    {
                        var user = this.Context.Token.Identity();
                        switch (site.UserModel ?? UserModel.Standard)
                        {
                            case UserModel.Check:
                            default:
                            case UserModel.Checked:
                            case UserModel.Standard:
                                break;
                            case UserModel.Bridge:
                                this.Prompt("此应用不支持设置多账户");
                                break;
                        }

                        var scookies = DataFactory.Instance().Cookies(site.Root, user.Id.Value).OrderBy(r => r.IndexValue).ToList();
                        var login = UMC.Data.Utility.TimeSpan();

                        var vt = login;
                        foreach (var sc in scookies)
                        {
                            if (String.IsNullOrEmpty(sc.Account))
                            {
                                login = sc.IndexValue ?? 0;
                                break;
                            }
                        }
                        if (login <= 0)
                        {
                            this.Prompt("请先设置自己主账户");
                        }
                        else
                        {
                            if (vt == login)
                            {
                                DataFactory.Instance().Put(new Entities.Cookie() { IndexValue = login, user_id = user.Id, Domain = site.Root });
                            }

                            this.Context.Send("Desktop.Open", new WebMeta("title", site.Caption, "id", site.Root, "text", "多账户对接")
                                .Put("src", String.Format("{0}://{1}{2}{3}/UMC.Login/New", request.Url.Scheme, site.Root, union, home
                                 , login)).Put("max", true), true);
                        }
                    }
                    break;
                case "Delete":
                    {
                        var ls = DataFactory.Instance().Cookies(site.Root, this.Context.Token.UserId.Value)
                            .Where(r => String.IsNullOrEmpty(r.Account) == false).ToArray();
                        if (ls.Length == 0)
                        {
                            this.Prompt("还未绑定账户，不需要移除");
                        }
                        var indexValue = UMC.Data.Utility.IntParse(this.AsyncDialog("IndexValue", k =>
                        {
                            if (ls.Length == 1)
                            {
                                return new UIConfirmDialog("您确认移除此应用的绑定吗") { DefaultValue = (ls[0].IndexValue ?? 0).ToString() };
                            }
                            else
                            {
                                var dc = new UISheetDialog() { Title = "请选择移除账户" };
                                foreach (var c in ls)
                                {
                                    if (String.IsNullOrEmpty(c.Account) == false && c.IndexValue != 0)
                                    {
                                        dc.Put(c.Account, c.IndexValue.ToString());
                                    }
                                }
                                return dc;
                            }
                        }), 0);
                        UMC.Data.DataFactory.Instance().Delete(new Password { Key = SiteConfig.MD5Key(site.Root, this.Context.Token.UserId.Value, indexValue) });
                        DataFactory.Instance().Delete(new Cookie { user_id = this.Context.Token.UserId.Value, Domain = site.Root, IndexValue = indexValue });

                        this.Prompt(String.Format("解除账户绑定成功", site.Caption));
                    }
                    break;
                case "Setting":
                    if (site != null)
                    {
                        if (request.IsMaster == false)
                        {
                            var rols = UMC.Data.DataFactory.Instance().Roles(this.Context.Token.UserId.Value, site.SiteKey.Value);
                            if (rols.Contains(UMC.Security.Membership.AdminRole) == false)
                            {
                                this.Prompt("应用管理的需要应用管理员权限");
                            }

                        }
                        this.Context.Send("Desktop.Open", new WebMeta("title", site.Caption + "设置", "id", site.Root, "text", "应用账户")
                            .Put("src", $"/Setting/{site.SiteKey.Value}").Put("max", true), true);
                    }
                    break;
                case "Password":
                    {
                        var ls = DataFactory.Instance().Cookies(site.Root, this.Context.Token.UserId.Value)
                               .Where(r => String.IsNullOrEmpty(r.Account) == false).ToArray();
                        if (ls.Length == 0)
                        {
                            this.Prompt("您未对接此应用");
                        }

                        var indexValue = UMC.Data.Utility.IntParse(this.AsyncDialog("IndexValue", k =>
                        {
                            var dc = new UISheetDialog() { Title = "请选择账户" };
                            foreach (var c in ls)
                            {
                                if (String.IsNullOrEmpty(c.Account) == false)
                                {
                                    dc.Put(c.Account, c.IndexValue.ToString());// new UIClick(new WebMeta(request.Arguments).Put(k, c.IndexValue)) { Text = c.Account }.Send(request.Model, request.Command));
                                }
                            }
                            if (dc.Count < 2)
                            {
                                return this.DialogValue(ls[0].IndexValue.ToString());
                            }
                            return dc;
                        }), 0);
                        var cookie = UMC.Data.DataFactory.Instance().Password(SiteConfig.MD5Key(site.Root, this.Context.Token.UserId.Value, indexValue));
                        if (String.IsNullOrEmpty(cookie) == false)
                        {
                            this.Context.Send("Clipboard", new WebMeta().Put("text", cookie), true);
                        }
                        else
                        {
                            this.Prompt("您未对接此应用");
                        }
                    }
                    break;
            }
        }

    }
}