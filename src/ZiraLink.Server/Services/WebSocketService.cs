using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class WebSocketService
    {
        private readonly IConfiguration _configuration;

        private readonly Dictionary<string, WebSocket> _webSockets = new Dictionary<string, WebSocket>();

        public WebSocketService(IConfiguration configuration) => _configuration = configuration;

        public async Task Initialize(HttpContext context, Project project, string projectHost)
        {
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            _webSockets.Add(projectHost, socket);

            var buffer = new byte[1024];
            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
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
                PublishWebSocketDataToRabbitMQ(project.Customer.Username, projectHost, project.InternalUrl, message);
            }

            _webSockets.Remove(projectHost);
        }

        public async Task InitializeConsumer()
        {
            var factory = new ConnectionFactory();
            factory.DispatchConsumersAsync = true;
            factory.Uri = new Uri(_configuration["ZIRALINK_CONNECTIONSTRINGS_RABBITMQ"]!);
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            var queueName = "websocket_client_bus";

            channel.QueueDeclare(queue: queueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            // Start consuming responses
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var response = Encoding.UTF8.GetString(ea.Body.ToArray());

                    if (!ea.BasicProperties.Headers.TryGetValue("Host", out var hostByteArray))
                        throw new ApplicationException("Host not found");
                    var host = Encoding.UTF8.GetString((byte[])hostByteArray);

                    var requestModel = JsonSerializer.Deserialize<WebSocketData>(response);
                    if (!_webSockets.ContainsKey(host))
                        throw new ApplicationException("WebSocket not found");

                    var webSocket = _webSockets[host];

                    var arraySegment = new ArraySegment<byte>(requestModel.Payload, 0, requestModel.PayloadCount);
                    await webSocket.SendAsync(arraySegment,
                            requestModel.MessageType,
                            requestModel.EndOfMessage,
                            CancellationToken.None);
                }
                finally
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                }
            };

            channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }

        private void PublishWebSocketDataToRabbitMQ(string username, string projectHost, string internalUrl, string message)
        {
            var factory = new ConnectionFactory();
            factory.Uri = new Uri(_configuration["ZIRALINK_CONNECTIONSTRINGS_RABBITMQ"]!);
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var queueName = $"{username}_websocket_server_bus";
            var exchangeName = "websocket_bus";

            channel.ExchangeDeclare(exchange: exchangeName,
                type: "direct",
                durable: false,
                autoDelete: false,
                arguments: null);

            channel.QueueDeclare(queue: queueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            channel.QueueBind(queue: queueName,
                exchange: exchangeName,
                routingKey: queueName,
                arguments: null);

            var properties = channel.CreateBasicProperties();
            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", internalUrl);
            headers.Add("Host", projectHost);
            properties.Headers = headers;

            channel.BasicPublish(exchange: exchangeName, routingKey: queueName, basicProperties: properties, body: Encoding.UTF8.GetBytes(message));
        }
    }
}
