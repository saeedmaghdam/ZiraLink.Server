using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ZiraLink.Server.IntegrationTests.Fixtures;

namespace ZiraLink.Server.IntegrationTests
{
    [ExcludeFromCodeCoverage]
    [Collection("Infrastructure Collection")]
    public class ServerTests
    {
        private InfrastructureFixture _fixture;

        public ServerTests(InfrastructureFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task SendARequestToServer_ShouldReturnWeatherForecastsResultFromSWA()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await using var application = new WebApplicationFactory<ProgramMock>();
            using var client = application.CreateClient();

            // Act
            var result = await client.GetAsync("/", cancellationTokenSource.Token);
            var response = await result.Content.ReadAsStringAsync(cancellationTokenSource.Token);
            var weatherForecast = JsonSerializer.Deserialize<WeatherForecast[]>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // Assert
            Assert.NotNull(weatherForecast);
            Assert.Equal(5, weatherForecast!.Length);
        }

        [Fact]
        public async Task WebSocketTests()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await using var application = new WebApplicationFactory<ProgramMock>();
            using var client = application.CreateClient();
            var clientWebSocket = application.Server.CreateWebSocketClient();
            var webSocket = await clientWebSocket.ConnectAsync(new Uri("http://localhost/"), CancellationToken.None);

            // Act & Assert
            for (int i = 0; i < 1000; i++)
            {
                await SendMessageToWebSocket(webSocket, "ZiraLink", cancellationTokenSource.Token);
                var response = await ReceiveMessageFromWebSocket(webSocket, cancellationTokenSource.Token);

                Assert.Equal($"{i + 1}: ZiraLink", response);
            }
        }

        private static async Task SendMessageToWebSocket(WebSocket ws, string data, CancellationToken cancellationToken)
        {
            var encoded = Encoding.UTF8.GetBytes(data);
            var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
            await ws.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
        }

        private static async Task<string> ReceiveMessageFromWebSocket(WebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new Byte[8192]);
            var result = default(WebSocketReceiveResult);

            using (var ms = new MemoryStream())
            {
                do
                {
                    result = await ws.ReceiveAsync(buffer, cancellationToken);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(ms, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }
    }

    internal record WeatherForecast(DateTime Date, int TemperatureC, int TemperatureF, string? Summary);
}
