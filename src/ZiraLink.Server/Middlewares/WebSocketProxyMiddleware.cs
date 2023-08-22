using ZiraLink.Server.Services;

namespace ZiraLink.Server.Middlewares
{
    public class WebSocketProxyMiddleware
    {
        private readonly ProjectService _projectService;
        private readonly WebSocketService _webSocketService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebSocketProxyMiddleware> _logger;

        private readonly RequestDelegate _next;

        public WebSocketProxyMiddleware(RequestDelegate next, ProjectService projectService, WebSocketService webSocketService, IConfiguration configuration, ILogger<WebSocketProxyMiddleware> logger)
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
            var projectHost = project.DomainType == Enums.DomainType.Default ? $"{project.Domain}{_configuration["ZIRALINK_DEFAULT_DOMAIN"]}" : project.Domain;

            if (context.WebSockets.IsWebSocketRequest)
                await _webSocketService.Initialize(context, project, projectHost);
            else
                await _next(context);
        }
    }
}
