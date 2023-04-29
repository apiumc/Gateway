using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections;
using UMC.Web;
using UMC.Data.Entities;

namespace UMC.Proxy.Activities
{

    [UMC.Web.Mapping("Proxy", "User", Auth = WebAuthType.User)]
    public class SiteUserActivity : WebActivity
    {


        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var strUser = this.AsyncDialog("Id", d =>
            {
                response.Redirect("Settings", "User");
                return this.DialogValue("none");
            });
            //System.Net.Q
            var site = UMC.Data.Utility.IntParse(this.AsyncDialog("Site", "0"), 0);

            var userId = UMC.Data.Utility.Guid(strUser) ?? Guid.Empty;

            var setting = Web.UIDialog.AsyncDialog(this.Context, "Setting", d =>
            {
                var form = request.SendValues ?? new WebMeta();
                if (form.ContainsKey("limit") == false)
                {
                    this.Context.Send(new UISectionBuilder(request.Model, request.Command, request.Arguments)
                        .RefreshEvent($"{request.Model}.{request.Command}")
                        .Builder(), true);

                }
                var user = Data.DataFactory.Instance().User(userId);


                var ui = UISection.Create(new UITitle("应用账户"));

                ui.AddCell("别名", user.Alias);


                ui.AddCell("账户", user.Username);

                if (String.IsNullOrEmpty(Data.DataFactory.Instance().Password(user.Id.Value)))
                {
                    ui.AddCell("密码登录", "未开启");
                }
                else
                {
                    ui.AddCell("密码登录", "已开启");
                }

                if (user.ActiveTime.HasValue)
                    ui.AddCell("最后登录", String.Format("{0:yy-MM-dd HH:mm}", user.ActiveTime));
                if (user.RegistrTime.HasValue)
                    ui.AddCell("注册时间", String.Format("{0:yy-MM-dd HH:mm}", user.RegistrTime));


                var status = "正常";
                var flags = user.Flags ?? UMC.Security.UserFlags.Normal;
                var opts = new Web.ListItemCollection();
                if (user.IsDisabled == true)
                {
                    status = "禁用";
                }
                else if ((int)(flags & UMC.Security.UserFlags.Lock) > 0)
                {
                    status = "锁定";
                }
                ui.NewSection().AddCell("状态", status)
                .AddCell("口令", String.IsNullOrEmpty(Data.DataFactory.Instance().Password(user.Id.Value)) ? "未开通" : "已开启");




                var roes = Data.DataFactory.Instance().Roles(user.Id.Value, site);

                var ui2 = ui.NewSection();
                ui2.AddCell("应用角色", "设置", new UIClick(new WebMeta(request.Arguments).Put(d, "Role")).Send(request.Model, request.Command));
                foreach (var dr in roes)
                {
                    switch (dr)
                    {
                        case UMC.Security.Membership.GuestRole:
                            break;
                        case UMC.Security.Membership.AdminRole:
                            ui2.Delete(UICell.UI('\uf0c0', "超级管理员", dr), new UIEventText()
                            .Click(new UIClick(new WebMeta(request.Arguments).Put(d, "Rolename").Put("Rolename", dr).Put("Site", site)).Send(request.Model, request.Command)));

                            break;
                        case UMC.Security.Membership.UserRole:
                            ui2.Delete(UICell.UI('\uf0c0', "内部员工", dr), new UIEventText()
                           .Click(new UIClick(new WebMeta(request.Arguments).Put(d, "Rolename").Put("Rolename", dr).Put("Site", site)).Send(request.Model, request.Command)));

                            break;
                        default:
                            ui2.Delete(UICell.UI('\uf0c0', dr, ""), new UIEventText()
                           .Click(new UIClick(new WebMeta(request.Arguments).Put(d, "Rolename").Put("Rolename", dr).Put("Site", site)).Send(request.Model, request.Command)));

                            break;
                    }
                }
                if (ui2.Length == 1)
                {
                    ui2.Add("Desc", new UMC.Web.WebMeta().Put("desc", "只拥有来宾角色").Put("icon", "\uF016"), new UMC.Web.WebMeta().Put("desc", "{icon}\n{desc}"),

                    new UIStyle().Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60)));


                }

