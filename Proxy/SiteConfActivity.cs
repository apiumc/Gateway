using System;
using UMC.Web;
using UMC.Data.Entities;

namespace UMC.Proxy.Activities
{
    /// <summary>
    /// 邮箱账户
    /// </summary>
    [UMC.Web.Mapping("Proxy", "Conf", Auth = WebAuthType.User)]
    public class SiteConfActivity : WebActivity
    {
        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var mainKey = this.AsyncDialog("Key", g =>
            {
                this.Prompt("请传入KEY");
                return this.DialogValue("none");
            });
            var config = UMC.Data.DataFactory.Instance().Config(mainKey);
            var Conf = this.AsyncDialog("Conf", g =>
            {
                var title = "内容配置";
                if (mainKey.StartsWith("SITE_JS_CONFIG_"))
                {
                    title = "脚本配置";
                }
                var from5 = new UIFormDialog() { Title = title };
                from5.AddTextarea(title, "ConfValue", config?.ConfValue).Put("Rows", 20).NotRequired();

                from5.Submit("确认", "Mime.Config");
                return from5;

            });
            if (mainKey.StartsWith("SITE_") == false)
            {
                this.Prompt("只能配置站点相关内容");
            }
            var ConfValue = Conf["ConfValue"];

            Config platformConfig = new Config();
            platformConfig.ConfKey = mainKey;
            if (String.IsNullOrEmpty(ConfValue))
            {

                UMC.Data.DataFactory.Instance().Delete(platformConfig);
            }
            else
            {
                platformConfig.ConfValue = ConfValue;
                UMC.Data.DataFactory.Instance().Put(platformConfig);
            }
            this.Context.Send("Mime.Config", true);
        }
    }
}