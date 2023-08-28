using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Asn1.X509;
using RabbitMQ.Client;
using Serilog;
using ZiraLink.Server;
using ZiraLink.Server.Enums;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Middlewares;
using ZiraLink.Server.Services;

var builder = WebApplication.CreateBuilder();

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
ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
{
    string expectedThumbprint = "10CE57B0083EBF09ED8E53CF6AC33D49B3A76414";
    if (certificate!.GetCertHashString() == expectedThumbprint)
        return true;

    if (sslPolicyErrors == SslPolicyErrors.None)
        return true;

    return false;
};

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
builder.Services.AddSingleton<IWebSocketService, WebSocketService>();
builder.Services.AddSingleton<IHttpRequestProxyService, HttpRequestProxyService>();
builder.Services.AddSingleton<ICache, Cache>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddHttpClient(NamedHttpClients.Default).ConfigurePrimaryHttpMessageHandler(_ =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
    {
        string expectedThumbprint = "10CE57B0083EBF09ED8E53CF6AC33D49B3A76414";
        if (certificate!.GetCertHashString() == expectedThumbprint)
            return true;

        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        return false;
    };
    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
    handler.SslProtocols = SslProtocols.Tls12;
    handler.ClientCertificates.Add(new X509Certificate2(Path.Combine(pathToExe, "server.pfx"), "son"));

    return handler;
});

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
app.UseHttpsRedirection();
app.UseWebSockets();
app.UseMiddleware<HttpRequestProxyMiddleware>();
app.UseMiddleware<WebSocketProxyMiddleware>();

app.Run();
public partial class ProgramMock { }