                var Organize = UMC.Data.DataFactory.Instance().Organizes(new User { Id = user.Id.Value });

                var ui4 = ui.NewSection();


                ui4.AddCell("所属组织", "");


                if (Organize.Length > 0)
                {
                    foreach (var s in Organize)
                    {
                        ui4.Add(UICell.Create("UI", new WebMeta().Put("text", s.Caption).Put("Icon", "\uf0e8")));

                    }
                }
                else
                {
                    ui4.Add("Desc", new UMC.Web.WebMeta().Put("desc", "未加入组织").Put("icon", "\uf0e8"), new UMC.Web.WebMeta().Put("desc", "{icon}\n{desc}"),

                    new UIStyle().Align(1).Color(0xaaa).Padding(20, 20).BgColor(0xfff).Size(12).Name("icon", new UIStyle().Font("wdk").Size(60)));


                }
                var dSite = DataFactory.Instance().Site(site);
                var scookies = DataFactory.Instance().Cookies(dSite.Root, user.Id.Value).Where(r => String.IsNullOrEmpty(r.Account) == false).OrderBy(r => r.IndexValue).ToArray();

                if (scookies.Length > 0)
                {
                    var um = ui.NewSection();
                    um.Header.Put("text", "对接账户");

                    foreach (var ac in scookies)
                    {

                        if (ac.LoginTime.HasValue)
                        {
                            um.AddCell('\uf1bb', ac.Account, UMC.Data.Utility.GetDate(UMC.Data.Utility.TimeSpan(ac.LoginTime.Value)));

                        }
                        else
                        {

                            um.AddCell('\uf1bb', ac.Account, String.Empty);
                        }
                        break;

                    }
                }
                var sess = UMC.Data.DataFactory.Instance().Session(user.Id.Value)
                .Where(r => String.Equals("Settings", r.ContentType) == false).ToArray();

                if (sess.Length > 0)
                {
                    var ui5 = ui.NewSection();
                    ui5.Header.Put("text", "登录会话");
                    foreach (var s in sess)
                    {
                        ui5.Add(UICell.Create("UI", new WebMeta().Put("value", UMC.Data.Utility.GetDate(s.UpdateTime), "text", s.ContentType)
                    .Put("Icon", "\uf286")));
                    }
                }
                ui.NewSection().AddCell('\uEA05', "功能授权", String.Empty, new UIClick(new WebMeta(request.Arguments).Put(d, "Auth")).Send(request.Model, request.Command));


                response.Redirect(ui);

                return this.DialogValue("none");
            });
            if (request.IsMaster == false)
            {
                var rols = UMC.Data.DataFactory.Instance().Roles(this.Context.Token.UserId.Value, site);
                if (rols.Contains(UMC.Security.Membership.AdminRole) == false)
                {
                    this.Prompt("需要管理员权限才能设置");
                }

            }
            switch (setting)
            {
                case "Rolename":
                    {
                        var Rolename = this.AsyncDialog("Rolename", r => this.DialogValue("none"));
                        Data.DataFactory.Instance().Delete(new UserToRole { user_id = userId, Rolename = Rolename, Site = site });

                        this.Context.Send($"{request.Model}.{request.Command}", true);
                    }
                    break;
                case "Role":
                    {

                        var rolename = this.AsyncDialog("Rolename", "Settings", "SelectRole", new WebMeta().Put("Site", site));
                        Data.DataFactory.Instance().Put(new UserToRole
                        {
                            user_id = userId,
                            Site = site,
                            Rolename = rolename
                        });

                        this.Context.Send($"{request.Model}.{request.Command}", true);
                    }
                    break;
                case "Auth":
                    {
                        var user = Data.DataFactory.Instance().User(userId);
                        response.Redirect("Settings", "Auth", new UMC.Web.WebMeta().Put("Type", "User", "Value", user.Username).Put("Site", site), true);
                    }
                    break;
            }

        }

    }
}