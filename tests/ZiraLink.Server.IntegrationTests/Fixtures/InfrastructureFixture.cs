using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ZiraLink.Server.IntegrationTests.Fixtures
{
    [ExcludeFromCodeCoverage]
    public class InfrastructureFixture
    {
        private readonly IConfiguration _configuration;

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _certificatePath;
        private readonly string _certificatePassword;
        private INetwork _network;

        public string CertificatePath => _certificatePath;
        public string CertificatePassword => _certificatePassword;
        public const string RabbitMqHost = "localhost";
        public const int RabbitMqPort = 5872;
        public const string RabbitMqUsername = "user";
        public const string RabbitMqPassword = "Pass123$";
        public const int RedisPort = 6579;
        public const int IdsPort = 5201;
        public const int ApiPort = 6201;
        public const int ClientPort = 8397;
        public const int SampleWebApplicationPort = 9443;

        public InfrastructureFixture()
        {
            var pathToExe = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);

            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            _certificatePath = Path.Combine(pathToExe, "certs", "s3d-me-localdev-server.pfx");
            _certificatePassword = _configuration["ASPNETCORE_Kestrel__Certificates__Default__Password"]!;

            InitializeNetwork();
            InitializeRabbitMq();
            InitializeRedis();
            InitializeSampleWebApplication();
            InitializeIds();
            InitializeApi();
            InitializeClient();
        }

        public HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = RemoteCertificateValidationCallback;
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.SslProtocols = SslProtocols.Tls12;
            handler.ClientCertificates.Add(new X509Certificate2(CertificatePath, CertificatePassword));
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost:9443/")
            };

            return httpClient;
        }

        public async Task<ClientWebSocket> CreateWebSocketClientAsync()
        {
            var webSocketClient = new ClientWebSocket();
            webSocketClient.Options.ClientCertificates.Add(new X509Certificate2(CertificatePath, CertificatePassword));
            webSocketClient.Options.RemoteCertificateValidationCallback = RemoteCertificateValidationCallback;

            await webSocketClient.ConnectAsync(new Uri("wss://localhost:9443"), _cancellationTokenSource.Token);

            return webSocketClient;
        }

        private void InitializeNetwork()
        {
            _network = new NetworkBuilder()
              .WithName(Guid.NewGuid().ToString("D"))
              .WithCleanUp(true)
              .WithDriver(NetworkDriver.Bridge)
              .Build();
        }

        private void InitializeRabbitMq()
        {
            var container = new ContainerBuilder()
              .WithImage("bitnami/rabbitmq:latest")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("rabbitmq")
              .WithPortBinding(RabbitMqPort, 5672)
              .WithPortBinding(15872, 15672)
              .WithEnvironment("RABBITMQ_USERNAME", RabbitMqUsername)
              .WithEnvironment("RABBITMQ_PASSWORD", RabbitMqPassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672).UntilHttpRequestIsSucceeded(r => r.ForPort(15672)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeRedis()
        {
            var container = new ContainerBuilder()
              .WithImage("bitnami/redis:7.0.7")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("redis")
              .WithPortBinding(RedisPort, 6379)
              .WithEnvironment("ALLOW_EMPTY_PASSWORD", "yes")
              .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeSampleWebApplication()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.client/sample-web-application:main")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("swa.localdev.me")
              .WithPortBinding(10050, 10050)
              .WithPortBinding(10051, 10051)
              .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Test")
              .WithEnvironment("ASPNETCORE_URLS", "http://+:10050;https://+:10051")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "10051")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/app/certs/s3d-me-localdev-server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(req => req
                .ForPort(10050)
                .ForPath("/")
                .ForStatusCode(HttpStatusCode.OK)
                .ForStatusCode(HttpStatusCode.Moved)
                .ForStatusCode(HttpStatusCode.MovedPermanently)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeIds()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.ids/ids:main")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("ids.localdev.me")
              .WithPortBinding(5500, 5500)
              .WithPortBinding(5501, 5501)
              .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Test")
              .WithEnvironment("ZIRALINK_CERT_THUMBPRINT_LOCALHOST", _configuration["ZIRALINK_CERT_THUMBPRINT_LOCALHOST"]!)
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_DB", "Data Source=/app/database.db")
              .WithEnvironment("ZIRALINK_ISSUER_URL", $"https://ids.localdev.me:5501")
              .WithEnvironment("ZIRALINK_API_URL", $"https://api.localdev.me:6501")
              .WithEnvironment("ZIRALINK_CLIENT_URL", $"https://client.localdev.me:8501")
              .WithEnvironment("ASPNETCORE_URLS", "http://+:5500;https://+:5501")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "5501")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/app/certs/s3d-me-localdev-server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(req => req
                .ForPort(5500)
                .ForPath("/.well-known/openid-configuration")
                .ForStatusCode(HttpStatusCode.OK)
                .ForStatusCode(HttpStatusCode.Moved)
                .ForStatusCode(HttpStatusCode.MovedPermanently)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeApi()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.api/api:main")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("api.localdev.me")
              .WithPortBinding(6500, 6500)
              .WithPortBinding(6501, 6501)
              .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Test")
              .WithEnvironment("ZIRALINK_CERT_THUMBPRINT_LOCALHOST", _configuration["ZIRALINK_CERT_THUMBPRINT_LOCALHOST"]!)
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_DB", "Data Source=/app/database.db")
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_RABBITMQ", $"amqp://user:Pass123$@rabbitmq:5672/")
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_REDIS", $"redis:6379")
              .WithEnvironment("ZIRALINK_REDIS_PASSWORD", "")
              .WithEnvironment("ZIRALINK_URL_IDS", $"https://ids.localdev.me:5501")
              .WithEnvironment("ZIRALINK_REDIRECTURI", "https://api.localdev.me:6501/signin-result")
              .WithEnvironment("ZIRALINK_WEB_URL", "https://localdev.me:4501")
              .WithEnvironment("ASPNETCORE_URLS", "http://+:6500;https://+:6501")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "6501")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/app/certs/s3d-me-localdev-server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(req => req
                .ForPort(6500)
                .ForPath("/swagger/index.html")
                .ForStatusCode(HttpStatusCode.OK)
                .ForStatusCode(HttpStatusCode.Moved)
                .ForStatusCode(HttpStatusCode.MovedPermanently)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeClient()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.client/client:main")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("client.localdev.me")
              .WithPortBinding(8500, 8500)
              .WithPortBinding(8501, 8501)
              .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Test")
              .WithEnvironment("ZIRALINK_CERT_THUMBPRINT_LOCALHOST", _configuration["ZIRALINK_CERT_THUMBPRINT_LOCALHOST"]!)
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_RABBITMQ", $"amqp://user:Pass123$@rabbitmq:5672/")
              .WithEnvironment("ZIRALINK_URL_IDS", $"https://ids.localdev.me:5501")
              .WithEnvironment("ASPNETCORE_Kestrel__Endpoints__Http__Url", $"http://+:8500")
              .WithEnvironment("ASPNETCORE_Kestrel__Endpoints__HttpsInlineCertFile__Url", $"https://+:8501")
              .WithEnvironment("ASPNETCORE_Kestrel__Endpoints__HttpsInlineCertFile__Certificate__Path", "/app/certs/s3d-me-localdev-server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Endpoints__HttpsInlineCertFile__Certificate__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(req => req
                .ForPort(8500)
                .ForPath("/")
                .ForStatusCode(HttpStatusCode.OK)
                .ForStatusCode(HttpStatusCode.Moved)
                .ForStatusCode(HttpStatusCode.MovedPermanently)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            string expectedThumbprint = _configuration["ZIRALINK_CERT_THUMBPRINT_LOCALHOST"]!;
            if (certificate!.GetCertHashString() == expectedThumbprint)
                return true;

            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            return false;
        }
    }
}
