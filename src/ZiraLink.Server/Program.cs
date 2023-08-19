using System.Reflection;
using ZiraLink.Server;
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

// Add services to the container.
builder.Services.AddSingleton<ResponseCompletionSources>();
builder.Services.AddSingleton<ZiraApiClient>();
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<WebSocketService>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

var webSocketService = app.Services.GetRequiredService<WebSocketService>();
await webSocketService.InitializeConsumer();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseWebSockets();
app.UseMiddleware<ProxyMiddleware>();

app.Run();
