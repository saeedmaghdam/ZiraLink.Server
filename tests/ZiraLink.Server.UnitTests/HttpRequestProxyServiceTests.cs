using System.Diagnostics.CodeAnalysis;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.UnitTests
{
    [ExcludeFromCodeCoverage]
    public class HttpRequestProxyServiceTests
    {
        [Fact]
        public async Task InitializeConsumerAsync_ShouldInitializeQueuesAndStartConsuming()
        {
            // Arrange
            var queueName = $"response_bus";
            var exchangeName = "response";

            var apiExternalBusService = new Mock<IApiExternalBusService>();
            var channelMock = new Mock<IModel>();
            var responseCompletionSourcesMock = new Mock<ResponseCompletionSources>();

            var httpRequestProxyService = new HttpRequestProxyService(apiExternalBusService.Object, channelMock.Object, responseCompletionSourcesMock.Object);

            // Act
            await httpRequestProxyService.InitializeConsumerAsync(CancellationToken.None);

            // Assert
            apiExternalBusService.Verify(m => m.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
            channelMock.Verify(m => m.ExchangeDeclare(exchangeName, "direct", false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueBind(queueName, exchangeName, "", null), Times.Once);
            channelMock.Verify(m => m.BasicConsume(queueName, false, "", false, false, null, It.IsAny<IBasicConsumer>()), Times.Once);
        }
    }
}
