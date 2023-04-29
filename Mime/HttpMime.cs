using System;
using System.Net;
using System.Threading.Tasks;
using UMC.Net;

namespace UMC.Host
{

    public abstract class HttpMime : IDisposable
    {
        public virtual string Scheme => "http";
        public abstract void OutputFinish();
        public abstract void Write(byte[] buffer, int offset, int count);


        public abstract void Dispose();
        public abstract String Host { get; }
        public abstract String RemoteIpAddress { get; }

        
        protected abstract void WebSocket(UMC.Net.NetContext context);
        
        public abstract void Subscribe(HttpMimeRequest webRequest);
        public virtual void PrepareRespone(HttpMimeRequest httpMimeRequest)
        {

            Task.Factory.StartNew(() =>
            {
                if (httpMimeRequest.IsWebSocket)
                {
                    if (httpMimeRequest.IsSubscribe)
                    {
                        Subscribe(httpMimeRequest);
                    }
                    else
                    {
                        var context = new HttpMimeContext(httpMimeRequest, new HttpMimeResponse(this, httpMimeRequest));
                        try
                        {
                            context.ProcessRequest();
                            context.ProcessAfter();

                            this.WebSocket(context);
                           
                        }
                        catch (Exception ex)
                        {
                            context.Error(ex);

                        }

                    }
                }
                else
                {
                    var context = new HttpMimeContext(httpMimeRequest, new HttpMimeResponse(this, httpMimeRequest));
                    try
                    {
                        context.ProcessRequest();
                        context.ProcessAfter();
                    }
                    catch (Exception ex)
                    {
                        context.Error(ex);

                    }
                }
            });

        }

        public void OutText(int status, string contentType, String text)
        {
            var writer = new TextWriter(this.Write);
            writer.Write($"HTTP/1.1 {status} {HttpStatusDescription.Get(status)}\r\n");
            writer.Write($"Content-Type: {contentType}; charset=utf-8\r\n");
            writer.Write($"Content-Length: {System.Text.Encoding.UTF8.GetByteCount(text)}\r\n");
            writer.Write("Connection: close\r\n");
            writer.Write("Server: UMC.Proxy\r\n\r\n");
            writer.Write(text);
            writer.Flush();
            writer.Close();
            this.Dispose();
        }
        public void OutText(int status, String text)
        {
            this.OutText(status, "text/plain", text);

        }
    }
}

