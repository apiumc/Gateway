using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using UMC.Net;
using System.Net.WebSockets;

namespace UMC.Host
{
    class HttpWebSocket
    {

        internal static string GetSecWebSocketAcceptString(string secWebSocketKey)
        {
            string s = secWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            byte[] inArray = System.Security.Cryptography.SHA1.HashData(bytes);
            return Convert.ToBase64String(inArray);
        }

        internal static bool ProcessWebSocketProtocolHeader(string clientSecWebSocketProtocol, out string acceptProtocol)
        {
            acceptProtocol = string.Empty;
            if (string.IsNullOrEmpty(clientSecWebSocketProtocol))
            {
                return false;
            }
            string[] array = clientSecWebSocketProtocol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (array.Length > 0)
            {
                acceptProtocol = array[0];
            }
            return true;
        }

        internal static bool ValidateWebSocketHeaders(NetContext context)
        {
            string text = context.Headers["Sec-WebSocket-Version"];
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            if (!string.Equals(text, "13", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string text2 = context.Headers["Sec-WebSocket-Key"];
            if (!string.IsNullOrWhiteSpace(text2))
            {
                try
                {
                    return Convert.FromBase64String(text2).Length == 16;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        internal static void AcceptWebSocketAsyncCore(NetContext context, Stream stream)
        {
            if (context.Url.AbsolutePath == "/UMC.WS")
            {
                var Device = context.Cookies["device"] ?? context.QueryString.Get("device");
                if (String.IsNullOrEmpty(Device) == false)
                {
                    if (ValidateWebSocketHeaders(context) && false)
                    {
                        string secWebSocketKey = context.Headers["Sec-WebSocket-Key"];
                        string secWebSocketAcceptString = GetSecWebSocketAcceptString(secWebSocketKey);
                        var writer = new Net.TextWriter(stream.Write);
                        writer.Write($"HTTP/1.1 101 {HttpStatusDescription.Get(101)}\r\n");
                        writer.Write("Connection: Upgrade\r\n");
                        writer.Write("Upgrade: websocket\r\n");


                        writer.Write($"Sec-WebSocket-Accept: {secWebSocketAcceptString}\r\n");
                        writer.Write($"Sec-WebSocket-Protocol: mqtt\r\n");

                        writer.Write("Server: UMC.Proxy\r\n\r\n");

                        writer.Flush();
                        writer.Dispose();
                        var DeviceId = UMC.Data.Utility.Guid(Device, true).Value;
                        //16384,
                        context.Tag = WebSocket.CreateFromStream(stream, isServer: true, "mqtt", WebSocket.DefaultKeepAliveInterval);
                        return;
                    }
                }
            }
            context.Error(new ArgumentException("WebSocket"));


        }

    }

}

