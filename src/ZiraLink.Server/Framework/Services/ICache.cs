using System.Net.WebSockets;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Framework.Services
{
    public interface ICache
    {
        Project SetProject(string host, Project value);
        bool TryGetProject(string host, out Project value);
        WebSocket SetWebSocket(string host, WebSocket value);
        bool TryGetWebSocket(string host, out WebSocket value);
        void RemoveWebSocket(string host);
    }
}
