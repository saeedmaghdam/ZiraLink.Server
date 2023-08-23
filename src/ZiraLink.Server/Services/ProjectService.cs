using Microsoft.Extensions.Caching.Memory;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class ProjectService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ZiraApiClient _ziraApiClient;
        private readonly IConfiguration _configuration;
        private readonly IModel _channel;

        public ProjectService(ZiraApiClient ziraApiClient, IConfiguration configuration, IMemoryCache memoryCache, IModel channel)
        {
            _ziraApiClient = ziraApiClient;
            _configuration = configuration;
            _memoryCache = memoryCache;
            _channel = channel;
        }

        public Project GetByHost(string host)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException(nameof(host));

            if (!_memoryCache.TryGetValue(host, out Project project)) throw new ApplicationException("Project not found");
            return project;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var queueName = $"api_to_server_external_bus";

            _channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var external_bus_consumer = new AsyncEventingBasicConsumer(_channel);
            external_bus_consumer.Received += async (model, ea) =>
            {
                try
                {
                    await UpdateProjectsAsync(cancellationToken);
                }
                finally
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                await Task.Yield();
            };

            await UpdateProjectsAsync(cancellationToken);
            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: external_bus_consumer);
        }

        private async Task UpdateProjectsAsync(CancellationToken cancellationToken)
        {
            var projects = await _ziraApiClient.GetProjects(CancellationToken.None);

            var projectDictionary = new Dictionary<string, Project>();
            foreach (var project in projects.Where(x => x.State == Enums.ProjectState.Active))
            {
                var projectHost = project.DomainType == Enums.DomainType.Default ? $"{project.Domain}{_configuration["ZIRALINK_DEFAULT_DOMAIN"]}" : project.Domain;
                _memoryCache.Set(projectHost, project);
            }
        }
    }
}
