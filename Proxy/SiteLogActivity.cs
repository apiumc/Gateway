using System;
using System.Collections.Generic;
using System.Linq;
using UMC.Web;

namespace UMC.Proxy.Activities
{
    /// <summary>
    /// 应用管理
    /// </summary>
    [UMC.Web.Mapping("Proxy", "Log", Auth = WebAuthType.User)]
  public  class SiteLogActivity : WebActivity
    {

        public override void ProcessActivity(WebRequest request, WebResponse response)
        {

            var Key = this.AsyncDialog("Key", g =>
            {

                var sts = new System.Data.DataTable();
                sts.Columns.Add("id");
                sts.Columns.Add("name");
                sts.Columns.Add("root");
                sts.Columns.Add("domain");
                sts.Columns.Add("module");
                sts.Columns.Add("auth");
                var keys = new List<String>();
                var ds = DataFactory.Instance().Site();

                var Keyword = (request.SendValues ?? request.Arguments)["Keyword"];
                if (String.IsNullOrEmpty(Keyword) == false)
                {

                    ds = ds.Where(r => r.Caption.Contains(Keyword) || r.Root.Contains(Keyword) || r.Domain.Contains(Keyword)).OrderBy(r => r.Caption).ToArray();
                }
                else
                {
                    ds = ds.OrderBy(r => r.Caption).ToArray();
                }

                foreach (var d in ds)
                {
                    sts.Rows.Add(d.SiteKey ?? UMC.Data.Utility.IntParse(UMC.Data.Utility.Guid(d.Root, true).Value), d.Caption, d.Root, (d.Domain.IndexOf(',') > 0 || d.Domain.IndexOf('\n') > 0) ? "多例均衡" : d.Domain, d.IsModule == true ? "模块" : "应用", d.AuthType ?? Web.WebAuthType.All);
                }

                var rdata = new WebMeta().Put("data", sts);
                response.Redirect(request.IsMaster ? rdata.Put("IsMaster", true) : rdata);
                return this.DialogValue("none");
            });

            var site = DataFactory.Instance().Site(Key);

            var data = new WebMeta();
            var caption = site.Caption;
            var vindex = caption.IndexOf("v.", StringComparison.CurrentCultureIgnoreCase);
            if (vindex > -1)
            {
                caption = caption.Substring(0, vindex);
            }
            data.Put("caption", $"{caption}的使用情况");
            var Users = this.AsyncDialog("User", g => this.DialogValue(this.Context.Token.Username)).Split(',');

            var webDate = new WebMeta();
            data.Put("data", webDate);
            var userManager = UMC.Security.Membership.Instance();
            var cookies = new List<String>();
            foreach (var u in Users)
            {
                var iden = userManager.Identity(u);
                if (iden == null)
                {
                    var cookie = DataFactory.Instance().Cookie(site.Root, UMC.Data.Utility.Guid(u, true).Value, 0);
                    if (cookie != null && String.IsNullOrEmpty(cookie.Account) == false)
                    {
                        webDate.Put(u, UMC.Data.Utility.GetDate(cookie.Time));
                    }
                    else
                    {
                        webDate.Put(u, "未使用");
                    }
                }
                else
                {
                    var cookie = DataFactory.Instance().Cookie(site.Root, iden.Id.Value, 0);
                    if (cookie != null)
                    {
                        webDate.Put(u, UMC.Data.Utility.GetDate(cookie.Time));
                    }
                    else
                    {
                        webDate.Put(u, "未使用");
                    }
                }
            }
            response.Redirect(data);


        }
    }
}