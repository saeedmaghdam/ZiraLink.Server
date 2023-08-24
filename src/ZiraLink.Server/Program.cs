using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;
using RabbitMQ.Client;
using Serilog;
using ZiraLink.Server;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Middlewares;
using ZiraLink.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var pathToExe = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);
Directory.SetCurrentDirectory(pathToExe!);

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
builder.Services.AddSingleton<IIdentityService, IdentityService>();
builder.Services.AddSingleton<WebSocketService>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddHttpClient();

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

app.Services.GetRequiredService<WebSocketService>().InitializeConsumer();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseWebSockets();
app.UseMiddleware<HttpRequestProxyMiddleware>();
app.UseMiddleware<WebSocketProxyMiddleware>();

app.Run();
