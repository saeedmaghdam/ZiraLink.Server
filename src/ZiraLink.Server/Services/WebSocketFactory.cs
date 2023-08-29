using System.Net.WebSockets;
using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server.Services
{
    public class WebSocketFactory : IWebSocketFactory
    {
        public IWebSocket CreateClientWebSocket(WebSocket webSocket)
        {
            return new WebsocketAdapter(webSocket);
        }
    }
}
