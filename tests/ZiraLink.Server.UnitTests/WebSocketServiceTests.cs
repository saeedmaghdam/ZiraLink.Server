using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Server.Models;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.UnitTests
{
    public class WebSocketServiceTests
    {
        [Fact]
        public async Task Initialize_ShouldInitializeRabbitMq()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ZIRALINK_DEFAULT_DOMAIN", ".app.ziralink.aghdam.nl" }
                })
                .Build();
            var cancellationToken = CancellationToken.None;
            var channelMock = new Mock<IModel>();
            var basicPropertiesMock = new Mock<IBasicProperties>();
            var memoryCacheMock = new Mock<IMemoryCache>();
            var webSocketMock = new Mock<WebSocket>();
            var httpContextMock = new Mock<HttpContext>();
            var cacheEntryMock = new Mock<ICacheEntry>();

            var receivedCallsCount = 0;
            Func<WebSocketReceiveResult> getWebSocketReceiveResult = () =>
            {
                if (receivedCallsCount == 2)
                    return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                else
                    return new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);
            };

            cacheEntryMock.SetupSet(m => m.Value = webSocketMock.Object).Verifiable();
            httpContextMock.Setup(m => m.WebSockets.AcceptWebSocketAsync()).ReturnsAsync(webSocketMock.Object);
            memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<object>())).Returns(cacheEntryMock.Object);
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), cancellationToken)).Callback(() => receivedCallsCount++).ReturnsAsync(getWebSocketReceiveResult);
            channelMock.Setup(m => m.CreateBasicProperties()).Returns(basicPropertiesMock.Object);

            var username = "saeedmaghdam";
            var project = new Project()
            {
                Customer = new Customer { Username = username },
                InternalUrl = "https://localhost:3000"
            };

            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", project.InternalUrl);
            headers.Add("Host", project.GetProjectHost(configuration));
            basicPropertiesMock.SetupGet(m => m.Headers).Returns(headers);

            var webSocketService = new WebSocketService(configuration, channelMock.Object, memoryCacheMock.Object);

            // Act
            await webSocketService.Initialize(httpContextMock.Object, project);

            // Assert
            var queueName = $"{username}_websocket_server_bus";
            var exchangeName = "websocket_bus";
            memoryCacheMock.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Once);
            channelMock.Verify(m => m.ExchangeDeclare(exchangeName, "direct", false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueBind(queueName, exchangeName, queueName, null), Times.Once);
            webSocketMock.Verify(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), cancellationToken), Times.Exactly(2));
            channelMock.Verify(m => m.BasicPublish(exchangeName, queueName, false, basicPropertiesMock.Object, It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
        }

        [Fact]
        public async Task Initialize_WebSocketMessageTypeIsClosed_ShouldNotPublish()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ZIRALINK_DEFAULT_DOMAIN", ".app.ziralink.aghdam.nl" }
                })
                .Build();
            var cancellationToken = CancellationToken.None;
            var channelMock = new Mock<IModel>();
            var basicPropertiesMock = new Mock<IBasicProperties>();
            var memoryCacheMock = new Mock<IMemoryCache>();
            var webSocketMock = new Mock<WebSocket>();
            var httpContextMock = new Mock<HttpContext>();
            var cacheEntryMock = new Mock<ICacheEntry>();

            cacheEntryMock.SetupSet(m => m.Value = webSocketMock.Object).Verifiable();
            httpContextMock.Setup(m => m.WebSockets.AcceptWebSocketAsync()).ReturnsAsync(webSocketMock.Object);
            memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<object>())).Returns(cacheEntryMock.Object);
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), cancellationToken)).ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            channelMock.Setup(m => m.CreateBasicProperties()).Returns(basicPropertiesMock.Object);

            var username = "saeedmaghdam";
            var project = new Project()
            {
                Customer = new Customer { Username = username },
                InternalUrl = "https://localhost:3000"
            };

            var webSocketService = new WebSocketService(configuration, channelMock.Object, memoryCacheMock.Object);

            // Act
            await webSocketService.Initialize(httpContextMock.Object, project);

            // Assert
            var queueName = $"{username}_websocket_server_bus";
            var exchangeName = "websocket_bus";
            memoryCacheMock.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Once);
            channelMock.Verify(m => m.ExchangeDeclare(exchangeName, "direct", false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueBind(queueName, exchangeName, queueName, null), Times.Once);
            webSocketMock.Verify(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), cancellationToken), Times.Exactly(1));
        }
    }
}
