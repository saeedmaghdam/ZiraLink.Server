using System.Net.WebSockets;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Framework.Services
{
    public interface ICache
    {
        Project SetProject(string host, Project value);
        bool TryGetProject(string host, out Project value);
        IWebSocket SetWebSocket(string host, IWebSocket value);
        bool TryGetWebSocket(string host, out IWebSocket value);
        void RemoveWebSocket(string host);
    }
}
