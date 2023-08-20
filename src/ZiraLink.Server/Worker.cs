using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Models;
using ZiraLink.Server.Services;

namespace ZiraLink.Server
{
    public class Worker : IHostedService
    {
        private readonly ResponseCompletionSources _responseCompletionSources;
        private readonly ProjectService _projectService;
        private readonly IConfiguration _configuration;

        public Worker(ResponseCompletionSources responseCompletionSources, ProjectService projectService, IConfiguration configuration)
        {
            _responseCompletionSources = responseCompletionSources;
            _projectService = projectService;
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _projectService.InitializeAsync(cancellationToken);

            // Set up RabbitMQ connection and channels
            var factory = new ConnectionFactory();
            factory.Uri = new Uri(_configuration["ZIRALINK_CONNECTIONSTRINGS_RABBITMQ"]!);
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            var queueName = $"response_bus";
            var exchangeName = "response";

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
               routingKey: "",
               arguments: null);

            // Start consuming responses
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var responseID = ea.BasicProperties.MessageId;
                //var response = Encoding.UTF8.GetString(ea.Body.ToArray());

                // Retrieve the response completion source and complete it
                if (RetrieveResponseCompletionSource(responseID, out var responseCompletionSource))
                {
                    var httpResponse = JsonSerializer.Deserialize<HttpResponseModel>(Encoding.UTF8.GetString(ea.Body.ToArray()));
                    responseCompletionSource.SetResult(httpResponse);
                }
                channel.BasicAck(ea.DeliveryTag, false);
            };

            channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
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
