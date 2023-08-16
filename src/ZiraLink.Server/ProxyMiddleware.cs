using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System.Text;

namespace ZiraLink.Server
{
    public class ProxyMiddleware
    {
        private readonly ResponseCompletionSources _responseCompletionSources;
        private readonly ZiraApiClient _ziraApiClient;

        private readonly RequestDelegate _next;

        public ProxyMiddleware(RequestDelegate next, ResponseCompletionSources responseCompletionSources, ZiraApiClient ziraApiClient)
        {
            _next = next;
            _responseCompletionSources = responseCompletionSources;
            _ziraApiClient = ziraApiClient;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestID = Guid.NewGuid().ToString();
            var host = context.Request.Host;

            var projects = await _ziraApiClient.GetProjects(CancellationToken.None);
            var project = projects
                .Where(x => x.State == Enums.ProjectState.Active)
                .Where(x => x.DomainType == Enums.DomainType.Default ? $"{x.Domain}.app.ziralink.com:7001" == host.ToString() : x.Domain == host.ToString())
                .SingleOrDefault();
            if (project == null)
                throw new ApplicationException("Project not found");

            // Create a TaskCompletionSource to await the response
            var responseCompletionSource = new TaskCompletionSource<HttpResponseModel>();

            // Store the response completion source in a dictionary or cache
            StoreResponseCompletionSource(requestID, responseCompletionSource);

            // Extract request details
            var requestData = await GetRequestDataAsync(context.Request);

            var projectHost = project.DomainType == Enums.DomainType.Default ? $"{project.Domain}.app.ziralink.com:7001" : project.Domain;
            PublishRequestToRabbitMQ(project.Customer.Username, projectHost, project.InternalUrl, requestID, requestData);

            // Wait for the response or timeout
            var responseTask = responseCompletionSource.Task;
            if (await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(10))) == responseTask)
            {
                var response = await responseTask;

                context.Response.ContentType = response.ContentType;
                context.Response.Headers.Add("Content-Encoding", "UTF-8");
                foreach (var header in response.Headers)
                {
                    if (header.Key == "Content-Encoding")
                        continue;

                    foreach (var headerValue in header.Value)
                    {
                        if (!context.Response.Headers.ContainsKey(header.Key))
                            context.Response.Headers.Add(header.Key, headerValue);
                        else
                            context.Response.Headers.Append(header.Key, headerValue);
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    context.Response.StatusCode = (int)response.HttpStatusCode;
                }
                else
                {
                    if (new List<string>() { "image/jpg", "image/jpeg", "text/css", "text/javascript", "application/javascript" }.Contains(context.Response.ContentType))
                    {
                        if (response.Bytes != null && response.Bytes.Length > 0)
                        {
                            // Create a memory stream from the byte array
                            var memoryStream = new MemoryStream(response.Bytes);

                            // Set the response headers
                            context.Response.ContentType = response.ContentType;
                            context.Response.ContentLength = memoryStream.Length;

                            // Write the image content to the response stream
                            await memoryStream.CopyToAsync(context.Response.Body);

                            // Close the memory stream
                            memoryStream.Close();
                        }
                    }
                    else
                    {
                        await context.Response.WriteAsync(Encoding.UTF8.GetString(response.Bytes));
                    }
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }
        }

        private void PublishRequestToRabbitMQ(string username, string host, string internalUrl, string requestID, string requestData)
        {
            var factory = new ConnectionFactory { HostName = "localhost", Port = 5672, UserName = "guest", Password = "guest" };
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
            properties.MessageId = requestID;
            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", internalUrl);
            headers.Add("Host", host);
            properties.Headers = headers;

            channel.BasicPublish(exchange: "request", routingKey: username, basicProperties: properties, body: Encoding.UTF8.GetBytes(requestData));
        }

        private void StoreResponseCompletionSource(string requestID, TaskCompletionSource<HttpResponseModel> responseCompletionSource)
        {
            // Store the response completion source in a dictionary or cache based on requestID
            _responseCompletionSources.Sources.TryAdd(requestID, responseCompletionSource);
        }

        private async Task<string> GetRequestDataAsync(HttpRequest request)
        {
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                var requestBody = await reader.ReadToEndAsync();

                // Reconstruct the full request including headers, query parameters, and path
                var requestBuilder = new StringBuilder();
                requestBuilder.AppendLine($"{request.Method} {request.Path}{request.QueryString} {request.Protocol}");

                foreach (var header in request.Headers)
                {
                    requestBuilder.AppendLine($"{header.Key}: {header.Value}");
                }

                requestBuilder.AppendLine();
                requestBuilder.AppendLine(requestBody);

                return requestBuilder.ToString();
            }
        }
    }
}
