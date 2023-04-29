using System;
using System.Collections.Generic;
using UMC.Data;
namespace UMC.Proxy.Entities
{
    public partial class SiteHost
    {
        readonly static Action<SiteHost, object>[] _SetValues = new Action<SiteHost, object>[] { (r, t) => r.Host = Reflection.ParseObject(t, r.Host), (r, t) => r.Root = Reflection.ParseObject(t, r.Root), (r, t) => r.Scheme = Reflection.ParseObject(t, r.Scheme) };
        readonly static string[] _Columns = new string[] { "Host", "Root", "Scheme" };
        protected override void SetValue(string name, object obv)
        {
            var index = Utility.Search(_Columns, name, StringComparer.CurrentCultureIgnoreCase);
            if (index > -1) _SetValues[index](this, obv);
        }
        protected override void GetValues(Action<String, object> action)
        {
            AppendValue(action, "Host", this.Host);
            AppendValue(action, "Root", this.Root);
            AppendValue(action, "Scheme", this.Scheme);
        }

        protected override RecordColumn[] GetColumns()
        {
            var cols = new RecordColumn[3];
            cols[0] = RecordColumn.Column("Host", this.Host);
            cols[1] = RecordColumn.Column("Root", this.Root);
            cols[2] = RecordColumn.Column("Scheme", this.Scheme);
            return cols;
        }

    }
}

