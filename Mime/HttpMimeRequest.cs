using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using UMC.Data.Entities;
using UMC.Net;

namespace UMC.Host
{

    public class HttpMimeRequest : UMC.Net.HttpMimeBody, IDisposable
    {

        NameValueCollection _Headers = new NameValueCollection();
        public NameValueCollection Headers
        {
            get
            {
                return _Headers;
            }
        }
        public NameValueCollection Cookies
        {
            get
            {
                return _Cookies;
            }
        }
        NameValueCollection _Cookies = new NameValueCollection();
        internal HttpMime _context;
        public HttpMimeRequest(HttpMime context)
        {

            _context = context;
            this._remoteIpAddress = context.RemoteIpAddress;

        }
        public string HttpMethod
        {
            get;
            private set;
        }
        public string RawUrl
        {
            get;
            private set;
        }
        public string ContentType
        {
            get;
            private set;
        }

        Uri _Referer;
        public Uri UrlReferrer
        {
            get
            {
                if (_Referer == null)
                {
                    var referer = _Headers.Get("Referer");

                    if (String.IsNullOrEmpty(referer) == false)
                    {
                        try
                        {
                            _Referer = new Uri(referer);
                        }
                        catch
                        {
                            _Referer = new Uri(this._uri, "/");
                        }

                    }
                    else
                    {
                        _Referer = new Uri(this._uri, "/");
                    }
                }
                return _Referer;
            }
        }
        Uri _uri;
        public Uri Url
        {
            get { return this._uri; }
        }
        public void RewriteUrl(String pathAndQuery)
        {
            this._uri = new Uri(_uri, pathAndQuery);
        }
        String _remoteIpAddress;
        public string UserHostAddress
        {
            get { return _remoteIpAddress; }
        }
        bool _IsUpgrade, _isWebSocket, _isSubscribe;
        public bool IsSubscribe => _isSubscribe;


        public override bool IsWebSocket => _isWebSocket && _IsUpgrade;
        protected override void Header(byte[] data, int offset, int size)
        {
            var utf = System.Text.Encoding.UTF8;
            var start = offset;
            var host = "";
            var scheme = _context.Scheme;
            for (var ci = 0; ci < size - 2; ci++)
            {
                var index = ci + offset;

                if (data[index] == 10 && data[index - 1] == 13)
                {
                    var heaerValue = utf.GetString(data, start, index - start - 1);
                    if (start == offset)
                    {
                        var ls = heaerValue.Split(' ');
                        if (ls.Length == 3)
                        {
                            this.HttpMethod = ls[0];
                            this.RawUrl = ls[1];
                            if (ls[2].StartsWith("HTTP/") == false)
                            {
                                this.IsHttpFormatError = true;
                                return;
                            }
                        }
                        else
                        {
                            this.IsHttpFormatError = true;
                            return;
                        }

                    }
                    else
                    {
                        var vi = heaerValue.IndexOf(':');
                        var key = heaerValue.Substring(0, vi);
                        var value = heaerValue.Substring(vi + 2);

                        switch (key.ToLower())
                        {
                            case "x-forwarded-host":
                            case "x-client-host":
                                host = value;
                                break;
                            case "umc-request-protocol":
                                _isSubscribe = String.Equals(value, "Subscribe", StringComparison.CurrentCultureIgnoreCase);
                                this._Headers.Add(key, value);
                                break;
                            case "host":
                                this._Headers.Add(key, value);
                                if (String.IsNullOrEmpty(host))
                                    host = value;
                                break;
                            case "x-real-ip":
                            case "x-forwarded-for":
                            case "x-client-ip":
                                this._remoteIpAddress = value;
                                break;
                            case "x-forwarded-proto":
                            case "x-client-scheme":
                                scheme = value;
                                break;
                            case "connection":
                                _IsUpgrade = String.Equals(value, "upgrade", StringComparison.CurrentCultureIgnoreCase);
                                this._Headers.Add(key, value);
                                break;
                            case "upgrade":
                                _isWebSocket = String.Equals(value, "websocket", StringComparison.CurrentCultureIgnoreCase);
                                this._Headers.Add(key, value);
                                break;
                            case "cookie":
                                this._Headers.Add(key, value);
                                var cs = value.Split("; ");
                                foreach (var c in cs)
                                {
                                    var kc = c.IndexOf('=');
                                    if (kc > 0)
                                    {
                                        _Cookies[c.Substring(0, kc)] = c.Substring(kc + 1);
                                    }
                                    else
                                    {
                                        _Cookies.Add(c, null);
                                    }
                                }
                                break;
                            case "content-type":
                                this._Headers.Add(key, value);
                                this.ContentType = value;
                                break;
                            default:
                                this._Headers.Add(key, value);
                                break;
                        }
                    }

                    start = index + 1;
                }
            }
            if (String.IsNullOrEmpty(host))
            {
                host = _context.Host;
            }
            switch (scheme)
            {
                case "https":
                case "http":
                    this._uri = new Uri($"{scheme}://{host}{this.RawUrl}");
                    break;
                default:
                    this._uri = new Uri($"http://{host}{this.RawUrl}");
                    break;
            }
            _context.PrepareRespone(this);
        }
        byte[] _lastFormBuffer;
        int lastFormBufferSize = 0;
        String FormKey;
        NameValueCollection _from = new NameValueCollection();

