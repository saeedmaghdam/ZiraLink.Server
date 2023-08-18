using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Models;

namespace ZiraLink.Server
{
    public class ProjectService
    {
        private Dictionary<string, Project> _projects = new Dictionary<string, Project>();
        private readonly ZiraApiClient _ziraApiClient;
        public ProjectService(ZiraApiClient ziraApiClient)
        {
            _ziraApiClient = ziraApiClient;
        }

        public Project GetByHost(string host)
        {
            if (!_projects.ContainsKey(host)) throw new ApplicationException("Project not found");
            return _projects[host];
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory();
            factory.Uri = new Uri(Environment.GetEnvironmentVariable("ZIRALINK_CONNECTIONSTRINGS_RABBITMQ")!);
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
                await UpdateProjectsAsync(cancellationToken);

                channel.BasicAck(ea.DeliveryTag, false);
                await Task.Yield();
            };

            await UpdateProjectsAsync(cancellationToken);
            channel.BasicConsume(queue: queueName, autoAck: false, consumer: external_bus_consumer);
        }

        private async Task UpdateProjectsAsync(CancellationToken cancellationToken)
        {
            var projects = await _ziraApiClient.GetProjects(CancellationToken.None);

            var projectDictionary = new Dictionary<string, Project>();
            foreach (var project in projects.Where(x=> x.State == Enums.ProjectState.Active))
            {
                var projectHost = project.DomainType == Enums.DomainType.Default ? $"{project.Domain}.app.ziralink.com:7001" : project.Domain;
                projectDictionary.TryAdd(projectHost, project);
            }

            _projects = projectDictionary;
        }
    }
}
