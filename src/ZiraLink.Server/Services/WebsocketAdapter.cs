using System.Net.WebSockets;
using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server.Services
{
    public class WebsocketAdapter : IWebSocket
    {
        private readonly WebSocket _websocket;

        public WebsocketAdapter(WebSocket websocket)
        {
            _websocket = websocket;
        }

        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => await _websocket.ReceiveAsync(buffer, cancellationToken);
        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => await _websocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

    }
}