        public void ReadAsForm(Action<NameValueCollection> action)
        {
            if (this.ContentType?.Contains("form-urlencoded", StringComparison.CurrentCultureIgnoreCase) == true)
            {
                _lastFormBuffer = new byte[0x100];
                this.ReadAsData((b, i, c) =>
                {
                    if (b.Length == 0)
                    {
                        if (i == 0 && c == 0)
                        {
                            this.FormValue(b, i, c);
                        }
                        action(_from);
                    }
                    else
                    {
                        this.FormValue(b, i, c);
                    }
                });
            }
            else
            {

                action(_from);
            }
        }

        public NameValueCollection Form
        {
            get
            {
                return _from;
            }
        }
        void FormValue(byte[] data, int offset, int size)
        {
            for (int i = 0; i < size; i++)
            {
                switch (data[offset + i])
                {
                    case 0x26:
                        String value = null;
                        if (lastFormBufferSize > 0)
                        {
                            value = System.Text.Encoding.UTF8.GetString(System.Web.HttpUtility.UrlDecodeToBytes(_lastFormBuffer, 0, lastFormBufferSize));

                        }
                        if (String.IsNullOrEmpty(FormKey) == false)
                        {
                            _from.Add(FormKey, value);

                        }
                        else if (String.IsNullOrEmpty(value) == false)
                        {

                            _from.Add(value, null);
                        }
                        lastFormBufferSize = 0;
                        break;
                    case 0x3d:
                        FormKey = lastFormBufferSize == 0 ? String.Empty : System.Text.Encoding.UTF8.GetString(System.Web.HttpUtility.UrlDecodeToBytes(_lastFormBuffer, 0, lastFormBufferSize));


                        lastFormBufferSize = 0;
                        break;
                    default:
                        if (lastFormBufferSize == _lastFormBuffer.Length)
                        {
                            var b = new byte[lastFormBufferSize + 0x100];
                            Array.Copy(_lastFormBuffer, 0, b, 0, lastFormBufferSize);
                            _lastFormBuffer = b;
                        }
                        _lastFormBuffer[lastFormBufferSize] = data[offset + i];
                        lastFormBufferSize++;
                        break;
                }
            }
            if (offset == 0 && size == 0 && data.Length == 0)
            {
                String value = null;
                if (lastFormBufferSize > 0)
                {
                    value = System.Text.Encoding.UTF8.GetString(System.Web.HttpUtility.UrlDecodeToBytes(_lastFormBuffer, 0, lastFormBufferSize));

                }
                if (String.IsNullOrEmpty(FormKey) == false)
                {
                    _from.Add(FormKey, value);

                }
                else if (String.IsNullOrEmpty(value) == false)
                {

                    _from.Add(value, null);
                }
                lastFormBufferSize = 0;
            }
        }
        class Bufer
        {
            public byte[] bytes;
            public int size;
        }
        Queue<Bufer> _body = new Queue<Bufer>();
        Net.NetReadData _readData;
        protected override void Body(byte[] data, int offset, int size)
        {
            if (size > 0)
            {
                if (_readData != null)
                {
                    while (this._body.Count > 0)
                    {
                        var d = _body.Dequeue();
                        _readData(d.bytes, 0, d.size);
                    }
                    _readData(data, offset, size);
                }
                else
                {
                    var d = new byte[size];
                    Array.Copy(data, offset, d, 0, size);
                    _body.Enqueue(new Bufer { bytes = d, size = size });
                }

            }
        }

        Exception _Error;
        protected override void ReceiveError(Exception ex)
        {
            _Error = ex;
            //lock (_sysc)
            {
                if (_readData != null)
                {
                    while (this._body.Count > 0)
                    {
                        var d = _body.Dequeue();
                        _readData(d.bytes, 0, d.size);

                    }
                    _readData(Array.Empty<byte>(), -1, 0);
                }
            }
            _context.OutputFinish();


        }
        public bool IsReadBody
        {
            get;
            private set;
        }
        public void ReadAsData(Net.NetReadData readData)
        {
            if (IsReadBody == false)
            {
                IsReadBody = true;
                //lock (_sysc)
                {
                    if (this.IsHttpFormatError)
                    {
                        readData(Array.Empty<byte>(), -1, 0);
                    }
                    else if (this.isBodyFinish)
                    {
                        while (this._body.Count > 0)
                        {
                            var d = _body.Dequeue();
                            readData(d.bytes, 0, d.size);

                        }

                        readData(Array.Empty<byte>(), 0, 0);
                    }
                    else
                    {
                        this._readData = readData;
                    }
                }

            }
            else
            {
                readData(Array.Empty<byte>(), this.IsHttpFormatError ? -1 : 0, 0);
            }
        }
        
        protected override void MimeBody(byte[] data, int offset, int size)
        {

        }
        public override void Finish()
        {
            //lock (_sysc)
            {
                this.isBodyFinish = true;
                if (_readData != null)
                {

                    while (this._body.Count > 0)
                    {
                        var d = _body.Dequeue();
                        _readData(d.bytes, 0, d.size);

                    }
                    _readData(Array.Empty<byte>(), 0, 0);

                }

            }

        }

        public void Dispose()
        {
            this._body.Clear();
        }

        bool isBodyFinish = false;
    }
}
