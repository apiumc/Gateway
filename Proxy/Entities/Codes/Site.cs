using System;
using System.Collections.Generic;
using UMC.Data;
namespace UMC.Proxy.Entities
{
    public partial class Site
    {
        readonly static Action<Site, object>[] _SetValues = new Action<Site, object>[] { (r, t) => r.Account = Reflection.ParseObject(t, r.Account), (r, t) => r.AdminConf = Reflection.ParseObject(t, r.AdminConf), (r, t) => r.AppendJSConf = Reflection.ParseObject(t, r.AppendJSConf), (r, t) => r.AppSecret = Reflection.ParseObject(t, r.AppSecret), (r, t) => r.AuthConf = Reflection.ParseObject(t, r.AuthConf), (r, t) => r.AuthExpire = Reflection.ParseObject(t, r.AuthExpire), (r, t) => r.AuthType = Reflection.ParseObject(t, r.AuthType), (r, t) => r.Caption = Reflection.ParseObject(t, r.Caption), (r, t) => r.Conf = Reflection.ParseObject(t, r.Conf), (r, t) => r.Domain = Reflection.ParseObject(t, r.Domain), (r, t) => r.Flag = Reflection.ParseObject(t, r.Flag), (r, t) => r.HeaderConf = Reflection.ParseObject(t, r.HeaderConf), (r, t) => r.HelpKey = Reflection.ParseObject(t, r.HelpKey), (r, t) => r.Home = Reflection.ParseObject(t, r.Home), (r, t) => r.Host = Reflection.ParseObject(t, r.Host), (r, t) => r.HostModel = Reflection.ParseObject(t, r.HostModel), (r, t) => r.HostReConf = Reflection.ParseObject(t, r.HostReConf), (r, t) => r.ImagesConf = Reflection.ParseObject(t, r.ImagesConf), (r, t) => r.IsAuth = Reflection.ParseObject(t, r.IsAuth), (r, t) => r.IsDebug = Reflection.ParseObject(t, r.IsDebug), (r, t) => r.IsDesktop = Reflection.ParseObject(t, r.IsDesktop), (r, t) => r.IsModule = Reflection.ParseObject(t, r.IsModule), (r, t) => r.LogConf = Reflection.ParseObject(t, r.LogConf), (r, t) => r.LogoutPath = Reflection.ParseObject(t, r.LogoutPath), (r, t) => r.MobileHome = Reflection.ParseObject(t, r.MobileHome), (r, t) => r.ModifyTime = Reflection.ParseObject(t, r.ModifyTime), (r, t) => r.OpenModel = Reflection.ParseObject(t, r.OpenModel), (r, t) => r.OutputCookies = Reflection.ParseObject(t, r.OutputCookies), (r, t) => r.RedirectPath = Reflection.ParseObject(t, r.RedirectPath), (r, t) => r.Root = Reflection.ParseObject(t, r.Root), (r, t) => r.SiteKey = Reflection.ParseObject(t, r.SiteKey), (r, t) => r.SLB = Reflection.ParseObject(t, r.SLB), (r, t) => r.StaticConf = Reflection.ParseObject(t, r.StaticConf), (r, t) => r.Time = Reflection.ParseObject(t, r.Time), (r, t) => r.Timeout = Reflection.ParseObject(t, r.Timeout), (r, t) => r.Type = Reflection.ParseObject(t, r.Type), (r, t) => r.UserBrowser = Reflection.ParseObject(t, r.UserBrowser), (r, t) => r.UserModel = Reflection.ParseObject(t, r.UserModel), (r, t) => r.Version = Reflection.ParseObject(t, r.Version) };
        readonly static string[] _Columns = new string[] { "Account", "AdminConf", "AppendJSConf", "AppSecret", "AuthConf", "AuthExpire", "AuthType", "Caption", "Conf", "Domain", "Flag", "HeaderConf", "HelpKey", "Home", "Host", "HostModel", "HostReConf", "ImagesConf", "IsAuth", "IsDebug", "IsDesktop", "IsModule", "LogConf", "LogoutPath", "MobileHome", "ModifyTime", "OpenModel", "OutputCookies", "RedirectPath", "Root", "SiteKey", "SLB", "StaticConf", "Time", "Timeout", "Type", "UserBrowser", "UserModel", "Version" };
        protected override void SetValue(string name, object obv)
        {
            var index = Utility.Search(_Columns, name, StringComparer.CurrentCultureIgnoreCase);
            if (index > -1) _SetValues[index](this, obv);
        }
        protected override void GetValues(Action<String, object> action)
        {
            AppendValue(action, "Account", this.Account);
            AppendValue(action, "AdminConf", this.AdminConf);
            AppendValue(action, "AppendJSConf", this.AppendJSConf);
            AppendValue(action, "AppSecret", this.AppSecret);
            AppendValue(action, "AuthConf", this.AuthConf);
            AppendValue(action, "AuthExpire", this.AuthExpire);
            AppendValue(action, "AuthType", this.AuthType);
            AppendValue(action, "Caption", this.Caption);
            AppendValue(action, "Conf", this.Conf);
            AppendValue(action, "Domain", this.Domain);
            AppendValue(action, "Flag", this.Flag);
            AppendValue(action, "HeaderConf", this.HeaderConf);
            AppendValue(action, "HelpKey", this.HelpKey);
            AppendValue(action, "Home", this.Home);
            AppendValue(action, "Host", this.Host);
            AppendValue(action, "HostModel", this.HostModel);
            AppendValue(action, "HostReConf", this.HostReConf);
            AppendValue(action, "ImagesConf", this.ImagesConf);
            AppendValue(action, "IsAuth", this.IsAuth);
            AppendValue(action, "IsDebug", this.IsDebug);
            AppendValue(action, "IsDesktop", this.IsDesktop);
            AppendValue(action, "IsModule", this.IsModule);
            AppendValue(action, "LogConf", this.LogConf);
            AppendValue(action, "LogoutPath", this.LogoutPath);
            AppendValue(action, "MobileHome", this.MobileHome);
            AppendValue(action, "ModifyTime", this.ModifyTime);
            AppendValue(action, "OpenModel", this.OpenModel);
            AppendValue(action, "OutputCookies", this.OutputCookies);
            AppendValue(action, "RedirectPath", this.RedirectPath);
            AppendValue(action, "Root", this.Root);
            AppendValue(action, "SiteKey", this.SiteKey);
            AppendValue(action, "SLB", this.SLB);
            AppendValue(action, "StaticConf", this.StaticConf);
            AppendValue(action, "Time", this.Time);
            AppendValue(action, "Timeout", this.Timeout);
            AppendValue(action, "Type", this.Type);
            AppendValue(action, "UserBrowser", this.UserBrowser);
            AppendValue(action, "UserModel", this.UserModel);
            AppendValue(action, "Version", this.Version);
        }

