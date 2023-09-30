namespace ZiraLink.Server.Framework.Services
{
    public interface IApiExternalBusService
    {
        Task InitializeAsync(CancellationToken cancellationToken);
    }
}
