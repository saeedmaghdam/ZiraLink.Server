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
        public ProjectService(ZiraApiClient ziraApiClient, IConfiguration configuration, IMemoryCache memoryCache)
        {
            _ziraApiClient = ziraApiClient;
            _configuration = configuration;
            _memoryCache = memoryCache;
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
            var factory = new ConnectionFactory();
            factory.Uri = new Uri(_configuration["ZIRALINK_CONNECTIONSTRINGS_RABBITMQ"]!);
            factory.DispatchConsumersAsync = true;
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            var queueName = $"api_to_server_external_bus";

            channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var external_bus_consumer = new AsyncEventingBasicConsumer(channel);
            external_bus_consumer.Received += async (model, ea) =>
            {
                try
                {
                    await UpdateProjectsAsync(cancellationToken);
                }
                finally
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                await Task.Yield();
            };

            await UpdateProjectsAsync(cancellationToken);
            channel.BasicConsume(queue: queueName, autoAck: false, consumer: external_bus_consumer);
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
