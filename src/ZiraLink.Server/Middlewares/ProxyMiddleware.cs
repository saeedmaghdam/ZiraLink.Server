﻿using Microsoft.AspNetCore.Http.Extensions;
using RabbitMQ.Client;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ZiraLink.Server.Models;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.Middlewares
{
    public class ProxyMiddleware
    {
        private readonly ResponseCompletionSources _responseCompletionSources;
        private readonly ProjectService _projectService;
        private readonly WebSocketService _webSocketService;

        private readonly RequestDelegate _next;

        public ProxyMiddleware(RequestDelegate next, ResponseCompletionSources responseCompletionSources, ProjectService projectService, WebSocketService webSocketService)
        {
            _next = next;
            _responseCompletionSources = responseCompletionSources;
            _projectService = projectService;
            _webSocketService = webSocketService;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString();
            var host = context.Request.Host;

            var project = _projectService.GetByHost(host.Value);
            var projectHost = project.DomainType == Enums.DomainType.Default ? $"{project.Domain}{Environment.GetEnvironmentVariable("ZIRALINK_DEFAULT_DOMAIN")}" : project.Domain;

            if (context.WebSockets.IsWebSocketRequest)
                await _webSocketService.Initialize(context, project, projectHost);
            else
                await HandleHttpRequest(context, requestId, project, projectHost);
        }

        private async Task HandleHttpRequest(HttpContext context, string requestId, Project project, string projectHost)
        {
            // Create a TaskCompletionSource to await the response
            var responseCompletionSource = new TaskCompletionSource<HttpResponseModel>();

            // Store the response completion source in a dictionary or cache
            StoreResponseCompletionSource(requestId, responseCompletionSource);

            // Extract request details
            var requestData = await GetRequestDataAsync(context.Request);

            PublishRequestToRabbitMQ(project.Customer.Username, projectHost, project.InternalUrl, requestId, requestData);

            // Wait for the response or timeout
            var responseTask = responseCompletionSource.Task;
            if (await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(10))) == responseTask)
            {
                var response = await responseTask;

                context.Response.StatusCode = (int)response.HttpStatusCode;

                context.Response.ContentType = response.ContentType;
                context.Response.Headers.Clear();
                foreach (var header in response.Headers)
                {
                    context.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
                }

                if (!string.IsNullOrEmpty(response.StringContent))
                {
                    await context.Response.WriteAsync(response.StringContent, Encoding.UTF8);
                }
                else
                {
                    await context.Response.Body.WriteAsync(response.Bytes);
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }
        }

        //private async Task HandleWebSocketRequest(HttpContext context, string requestId, Project project, string projectHost)
        //{
        //    var socket = await context.WebSockets.AcceptWebSocketAsync();

        //    var buffer = new byte[1024];
        //    while (true)
        //    {
        //        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
        //        if (result.MessageType == WebSocketMessageType.Close)
        //            break;

        //        var webSocketData = new WebSocketData
        //        {
        //            Payload = buffer,
        //            PayloadCount = result.Count,
        //            MessageType = result.MessageType,
        //            EndOfMessage = result.EndOfMessage
        //        };

        //        var message = JsonSerializer.Serialize(webSocketData);
        //        PublishWebSocketDataToRabbitMQ(project.Customer.Username, projectHost, project.InternalUrl, requestId, message);
        //    }

        //    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Proxy closed", default);
        //}

        private void PublishRequestToRabbitMQ(string username, string projectHost, string internalUrl, string requestId, string message)
        {
            var factory = new ConnectionFactory();
            factory.Uri = new Uri(Environment.GetEnvironmentVariable("ZIRALINK_CONNECTIONSTRINGS_RABBITMQ")!);
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var queueName = $"{username}_request_bus";
            var exchangeName = "request";

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
                routingKey: username,
                arguments: null);

            var properties = channel.CreateBasicProperties();
            properties.MessageId = requestId;
            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", internalUrl);
            headers.Add("Host", projectHost);
            properties.Headers = headers;

            channel.BasicPublish(exchange: exchangeName, routingKey: username, basicProperties: properties, body: Encoding.UTF8.GetBytes(message));
        }

        //private void PublishWebSocketDataToRabbitMQ(string username, string projectHost, string internalUrl, string requestId, string message)
        //{
        //    var factory = new ConnectionFactory();
        //    factory.Uri = new Uri(Environment.GetEnvironmentVariable("ZIRALINK_CONNECTIONSTRINGS_RABBITMQ")!);
        //    using var connection = factory.CreateConnection();
        //    using var channel = connection.CreateModel();

        //    var queueName = $"{username}_websocket_server_bus";
        //    var exchangeName = "websocket_bus";

        //    channel.ExchangeDeclare(exchange: exchangeName,
        //        type: "direct",
        //        durable: false,
        //        autoDelete: false,
        //        arguments: null);

        //    channel.QueueDeclare(queue: queueName,
        //             durable: false,
        //             exclusive: false,
        //             autoDelete: false,
        //             arguments: null);

        //    channel.QueueBind(queue: queueName,
        //        exchange: exchangeName,
        //        routingKey: queueName,
        //        arguments: null);

        //    var properties = channel.CreateBasicProperties();
        //    properties.MessageId = requestId;
        //    var headers = new Dictionary<string, object>();
        //    headers.Add("IntUrl", internalUrl);
        //    headers.Add("Host", projectHost);
        //    properties.Headers = headers;

        //    channel.BasicPublish(exchange: exchangeName, routingKey: queueName, basicProperties: properties, body: Encoding.UTF8.GetBytes(message));
        //}

        private void StoreResponseCompletionSource(string requestID, TaskCompletionSource<HttpResponseModel> responseCompletionSource)
        {
            // Store the response completion source in a dictionary or cache based on requestID
            _responseCompletionSources.Sources.TryAdd(requestID, responseCompletionSource);
        }

        private async Task<string> GetRequestDataAsync(HttpRequest request)
        {
            var requestMethod = request.Method;

            var requestModel = new HttpRequestModel();
            requestModel.RequestUrl = request.GetDisplayUrl();
            requestModel.Method = requestMethod;
            var headers = new List<KeyValuePair<string, IEnumerable<string>>>();
            foreach (var header in request.Headers)
                headers.Add(new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value));
            requestModel.Headers = headers;

            if (!HttpMethods.IsGet(requestMethod) &&
                !HttpMethods.IsHead(requestMethod) &&
                !HttpMethods.IsDelete(requestMethod) &&
                !HttpMethods.IsTrace(requestMethod))
            {
                requestModel.Bytes = ReadStreamInBytes(request.Body);
            }

            return JsonSerializer.Serialize(requestModel);
        }

        public static byte[] ReadStreamInBytes(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}
