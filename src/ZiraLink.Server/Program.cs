using System.Reflection;
using ZiraLink.Server;

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
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseMiddleware<ProxyMiddleware>();

app.Run();
