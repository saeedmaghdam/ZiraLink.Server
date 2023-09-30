using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server
{
    public class Worker : IHostedService
    {
        private readonly IHttpRequestProxyService _httpRequestProxyService;
        private readonly IServerBusService _serverBusService;
        private readonly IAppProjectConsumerService _appProjectConsumerService;

        public Worker(IHttpRequestProxyService httpRequestProxyService, IServerBusService serverBusService, IAppProjectConsumerService appProjectConsumerService)
        {
            _httpRequestProxyService = httpRequestProxyService;
            _serverBusService = serverBusService;
            _appProjectConsumerService = appProjectConsumerService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _httpRequestProxyService.InitializeConsumerAsync(cancellationToken);
            _serverBusService.InitializeConsumer(cancellationToken);
            _appProjectConsumerService.InitializeConsumer();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
