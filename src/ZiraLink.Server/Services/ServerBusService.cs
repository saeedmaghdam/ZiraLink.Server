using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server.Services
{
    public class ServerBusService : IServerBusService
    {
        private readonly IModel _channel;
        private readonly IZiraApiClient _ziraApiClient;

        public ServerBusService(IModel channel, IZiraApiClient ziraApiClient)
        {
            _channel = channel;
            _ziraApiClient = ziraApiClient;
        }

        public void InitializeConsumer(CancellationToken cancellationToken)
        {
            var queueName = "server_bus";

            _channel.QueueDeclare(queueName, false, false, false, null);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var username = Encoding.UTF8.GetString(ea.BasicProperties.Headers["username"] as byte[]);

                var clientExchangeName = "client_bus";
                var clientQueueName = $"{username}_client_bus";

                _channel.ExchangeDeclare(clientExchangeName, "direct", false, false, null);
                _channel.QueueDeclare(clientQueueName, false, false, false, null);
                _channel.QueueBind(clientQueueName, clientExchangeName, username, null);

                var appProjects = await _ziraApiClient.GetAppProjectsAsync(cancellationToken);

                var responseBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(appProjects));
                _channel.BasicPublish(clientExchangeName, username, null, responseBody);

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queueName, false, consumer);
        }
    }
}
