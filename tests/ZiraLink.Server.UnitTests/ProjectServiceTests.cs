using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Server.Enums;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.UnitTests
{
    public class ProjectServiceTests
    {
        [Fact]
        public void GetByHost_HostNotExistsInCache_ShouldThrowAnApplicationException()
        {
            // Arrange
            var memoryCacheMock = new Mock<IMemoryCache>();
            var projectService = new ProjectService(null, null, memoryCacheMock.Object, null);

            // Act
            var action = () => projectService.GetByHost("aghdam.nl");

            // Assert
            var exception = Assert.Throws<ApplicationException>(() => action());
            Assert.Equal("Project not found", exception.Message);
        }

        [Fact]
        public void GetByHost_HostIsEmpty_ShouldThrowAnApplicationException()
        {
            // Arrange
            var memoryCacheMock = new Mock<IMemoryCache>();
            var projectService = new ProjectService(null, null, memoryCacheMock.Object, null);

            // Act
            var action = () => projectService.GetByHost("");

            // Assert
            var exception = Assert.Throws<ArgumentNullException>(() => action());
            Assert.Equal("host", exception.ParamName);
        }

        [Fact]
        public void GetByHost_HostExistsInCache_ShouldNotThrowAnyException()
        {
            // Arrange
            var memoryCacheMock = new Mock<IMemoryCache>();

            var projectViewId = Guid.NewGuid();
            memoryCacheMock.Setup(m => m.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny)).Returns(true);
            var projectService = new ProjectService(null, null, memoryCacheMock.Object, null);

            // Act
            var action = () => projectService.GetByHost("aghdam.nl");
            var exception = Record.Exception(action);

            // Assert
            Assert.Null(exception);
            memoryCacheMock.Verify(m => m.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny), Times.Once);
        }

        [Fact]
        public async Task Initialize_ShouldSetupQueueAndInitializeConsumer()
        {
            // Arrange
            var configurationMock = new Mock<IConfiguration>();
            var ziraApiClientMock = new Mock<IZiraApiClient>();
            var cacheEntryMock = new Mock<ICacheEntry>();
            var memoryCacheMock = new Mock<IMemoryCache>();
            var channelMock = new Mock<IModel>();
            var cancellationToken = CancellationToken.None;
            var projectService = new ProjectService(ziraApiClientMock.Object, configurationMock.Object, memoryCacheMock.Object, channelMock.Object);
            var queueName = "api_to_server_external_bus";
            
            memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<object>())).Returns(cacheEntryMock.Object);
            ziraApiClientMock.Setup(m => m.GetProjectsAsync(cancellationToken)).ReturnsAsync(new List<Project>
            {
                new Project { State = Enums.ProjectState.Active, DomainType = DomainType.Custom, Domain = "aghdam.nl" }
            });

            // Act
            await projectService.InitializeAsync(cancellationToken);

            // Assert
            ziraApiClientMock.Verify(m => m.GetProjectsAsync(cancellationToken), Times.Once);
            memoryCacheMock.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Once);
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.BasicConsume(queueName, false, "", false, false, null, It.IsAny<IBasicConsumer>()), Times.Once);
        }
    }
}
