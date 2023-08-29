using ZiraLink.Server.Models;

namespace ZiraLink.Server.Framework.Services
{
    public interface IWebSocketService
    {
        Task Initialize(IWebSocket webSocket, Project project);
        void InitializeConsumer();
    }
}
