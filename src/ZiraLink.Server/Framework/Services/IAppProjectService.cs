namespace ZiraLink.Server.Framework.Services
{
    public interface IAppProjectService
    {
        Task UpdateAppProjectsAsync(CancellationToken cancellationToken);
    }
}