        protected override RecordColumn[] GetColumns()
        {
            var cols = new RecordColumn[39];
            cols[0] = RecordColumn.Column("Account", this.Account);
            cols[1] = RecordColumn.Column("AdminConf", this.AdminConf);
            cols[2] = RecordColumn.Column("AppendJSConf", this.AppendJSConf);
            cols[3] = RecordColumn.Column("AppSecret", this.AppSecret);
            cols[4] = RecordColumn.Column("AuthConf", this.AuthConf);
            cols[5] = RecordColumn.Column("AuthExpire", this.AuthExpire);
            cols[6] = RecordColumn.Column("AuthType", this.AuthType);
            cols[7] = RecordColumn.Column("Caption", this.Caption);
            cols[8] = RecordColumn.Column("Conf", this.Conf);
            cols[9] = RecordColumn.Column("Domain", this.Domain);
            cols[10] = RecordColumn.Column("Flag", this.Flag);
            cols[11] = RecordColumn.Column("HeaderConf", this.HeaderConf);
            cols[12] = RecordColumn.Column("HelpKey", this.HelpKey);
            cols[13] = RecordColumn.Column("Home", this.Home);
            cols[14] = RecordColumn.Column("Host", this.Host);
            cols[15] = RecordColumn.Column("HostModel", this.HostModel);
            cols[16] = RecordColumn.Column("HostReConf", this.HostReConf);
            cols[17] = RecordColumn.Column("ImagesConf", this.ImagesConf);
            cols[18] = RecordColumn.Column("IsAuth", this.IsAuth);
            cols[19] = RecordColumn.Column("IsDebug", this.IsDebug);
            cols[20] = RecordColumn.Column("IsDesktop", this.IsDesktop);
            cols[21] = RecordColumn.Column("IsModule", this.IsModule);
            cols[22] = RecordColumn.Column("LogConf", this.LogConf);
            cols[23] = RecordColumn.Column("LogoutPath", this.LogoutPath);
            cols[24] = RecordColumn.Column("MobileHome", this.MobileHome);
            cols[25] = RecordColumn.Column("ModifyTime", this.ModifyTime);
            cols[26] = RecordColumn.Column("OpenModel", this.OpenModel);
            cols[27] = RecordColumn.Column("OutputCookies", this.OutputCookies);
            cols[28] = RecordColumn.Column("RedirectPath", this.RedirectPath);
            cols[29] = RecordColumn.Column("Root", this.Root);
            cols[30] = RecordColumn.Column("SiteKey", this.SiteKey);
            cols[31] = RecordColumn.Column("SLB", this.SLB);
            cols[32] = RecordColumn.Column("StaticConf", this.StaticConf);
            cols[33] = RecordColumn.Column("Time", this.Time);
            cols[34] = RecordColumn.Column("Timeout", this.Timeout);
            cols[35] = RecordColumn.Column("Type", this.Type);
            cols[36] = RecordColumn.Column("UserBrowser", this.UserBrowser);
            cols[37] = RecordColumn.Column("UserModel", this.UserModel);
            cols[38] = RecordColumn.Column("Version", this.Version);
            return cols;
        }

    }
}

