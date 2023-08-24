using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server
{
    public class Worker : IHostedService
    {
        private readonly IHttpRequestProxyService _httpRequestProxyService;

        public Worker(IHttpRequestProxyService httpRequestProxyService) => _httpRequestProxyService = httpRequestProxyService;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _httpRequestProxyService.InitializeConsumerAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
