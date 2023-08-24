using ZiraLink.Server.Models;

namespace ZiraLink.Server.Framework.Services
{
    public interface IZiraApiClient
    {
        Task<List<Project>> GetProjectsAsync(CancellationToken cancellationToken);
    }
}
