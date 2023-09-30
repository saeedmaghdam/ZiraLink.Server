using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server.Services
{
    public class AppProjectConsumerService : IAppProjectConsumerService
    {
        private readonly ICache _cache;
        private readonly IModel _channel;

        public AppProjectConsumerService(ICache cache, IModel channel)
        {
            _cache = cache;
            _channel = channel;
        }

        public void InitializeConsumer()
        {
            InitializeRequestConsumer();
            InitializeResponseConsumer();
        }

        private void InitializeRequestConsumer()
        {
            var queueName = "server_network_requests";

            _channel.QueueDeclare(queueName, false, false, false, null);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var useportUsername = Encoding.UTF8.GetString(ea.BasicProperties.Headers["useport_username"] as byte[]);
                var useportPort = int.Parse(ea.BasicProperties.Headers["useport_port"].ToString()!);
                var useportConnectionId = Encoding.UTF8.GetString(ea.BasicProperties.Headers["useport_connectionid"] as byte[]);

                if (!_cache.TryGetAppProject(useportUsername, useportPort, out var appProject))
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                if (!appProject.AppProjectViewId.HasValue)
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                if (!_cache.TryGetAppProject(appProject.AppProjectViewId.Value, out var destinationAppProject))
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var destinationQueue = $"{destinationAppProject.Customer.Username}_client_shareport_network_packets";
                _channel.QueueDeclare(destinationQueue, false, false, false, null);

                var properties = _channel.CreateBasicProperties();
                properties.Headers = new Dictionary<string, object>()
                {
                    { "useport_username", useportUsername },
                    { "useport_port", useportPort },
                    { "useport_connectionid", useportConnectionId },
                    { "sharedport_port", destinationAppProject.InternalPort }
                };

                _channel.BasicPublish(string.Empty, destinationQueue, properties, body);

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }

        private void InitializeResponseConsumer()
        {
            var queueName = "server_network_responses";

            _channel.QueueDeclare(queueName, false, false, false, null);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var useportUsername = Encoding.UTF8.GetString(ea.BasicProperties.Headers["useport_username"] as byte[]);
                var useportPort = int.Parse(ea.BasicProperties.Headers["useport_port"].ToString()!);
                var useportConnectionId = Encoding.UTF8.GetString(ea.BasicProperties.Headers["useport_connectionid"] as byte[]);

                if (!_cache.TryGetAppProject(useportUsername, useportPort, out var appProject))
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var destinationQueue = $"{useportUsername}_client_useport_network_packets";
                _channel.QueueDeclare(destinationQueue, false, false, false, null);

                var properties = _channel.CreateBasicProperties();
                properties.Headers = new Dictionary<string, object>()
                {
                    { "useport_username", useportUsername },
                    { "useport_port", useportPort },
                    { "useport_connectionid", useportConnectionId }
                };

                _channel.BasicPublish(string.Empty, destinationQueue, properties, body);

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }
    }
}
