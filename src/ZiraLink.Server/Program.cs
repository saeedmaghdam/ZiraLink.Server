using System.Net;
using System.Net.Security;
using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;
using RabbitMQ.Client;
using Serilog;
using ZiraLink.Server;
using ZiraLink.Server.Enums;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Middlewares;
using ZiraLink.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var pathToExe = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);

IConfiguration Configuration = new ConfigurationBuilder()
    .SetBasePath(pathToExe)
    .AddJsonFile("appsettings.json", false, true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .CreateLogger();
builder.Host.UseSerilog();

// Add services to the container.
if (Configuration["ASPNETCORE_ENVIRONMENT"] == "Test")
{
    ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
    {
        string expectedThumbprint = Configuration["ZIRALINK_CERT_THUMBPRINT_LOCALHOST"]!;
        if (certificate!.GetCertHashString() == expectedThumbprint)
            return true;

        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        return false;
    };
}

builder.Services.AddMemoryCache();

builder.Services.AddSingleton(serviceProvider =>
{
    var factory = new ConnectionFactory();
    factory.DispatchConsumersAsync = true;
    factory.Uri = new Uri(Configuration["ZIRALINK_CONNECTIONSTRINGS_RABBITMQ"]!);
    var connection = factory.CreateConnection();
    var channel = connection.CreateModel();

    return channel;
});
builder.Services.AddSingleton<ResponseCompletionSources>();
builder.Services.AddSingleton<IZiraApiClient, ZiraApiClient>();
builder.Services.AddSingleton<IProjectService, ProjectService>();
builder.Services.AddSingleton<IAppProjectService, AppProjectService>();
builder.Services.AddSingleton<IApiExternalBusService, ApiExternalBusService>();
builder.Services.AddSingleton<IIdentityService, IdentityService>();
builder.Services.AddSingleton<IWebSocketService, WebSocketService>();
builder.Services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
builder.Services.AddSingleton<IHttpRequestProxyService, HttpRequestProxyService>();
builder.Services.AddSingleton<ICache, Cache>();
builder.Services.AddSingleton<IServerBusService, ServerBusService>();
builder.Services.AddSingleton<IAppProjectConsumerService, AppProjectConsumerService>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddHttpClient(NamedHttpClients.Default);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
    // Only loopback proxies are allowed by default.
    // Clear that restriction because forwarders are enabled by explicit
    // configuration.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.Services.GetRequiredService<IWebSocketService>().InitializeConsumer();

// Configure the HTTP request pipeline.
if (string.IsNullOrWhiteSpace(Configuration["ZIRALINK_USE_HTTP"]) || !bool.Parse(Configuration["ZIRALINK_USE_HTTP"]!))
{
    app.UseHttpsRedirection();
}

app.UseWebSockets();
app.UseMiddleware<HttpRequestProxyMiddleware>();
app.UseMiddleware<WebSocketProxyMiddleware>();

app.Run();
