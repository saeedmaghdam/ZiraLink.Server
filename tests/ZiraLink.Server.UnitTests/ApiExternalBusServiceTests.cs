using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Server.Enums;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.UnitTests
{
    [ExcludeFromCodeCoverage]
    public class ApiExternalBusServiceTests
    {
        [Fact]
        public async Task Initialize_ShouldSetupQueueAndInitializeConsumer()
        {
            // Arrange
            var channelMock = new Mock<IModel>();
            var projectServiceMock = new Mock<IProjectService>();
            var appProjectServiceMock = new Mock<IAppProjectService>();
            var cancellationToken = CancellationToken.None;
            var apiExternalBusService = new ApiExternalBusService(channelMock.Object, projectServiceMock.Object, appProjectServiceMock.Object);

            var queueName = "api_to_server_external_bus";
            var projects = new List<Project>() { new Project { State = Enums.ProjectState.Active, DomainType = DomainType.Custom, Domain = "ziralink.local" } };



            // Act
            await apiExternalBusService.InitializeAsync(cancellationToken);

            // Assert
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            projectServiceMock.Verify(m => m.UpdateProjectsAsync(It.IsAny<CancellationToken>()), Times.Once);
            appProjectServiceMock.Verify(m => m.UpdateAppProjectsAsync(It.IsAny<CancellationToken>()), Times.Once);
            channelMock.Verify(m => m.BasicConsume(queueName, false, "", false, false, null, It.IsAny<IBasicConsumer>()), Times.Once);
        }
    }
}
