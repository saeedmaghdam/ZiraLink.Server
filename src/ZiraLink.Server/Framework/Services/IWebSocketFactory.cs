using System.Net.WebSockets;

namespace ZiraLink.Server.Framework.Services
{
    public interface IWebSocketFactory
    {
        IWebSocket CreateClientWebSocket(WebSocket webSocket);
    }
}
