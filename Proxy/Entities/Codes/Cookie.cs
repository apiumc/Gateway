using System;
using System.Collections.Generic;
using UMC.Data;
namespace UMC.Proxy.Entities
{
    public partial class Cookie
    {
        readonly static Action<Cookie, object>[] _SetValues = new Action<Cookie, object>[] { (r, t) => r.Account = Reflection.ParseObject(t, r.Account), (r, t) => r.Badge = Reflection.ParseObject(t, r.Badge), (r, t) => r.ChangedTime = Reflection.ParseObject(t, r.ChangedTime), (r, t) => r.Config = Reflection.ParseObject(t, r.Config), (r, t) => r.Cookies = Reflection.ParseObject(t, r.Cookies), (r, t) => r.Domain = Reflection.ParseObject(t, r.Domain), (r, t) => r.IndexValue = Reflection.ParseObject(t, r.IndexValue), (r, t) => r.LoginTime = Reflection.ParseObject(t, r.LoginTime), (r, t) => r.Model = Reflection.ParseObject(t, r.Model), (r, t) => r.Time = Reflection.ParseObject(t, r.Time), (r, t) => r.user_id = Reflection.ParseObject(t, r.user_id) };
        readonly static string[] _Columns = new string[] { "Account", "Badge", "ChangedTime", "Config", "Cookies", "Domain", "IndexValue", "LoginTime", "Model", "Time", "user_id" };
        protected override void SetValue(string name, object obv)
        {
            var index = Utility.Search(_Columns, name, StringComparer.CurrentCultureIgnoreCase);
            if (index > -1) _SetValues[index](this, obv);
        }
        protected override void GetValues(Action<String, object> action)
        {
            AppendValue(action, "Account", this.Account);
            AppendValue(action, "Badge", this.Badge);
            AppendValue(action, "ChangedTime", this.ChangedTime);
            AppendValue(action, "Config", this.Config);
            AppendValue(action, "Cookies", this.Cookies);
            AppendValue(action, "Domain", this.Domain);
            AppendValue(action, "IndexValue", this.IndexValue);
            AppendValue(action, "LoginTime", this.LoginTime);
            AppendValue(action, "Model", this.Model);
            AppendValue(action, "Time", this.Time);
            AppendValue(action, "user_id", this.user_id);
        }

        protected override RecordColumn[] GetColumns()
        {
            var cols = new RecordColumn[11];
            cols[0] = RecordColumn.Column("Account", this.Account);
            cols[1] = RecordColumn.Column("Badge", this.Badge);
            cols[2] = RecordColumn.Column("ChangedTime", this.ChangedTime);
            cols[3] = RecordColumn.Column("Config", this.Config);
            cols[4] = RecordColumn.Column("Cookies", this.Cookies);
            cols[5] = RecordColumn.Column("Domain", this.Domain);
            cols[6] = RecordColumn.Column("IndexValue", this.IndexValue);
            cols[7] = RecordColumn.Column("LoginTime", this.LoginTime);
            cols[8] = RecordColumn.Column("Model", this.Model);
            cols[9] = RecordColumn.Column("Time", this.Time);
            cols[10] = RecordColumn.Column("user_id", this.user_id);
            return cols;
        }

    }
}

