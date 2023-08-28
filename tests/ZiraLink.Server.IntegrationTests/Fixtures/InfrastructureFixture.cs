using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Networks;
using System.Diagnostics.CodeAnalysis;
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
            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            _certificatePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "certs", "localhost", "server.pfx");
            _certificatePassword = "son";

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
              .WithNetworkAliases("swa")
              .WithPortBinding(9080, 80)
              .WithPortBinding(SampleWebApplicationPort, 443)
              .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Test")
              .WithEnvironment("ASPNETCORE_URLS", "http://+:80;https://+:443")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "443")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeIds()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.ids/ids:main")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("ids")
              .WithPortBinding(5200, 5000)
              .WithPortBinding(IdsPort, 5001)
              .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Test")
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_DB", "Data Source=/app/database.db")
              .WithEnvironment("ZIRALINK_ISSUER_URL", $"https://localhost:{IdsPort}")
              .WithEnvironment("ZIRALINK_API_URL", $"https://localhost:{ApiPort}")
              .WithEnvironment("ZIRALINK_CLIENT_URL", $"https://localhost:{ClientPort}")
              .WithEnvironment("ASPNETCORE_URLS", "http://+:5000;https://+:5001")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "5001")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "./certs/localhost/server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5000)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeApi()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.api/api:main")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("api")
              .WithPortBinding(6200, 6000)
              .WithPortBinding(ApiPort, 6001)
              .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Test")
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_DB", "Data Source=/app/database.db")
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_RABBITMQ", $"amqp://user:Pass123$@rabbitmq:5672/")
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_REDIS", $"redis:6379")
              .WithEnvironment("ZIRALINK_REDIS_PASSWORD", "")
              .WithEnvironment("ZIRALINK_URL_IDS", $"https://ids:5001")
              .WithEnvironment("ZIRALINK_REDIRECTURI", "https://localhost:4201/signin-result")
              .WithEnvironment("ZIRALINK_WEB_URL", "https://localhost:4201")
              .WithEnvironment("ASPNETCORE_URLS", "http://+:6000;https://+:6001")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "6001")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "./certs/localhost/server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(6000)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeClient()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.client/client:main")
              .WithCleanUp(true)
              .WithNetwork(_network)
              .WithNetworkAliases("client")
              .WithPortBinding(8396, 8196)
              .WithPortBinding(ClientPort, 8197)
              .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Test")
              .WithEnvironment("ZIRALINK_CONNECTIONSTRINGS_RABBITMQ", $"amqp://user:Pass123$@rabbitmq:5672/")
              .WithEnvironment("ZIRALINK_URL_IDS", $"https://ids:5001")
              .WithEnvironment("ASPNETCORE_URLS", "http://+:8196;https://+:8197")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "8197")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "./certs/localhost/server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithEnvironment("ASPNETCORE_Kestrel__Endpoints__Http__Url", $"http://+:8196")
              .WithEnvironment("ASPNETCORE_Kestrel__Endpoints__HttpsInlineCertFile__Url", $"https://+:8197")
              .WithEnvironment("ASPNETCORE_Kestrel__Endpoints__HttpsInlineCertFile__Certificate__Path", "./certs/localhost/server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Endpoints__HttpsInlineCertFile__Certificate__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8196)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            string expectedThumbprint = "10CE57B0083EBF09ED8E53CF6AC33D49B3A76414";
            if (certificate!.GetCertHashString() == expectedThumbprint)
                return true;

            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            return false;
        }
    }
}
