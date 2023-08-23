using Microsoft.AspNetCore.Http.Extensions;
using RabbitMQ.Client;
using System.Text.Json;
using System.Text;
using ZiraLink.Server.Models;
using ZiraLink.Server.Services;
using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server.Middlewares
{
    public class HttpRequestProxyMiddleware
    {
        private readonly ResponseCompletionSources _responseCompletionSources;
        private readonly IProjectService _projectService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HttpRequestProxyMiddleware> _logger;
        private readonly IModel _channel;
        private Dictionary<string, bool> _initializedQueues = new Dictionary<string, bool>();

        private readonly RequestDelegate _next;

        public HttpRequestProxyMiddleware(RequestDelegate next, ResponseCompletionSources responseCompletionSources, IProjectService projectService, IConfiguration configuration, ILogger<HttpRequestProxyMiddleware> logger, IModel channel)
        {
            _next = next;
            _responseCompletionSources = responseCompletionSources;
            _projectService = projectService;
            _configuration = configuration;
            _logger = logger;
            _channel = channel;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString();
            var host = context.Request.Host;

            var project = _projectService.GetByHost(host.Value);
            var projectHost = project.DomainType == Enums.DomainType.Default ? $"{project.Domain}{_configuration["ZIRALINK_DEFAULT_DOMAIN"]}" : project.Domain;

            if (!context.WebSockets.IsWebSocketRequest)
                await HandleHttpRequest(context, requestId, project, projectHost);
            else
                await _next(context);
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
            if (await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(100))) == responseTask)
            {
                var response = await responseTask;
                if (response.IsRedirected)
                {
                    context.Response.Redirect(response.RedirectUrl, response.HttpStatusCode == System.Net.HttpStatusCode.PermanentRedirect ? true : false);
                    return;
                }

                context.Response.StatusCode = (int)response.HttpStatusCode;

                context.Response.ContentType = response.ContentType;
                context.Response.Headers.Clear();
                var excluded_headers_list = new string[]
                {
                    "transfer-encoding"
                };
                foreach (var header in response.Headers)
                {
                    if (excluded_headers_list.Contains(header.Key.ToLower()))
                        continue;

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

        private void PublishRequestToRabbitMQ(string username, string projectHost, string internalUrl, string requestId, string message)
        {
            var queueName = $"{username}_request_bus";
            var exchangeName = "request";

            if (!_initializedQueues.ContainsKey(username))
            {
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
                    routingKey: username,
                    arguments: null);

                _initializedQueues.Add(username, true);
            }

            var properties = _channel.CreateBasicProperties();
            properties.MessageId = requestId;
            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", internalUrl);
            headers.Add("Host", projectHost);
            properties.Headers = headers;

            _channel.BasicPublish(exchange: exchangeName, routingKey: username, basicProperties: properties, body: Encoding.UTF8.GetBytes(message));
        }

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
                requestModel.Bytes = await ReadStreamInBytesAsync(request.Body);
            }

            return JsonSerializer.Serialize(requestModel);
        }

        public static async Task<byte[]> ReadStreamInBytesAsync(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}
