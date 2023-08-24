using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.Middlewares
{
    public class WebSocketProxyMiddleware
    {
        private readonly IProjectService _projectService;
        private readonly IWebSocketService _webSocketService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebSocketProxyMiddleware> _logger;

        private readonly RequestDelegate _next;

        public WebSocketProxyMiddleware(RequestDelegate next, IProjectService projectService, IWebSocketService webSocketService, IConfiguration configuration, ILogger<WebSocketProxyMiddleware> logger)
        {
            _next = next;
            _projectService = projectService;
            _webSocketService = webSocketService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString();
            var host = context.Request.Host;

            var project = _projectService.GetByHost(host.Value);
            
            if (context.WebSockets.IsWebSocketRequest)
                await _webSocketService.Initialize(context, project);
            else
                await _next(context);
        }
    }
}
