using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UMC.Data;
using UMC.Net;

namespace UMC.Host
{
    public class HttpMimeContext : UMC.Net.NetContext
    {
        public override string Server => Environment.MachineName;
        HttpMimeRequest _request;
        HttpMimeResponse _response;
        public override long? ContentLength { get => _request.ContentLength; set => _response.ContentLength = value; }
        public override void AppendCookie(string name, string value)
        {
            _response.AppendCookie(name, value);

        }
        public override void AppendCookie(string name, string value, string path)
        {

            _response.AppendCookie(name, value, path);
        }
        public override bool IsWebSocket => _request.IsWebSocket;
        Net.TextWriter writer;
        public override void RewriteUrl(string pathAndQuery)
        {
            _request.RewriteUrl(pathAndQuery);
        }
        public HttpMimeContext(HttpMimeRequest request, HttpMimeResponse response)
        {
            this._request = request;
            this._response = response;
            writer = new Net.TextWriter(response.OutputStream.Write);
        }
        public override void ReadAsData(NetReadData readData)
        {
            if (_request.ContentLength > 0 && _request.IsMimeFinish == false)
            {
                if (aseSynchronousIOEnd == null)
                {
                    aseSynchronousIOEnd = () => { };
                }
            }
            this._request.ReadAsData(readData);
        }
        Action aseSynchronousIOEnd;
        public override bool AllowSynchronousIO => aseSynchronousIOEnd != null;
        public override void UseSynchronousIO(Action action)
        {
            aseSynchronousIOEnd = action;
        }
        void UseSynchronousIOEnd()
        {
            try
            {
                aseSynchronousIOEnd();
            }
            catch (Exception ex)
            {

                UMC.Data.Utility.Error("SynchronousIO", DateTime.Now, ex.ToString());
            }
        }
        public override void OutputFinish()
        {

            if (aseSynchronousIOEnd != null)
            {
                this.Output.Flush();
                if (_response.OutputFinish())
                {
                    UseSynchronousIOEnd();
                    _request._context.OutputFinish();
                }
                else
                {
                    _request._context.Dispose();
                }

                _request.Dispose();
            }

        }
        public override void Error(Exception ex)
        {
            this.Output.Flush();
            _response.OutputError(ex);
            if (aseSynchronousIOEnd != null)
            {
                UseSynchronousIOEnd();

            }
            _request._context.Dispose();
            _request.Dispose();

        }
        public override void ReadAsForm(Action<NameValueCollection> action)
        {
            if (_request.ContentLength > 0 && _request.IsMimeFinish == false)
            {
                if (aseSynchronousIOEnd == null)
                {
                    aseSynchronousIOEnd = () => { };
                }
            }
            _request.ReadAsForm(action);
        }
        public virtual void ProcessRequest()
        {
            new UMC.Proxy.WebServlet().ProcessRequest(this);
            //this.ProcessAfter();
        }
        internal protected virtual void ProcessAfter()
        {
            if (this.IsWebSocket == false && aseSynchronousIOEnd == null)
            {
                this.Output.Flush();
                _response.OutputFinish();
                _request._context.OutputFinish();

                _request.Dispose();
            }
        }
        public override int StatusCode
        {
            get
            {
                return this._response.StatusCode;
            }
            set
            {
                this._response.StatusCode = value;
            }
        }
        public override void AddHeader(string name, string value)
        {
            this._response.AddHeader(name, value);
        }
        public override NameValueCollection Headers
        {
            get
            {
                return _request.Headers;
            }
        }
        public override NameValueCollection Cookies
        {
            get
            {

                return _request.Cookies; ;
            }
        }
        NameValueCollection _QueryString;
        public override NameValueCollection QueryString
        {
            get
            {
                if (_QueryString == null)
                {
                    var Query = this.Url.Query;
                    if (String.IsNullOrEmpty(Query) == false)
                    {
                        _QueryString = System.Web.HttpUtility.ParseQueryString(Query);
                    }
                    else
                    {
                        _QueryString = new NameValueCollection();
                    }
                }
                return _QueryString;
            }
        }

        public override System.IO.TextWriter Output
        {
            get
            {
                return this.writer;
            }
        }
        public override System.IO.Stream OutputStream
        {
            get
            {
                return this._response.OutputStream;
            }
        }

        public override string ContentType
        {
            get
            {
                return this._request.ContentType;
            }
            set
            {
                this._response.ContentType = value;
            }
        }

        public override string UserHostAddress
        {
            get { return this._request.UserHostAddress; }
        }

        public override string RawUrl
        {
            get { return _request.RawUrl; }
        }

        public override string UserAgent
        {
            get { return this._request.Headers["User-Agent"]; }
        }
        Uri _Referer;
        public override Uri UrlReferrer
        {
            get
            {
                if (_Referer == null)
                {
                    String referer = _request.Headers["Referer"];
                    if (String.IsNullOrEmpty(referer) == false)
                    {
                        try
                        {
                            _Referer = new Uri(referer);
                        }
                        catch
                        {
                            _Referer = new Uri(_request.Url, "/");
                        }

                    }
                }
                return _Referer;
            }
        }

        public override Uri Url
        {
            get { return _request.Url; }
        }

        public override void Redirect(string url)
        {
            this._response.Redirect(url);
        }

        public override string HttpMethod
        {
            get { return this._request.HttpMethod; }
        }
    }
}
