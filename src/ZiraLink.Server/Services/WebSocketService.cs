using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class WebSocketService : IWebSocketService
    {
        private readonly IConfiguration _configuration;
        private readonly IModel _channel;
        private readonly ICache _cache;

        public WebSocketService(IConfiguration configuration, IModel channel, ICache cache)
        {
            _configuration = configuration;
            _channel = channel;
            _cache = cache;
        }

        public async Task Initialize(IWebSocket webSocket, Project project)
        {
            _cache.SetWebSocket(project.GetProjectHost(_configuration), webSocket);
            InitializeRabbitMq(project.Customer.Username);

            try
            {
                var buffer = new byte[1024];
                while (true)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var webSocketData = new WebSocketData
                    {
                        Payload = buffer,
                        PayloadCount = result.Count,
                        MessageType = result.MessageType,
                        EndOfMessage = result.EndOfMessage
                    };

                    var message = JsonSerializer.Serialize(webSocketData);
                    PublishWebSocketDataToRabbitMQ(project.Customer.Username, project.GetProjectHost(_configuration), project.InternalUrl, message);
                }
            }
            finally
            {
                _cache.RemoveWebSocket(project.GetProjectHost(_configuration));
            }
        }

        public void InitializeConsumer()
        {
            var queueName = "websocket_client_bus";

            _channel.QueueDeclare(queue: queueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            // Start consuming responses
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                _channel.BasicAck(ea.DeliveryTag, false);

                try
                {
                    var response = Encoding.UTF8.GetString(ea.Body.ToArray());

                    if (!ea.BasicProperties.Headers.TryGetValue("Host", out var hostByteArray))
                        throw new ApplicationException("Host not found");
                    var host = Encoding.UTF8.GetString((byte[])hostByteArray);

                    var requestModel = JsonSerializer.Deserialize<WebSocketData>(response);
                    if (!_cache.TryGetWebSocket(host, out IWebSocket webSocket))
                        throw new ApplicationException("WebSocket not found");

                    var arraySegment = new ArraySegment<byte>(requestModel.Payload, 0, requestModel.PayloadCount);
                    await webSocket.SendAsync(arraySegment,
                            requestModel.MessageType,
                            requestModel.EndOfMessage,
                            CancellationToken.None);
                }
                catch
                {
                    // ignored
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }

        private void InitializeRabbitMq(string username)
        {
            var queueName = $"{username}_websocket_server_bus";
            var exchangeName = "websocket_bus";

            _channel.ExchangeDeclare(exchange: exchangeName,
                type: "direct",
                durable: false,
                autoDelete: false,
                arguments: null);

            _channel.QueueDeclare(queue: queueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            _channel.QueueBind(queue: queueName,
                exchange: exchangeName,
                routingKey: queueName,
                arguments: null);
        }

        private void PublishWebSocketDataToRabbitMQ(string username, string projectHost, string internalUrl, string message)
        {
            var queueName = $"{username}_websocket_server_bus";
            var exchangeName = "websocket_bus";

            var properties = _channel.CreateBasicProperties();
            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", internalUrl);
            headers.Add("Host", projectHost);
            properties.Headers = headers;

            _channel.BasicPublish(exchange: exchangeName, routingKey: queueName, basicProperties: properties, body: Encoding.UTF8.GetBytes(message));
        }
    }
}
