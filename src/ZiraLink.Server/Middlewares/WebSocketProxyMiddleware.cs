using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.Middlewares
{
    public class WebSocketProxyMiddleware
    {
        private readonly IProjectService _projectService;
        private readonly IWebSocketService _webSocketService;
        private readonly IConfiguration _configuration;
        private readonly IWebSocketFactory _webSocketFactory;
        private readonly ILogger<WebSocketProxyMiddleware> _logger;

        private readonly RequestDelegate _next;

        public WebSocketProxyMiddleware(RequestDelegate next, ILogger<WebSocketProxyMiddleware> logger, IProjectService projectService, IWebSocketService webSocketService, IConfiguration configuration, IWebSocketFactory webSocketFactory)
        {
            _next = next;
            _logger = logger;
            _projectService = projectService;
            _webSocketService = webSocketService;
            _configuration = configuration;
            _webSocketFactory = webSocketFactory;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString();
            var host = context.Request.Host;

            var project = _projectService.GetByHost(host.Value);

            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocketConnection = await context.WebSockets.AcceptWebSocketAsync();
                var webSocket = _webSocketFactory.CreateClientWebSocket(webSocketConnection);
                await _webSocketService.Initialize(webSocket, project);
            }
            else
            {
                await _next(context);
            }
        }
    }
}
