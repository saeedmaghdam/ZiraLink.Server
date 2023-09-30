using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class AppProjectService : IAppProjectService
    {
        private readonly IZiraApiClient _ziraApiClient;
        private readonly IConfiguration _configuration;
        private readonly ICache _cache;

        public AppProjectService(IZiraApiClient ziraApiClient, IConfiguration configuration, ICache cache)
        {
            _ziraApiClient = ziraApiClient;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task UpdateAppProjectsAsync(CancellationToken cancellationToken)
        {
            var appProjects = await _ziraApiClient.GetAppProjectsAsync(CancellationToken.None);

            var appProjectDictionary = new Dictionary<string, AppProject>();
            foreach (var appProject in appProjects.Where(x => x.State == Enums.ProjectState.Active))
            {
                _cache.SetAppProject(appProject.Customer.Username, appProject.InternalPort, appProject);
                _cache.SetAppProject(appProject.ViewId, appProject);
            }
        }
    }
}
