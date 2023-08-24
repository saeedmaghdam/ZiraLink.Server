namespace ZiraLink.Server.Framework.Services
{
    public interface IHttpRequestProxyService
    {
        Task InitializeConsumerAsync(CancellationToken cancellationToken);
    }
}
