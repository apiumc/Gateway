using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.IO.Compression;
using UMC.Data;
using UMC.Net;
using UMC.Proxy.Entities;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Collections.Specialized;

namespace UMC.Proxy
{

    class LogSetting : UMC.Data.DataProvider
    {
        public static LogSetting Instance()
        {
            if (_Instance == null)
            {
                _Instance = new LogSetting();
                _Instance.LoadConf();
            }
            return _Instance;
        }

        static LogSetting _Instance;
        String _sql, _dbkey;
        bool _isWriter = false;
        string[] _csvFields;
        Uri _httpUrl;
        String _method;
        public virtual bool IsWriter
        {
            get
            {
                return _isWriter;
            }
        }
        public void LoadConf()
        {
            Data.Provider provider;
            var pc = Reflection.Configuration("assembly");
            if (pc != null)
            {
                provider = pc["Log"] ?? Data.Provider.Create("Log", "none");
            }
            else
            {
                provider = Data.Provider.Create("Log", "none");
            }

            if (this.csvWriter != null)
            {
                try
                {
                    this.csvWriter.Close();
                    this.csvWriter.Dispose();
                }
                catch
                {

                }
            }
            this.Provider = provider;
            switch (provider.Type)
            {
                case "csv":
                    this._isWriter = true;
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

                    this._csvFields = fs.ToArray();
                    break;
                case "json":
                    this._isWriter = true;
                    try
                    {
                        this._httpUrl = new Uri(provider["url"]);
                        this._method = provider["method"];
                    }
                    catch
                    {
                        this._isWriter = false;
                    }
                    break;
                case "sql":
                    var _table = provider["table"];
                    this._dbkey = provider["database"];
                    if (String.IsNullOrEmpty(_table) == false && String.IsNullOrEmpty(this._dbkey) == false)
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.Append("INSERT INTO ");
                        sb.Append(_table);
                        sb.Append("(");
                        var values = new System.Text.StringBuilder();
                        var dbp = Database.Instance(this._dbkey).DbProvider;
                        this._isWriter = true;
                        for (var i = 0; i < provider.Attributes.Count; i++)
                        {
                            var key = provider.Attributes.GetKey(i);
                            switch (key)
                            {
                                case "name":
                                case "type":
                                case "table":
                                case "database":
                                    break;
                                default:
                                    if (values.Length > 0)
                                    {
                                        values.Append(",");
                                        sb.Append(",");
                                    }
                                    values.Append("{");
                                    values.Append(provider.Attributes.Get(i));
                                    values.Append("}");
                                    sb.Append(dbp.QuotePrefix);
                                    sb.Append(key);
                                    sb.Append(dbp.QuoteSuffix);

                                    break;

                            }
                        }
                        sb.Append(")VALUES(");
                        sb.Append(values.ToString());
                        sb.Append(")");
                        this._sql = sb.ToString();
                    }
                    break;

            }
        }

        ConcurrentQueue<Web.WebMeta> _logMetas = new ConcurrentQueue<Web.WebMeta>();

        public virtual void Write(Web.WebMeta logMeta)
        {
            if (_isWriter)
            {
                _logMetas.Enqueue(logMeta);
                if (IsWriting == false)
                {
                    IsWriting = true;
                    System.Threading.Tasks.Task.Factory.StartNew(WriteLog);
                }

            }

        }
        void WriteJson(Web.WebMeta value, int time)
        {
            var req = _httpUrl.WebRequest();
            Action<NetHttpResponse> action = (xhr) =>
            {
                if (xhr.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    error = 0;
                }
                else
                {
                    xhr.ReadAsString(str =>
                    {
                        UMC.Data.Utility.Error("Proxy", DateTime.Now, value, str);

                    }, ex =>
                    {

                        UMC.Data.Utility.Error("Proxy", DateTime.Now, value, ex.ToString());
                    });
                    error++;
                }
                if (error > 100)
                {
                    _isWriter = false;
                }
                if (time > 200)
                {
                    IsWriting = false;
                }
                else if (_logMetas.TryDequeue(out value))
                {
                    this.WriteJson(value, time + 1);
                }
                else
                {

                    IsWriting = false;
                }
            };

            if (String.Equals("PUT", this._method))
            {
                req.Put(value, action);
            }
            else
            {

                req.Post(value, action);
            }


        }

        Regex regSql = new Regex("\\{(?<key>[\\w\\.\\$,\\[\\]_-]+)\\}");

        void WriteSQL(Web.WebMeta value)
        {
            var dv = value.GetDictionary();
            var ls = new List<Object>();
            var sql = regSql.Replace(_sql, r =>
            {
                var key = r.Groups["key"].Value;
                ls.Add(Get(key, dv));
                return "{" + (ls.Count - 1) + "}";
            });
            Database.Instance(_dbkey).Sqler().ExecuteNonQuery(sql, ls.ToArray());


        }
        int _Day;
        System.IO.StreamWriter csvWriter;
        void WriteCsv(Web.WebMeta value)
        {
            var now = DateTime.Now;
            if (_Day != now.Day)
            {
                _Day = now.Day;

                if (csvWriter != null)
                {
                    csvWriter.Close();
                    csvWriter.Dispose();
                }
                var file = UMC.Data.Reflection.ConfigPath(String.Format("Static\\log\\Proxy\\{0:yy-MM-dd}.log", now));
                if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(file)))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
                }
                this.csvWriter = new System.IO.StreamWriter(file, true);


            }

            var dv = value.GetDictionary();

            foreach (var f in this._csvFields)
            {
                CSV.CSVFormat(this.csvWriter, Get(f, dv));
                this.csvWriter.Write(',');
            }
            this.csvWriter.WriteLine();
            this.csvWriter.Flush();
        }
        object Get(String key, System.Collections.Hashtable value)
        {
            var em = value.GetEnumerator();
            while (em.MoveNext())
            {
                var k = em.Key as string;
                if (String.Equals(key, k, StringComparison.CurrentCultureIgnoreCase))
                {
                    return em.Value;
                }
            }
            return null;
        }
        int error = 0;
        bool IsWriting = false;
        void WriteLog()
        {
            Web.WebMeta value;
            switch (this.Provider.Type)
            {
                case "csv":
                case "sql":
                    while (_logMetas.TryDequeue(out value))
                    {
                        try
                        {
                            if (this.Provider.Type == "csv")
                            {
                                this.WriteCsv(value);
                            }
                            else
                            {
                                this.WriteSQL(value);
                            }
                            error = 0;
                        }
                        catch (Exception ex)
                        {
                            error++;
                            UMC.Data.Utility.Error("Proxy", DateTime.Now, value, ex.ToString());
                        }
                        if (error > 100)
                        {
                            _isWriter = false;
                            break;
                        }
                    }
                    IsWriting = false;
                    break;
                default:

                    if (_logMetas.TryDequeue(out value))
                    {
                        this.WriteJson(value, 0);
                    }
                    else
                    {

                        IsWriting = false;
                    }
                    break;
            }
        }
        public static void Instance(LogSetting logSetting, Provider provider)
        {
            logSetting.Provider = provider;
            _Instance = logSetting;
        }
    }
}

