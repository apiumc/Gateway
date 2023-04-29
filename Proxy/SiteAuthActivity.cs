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
    [UMC.Web.Mapping("Proxy", "Auth", Auth = WebAuthType.Guest)]
    public class SiteAuthActivity : WebActivity
    {

        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var seesionKey = Utility.MD5(this.Context.Token.Device.Value);

            var sesion = UMC.Data.DataFactory.Instance().Session(this.Context.Token.Device.ToString());

            if (sesion != null)
            {
                sesion.SessionKey = seesionKey;
                UMC.Data.DataFactory.Instance().Put(sesion);
                response.Redirect(new WebMeta().Put("AuthKey", seesionKey));

            }

        }
    }
}