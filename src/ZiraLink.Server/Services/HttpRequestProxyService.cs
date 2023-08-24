using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class HttpRequestProxyService : IHttpRequestProxyService
    {
        private readonly IProjectService _projectService;
        private readonly IModel _channel;
        private readonly ResponseCompletionSources _responseCompletionSources;

        public HttpRequestProxyService(IProjectService projectService, IModel channel, ResponseCompletionSources responseCompletionSources)
        {
            _projectService = projectService;
            _channel = channel;
            _responseCompletionSources = responseCompletionSources;
        }

        public async Task InitializeConsumerAsync(CancellationToken cancellationToken)
        {
            await _projectService.InitializeAsync(cancellationToken);

            var queueName = $"response_bus";
            var exchangeName = "response";

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
               routingKey: "",
               arguments: null);

            // Start consuming responses
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var responseID = ea.BasicProperties.MessageId;
                    //var response = Encoding.UTF8.GetString(ea.Body.ToArray());

                    // Retrieve the response completion source and complete it
                    if (RetrieveResponseCompletionSource(responseID, out var responseCompletionSource))
                    {
                        var httpResponse = JsonSerializer.Deserialize<HttpResponseModel>(Encoding.UTF8.GetString(ea.Body.ToArray()));
                        responseCompletionSource.SetResult(httpResponse);
                    }
                }
                finally
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }

        private bool RetrieveResponseCompletionSource(string requestID, out TaskCompletionSource<HttpResponseModel> responseCompletionSource)
        {
            // Retrieve the response completion source from the dictionary or cache based on requestID
            // Return true if the response completion source is found, false otherwise
            // You need to implement the appropriate logic for retrieving and removing the completion source
            // This is just a placeholder method to illustrate the concept

            // Example implementation using a dictionary
            return _responseCompletionSources.Sources.TryRemove(requestID, out responseCompletionSource);
        }
    }
}
