using ZiraLink.Server.Models;

namespace ZiraLink.Server.Framework.Services
{
    public interface IWebSocketService
    {
        Task Initialize(HttpContext context, Project project);
        void InitializeConsumer();
    }
}
