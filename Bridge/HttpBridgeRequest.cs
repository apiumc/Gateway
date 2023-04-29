using System;

namespace UMC.Host
{

    class HttpBridgeRequest : HttpMimeRequest
    {
       
        HttpBridgeMime httpBridge;
        public HttpBridgeRequest(HttpBridgeMime mime) : base(mime)
        {
            this.httpBridge = mime;
        }
        public override void Receive(byte[] buffer, int offset, int size)
        {
            if (this.IsWebSocket)
            {
                this.httpBridge.WebSocket(buffer, offset, size);
            }
            else
            {
                base.Receive(buffer, offset, size);
            }
        }
    }
}

