using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.UnitTests
{
    [ExcludeFromCodeCoverage]
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
            var cacheMock = new Mock<ICache>();
            var webSocketMock = new Mock<IWebSocket>();
            var httpContextMock = new Mock<HttpContext>();

            var username = "logon";
            var project = new Project()
            {
                DomainType = Enums.DomainType.Custom,
                Domain = "ziralink.local",
                Customer = new Customer { Username = username },
                InternalUrl = "https://localhost:3000"
            };

            var receivedCallsCount = 0;
            Func<WebSocketReceiveResult> getWebSocketReceiveResult = () =>
            {
                if (receivedCallsCount == 2)
                    return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                else
                    return new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);
            };

            cacheMock.Setup(m => m.SetWebSocket(project.GetProjectHost(configuration), webSocketMock.Object)).Returns(webSocketMock.Object);
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), cancellationToken)).Callback(() => receivedCallsCount++).ReturnsAsync(getWebSocketReceiveResult);
            channelMock.Setup(m => m.CreateBasicProperties()).Returns(basicPropertiesMock.Object);

            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", project.InternalUrl);
            headers.Add("Host", project.GetProjectHost(configuration));
            basicPropertiesMock.SetupGet(m => m.Headers).Returns(headers);

            var webSocketService = new WebSocketService(configuration, channelMock.Object, cacheMock.Object);

            // Act
            await webSocketService.Initialize(webSocketMock.Object, project);

            // Assert
            var queueName = $"{username}_websocket_server_bus";
            var exchangeName = "websocket_bus";
            cacheMock.Verify(m => m.SetWebSocket(project.GetProjectHost(configuration), webSocketMock.Object), Times.Once);
            cacheMock.Verify(m => m.RemoveWebSocket(project.GetProjectHost(configuration)), Times.Once);
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
            var cacheMock = new Mock<ICache>();
            var webSocketMock = new Mock<IWebSocket>();
            var httpContextMock = new Mock<HttpContext>();

            var username = "logon";
            var project = new Project()
            {
                Customer = new Customer { Username = username },
                InternalUrl = "https://localhost:3000"
            };

            cacheMock.Setup(m => m.SetWebSocket(project.GetProjectHost(configuration), webSocketMock.Object)).Returns(webSocketMock.Object);
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), cancellationToken)).ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            channelMock.Setup(m => m.CreateBasicProperties()).Returns(basicPropertiesMock.Object);

            var webSocketService = new WebSocketService(configuration, channelMock.Object, cacheMock.Object);

            // Act
            await webSocketService.Initialize(webSocketMock.Object, project);

            // Assert
            var queueName = $"{username}_websocket_server_bus";
            var exchangeName = "websocket_bus";
            cacheMock.Verify(m => m.SetWebSocket(project.GetProjectHost(configuration), webSocketMock.Object), Times.Once);
            cacheMock.Verify(m => m.RemoveWebSocket(project.GetProjectHost(configuration)), Times.Once);
            channelMock.Verify(m => m.ExchangeDeclare(exchangeName, "direct", false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueBind(queueName, exchangeName, queueName, null), Times.Once);
            webSocketMock.Verify(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), cancellationToken), Times.Exactly(1));
        }

        [Fact]
        public void InitializeConsumer_ShouldInitializeQueueAndStartConsuming()
        {
            // Arrange
            var queueName = "websocket_client_bus";
            var channelMock = new Mock<IModel>();

            var webSocketService = new WebSocketService(null, channelMock.Object, null);

            // Act
            webSocketService.InitializeConsumer();

            // Assert
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.BasicConsume(queueName, false, "", false, false, null, It.IsAny<IBasicConsumer>()), Times.Once);
        }
    }
}
