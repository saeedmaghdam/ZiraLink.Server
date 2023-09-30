using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IZiraApiClient _ziraApiClient;
        private readonly IConfiguration _configuration;
        private readonly ICache _cache;

        public ProjectService(IZiraApiClient ziraApiClient, IConfiguration configuration, ICache cache)
        {
            _ziraApiClient = ziraApiClient;
            _configuration = configuration;
            _cache = cache;
        }

        public Project GetByHost(string host)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException(nameof(host));

            if (!_cache.TryGetProject(host, out Project project)) throw new ApplicationException("Project not found");
            return project;
        }

        public async Task UpdateProjectsAsync(CancellationToken cancellationToken)
        {
            var projects = await _ziraApiClient.GetProjectsAsync(CancellationToken.None);

            var projectDictionary = new Dictionary<string, Project>();
            foreach (var project in projects.Where(x => x.State == Enums.ProjectState.Active))
                _cache.SetProject(project.GetProjectHost(_configuration), project);
        }
    }
}
