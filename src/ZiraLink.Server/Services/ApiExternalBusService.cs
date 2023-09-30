using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server.Services
{
    public class ApiExternalBusService : IApiExternalBusService
    {
        private readonly IModel _channel;
        private readonly IProjectService _projectService;
        private readonly IAppProjectService _appProjectService;

        public ApiExternalBusService(IModel channel, IProjectService projectService, IAppProjectService appProjectService)
        {
            _channel = channel;
            _projectService = projectService;
            _appProjectService = appProjectService;
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
                _channel.BasicAck(ea.DeliveryTag, false);
                var message = Encoding.UTF8.GetString(ea.Body.ToArray());

                try
                {
                    switch (message)
                    {
                        case "PROJECT_CREATED":
                        case "PROJECT_DELETED":
                        case "PROJECT_PATCHED":
                            await _projectService.UpdateProjectsAsync(cancellationToken);
                            break;
                        case "APP_PROJECT_CREATED":
                        case "APP_PROJECT_DELETED":
                        case "APP_PROJECT_PATCHED":
                            await _appProjectService.UpdateAppProjectsAsync(cancellationToken);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                catch
                {
                    // ignored
                }

                await Task.Yield();
            };

            await _projectService.UpdateProjectsAsync(cancellationToken);
            await _appProjectService.UpdateAppProjectsAsync(cancellationToken);

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: external_bus_consumer);
        }
    }
}
