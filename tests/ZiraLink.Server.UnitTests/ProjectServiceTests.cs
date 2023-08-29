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
    public class ProjectServiceTests
    {
        [Fact]
        public void GetByHost_HostNotExistsInCache_ShouldThrowAnApplicationException()
        {
            // Arrange
            var host = "ziralink.local";
            var cacheMock = new Mock<ICache>();
            var projectService = new ProjectService(null, null, cacheMock.Object, null);

            // Act
            var action = () => projectService.GetByHost(host);

            // Assert
            var exception = Assert.Throws<ApplicationException>(() => action());
            Assert.Equal("Project not found", exception.Message);
            cacheMock.Verify(m => m.TryGetProject(host, out It.Ref<Project>.IsAny), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetByHost_HostIsEmpty_ShouldThrowAnApplicationException()
        {
            // Arrange
            var cacheMock = new Mock<ICache>();
            var projectService = new ProjectService(null, null, cacheMock.Object, null);

            // Act
            var action = () => projectService.GetByHost("");

            // Assert
            var exception = Assert.Throws<ArgumentNullException>(() => action());
            Assert.Equal("host", exception.ParamName);
            cacheMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetByHost_HostExistsInCache_ShouldNotThrowAnyException()
        {
            // Arrange
            var host = "ziralink.local";
            var cacheMock = new Mock<ICache>();

            var projectViewId = Guid.NewGuid();
            cacheMock.Setup(m => m.TryGetProject(host, out It.Ref<Project>.IsAny)).Returns(true);
            var projectService = new ProjectService(null, null, cacheMock.Object, null);

            // Act
            var action = () => projectService.GetByHost(host);
            var exception = Record.Exception(action);

            // Assert
            Assert.Null(exception);
            cacheMock.Verify(m => m.TryGetProject(host, out It.Ref<Project>.IsAny), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Initialize_ShouldSetupQueueAndInitializeConsumer()
        {
            // Arrange
            var configurationMock = new Mock<IConfiguration>();
            var ziraApiClientMock = new Mock<IZiraApiClient>();
            var cacheMock = new Mock<ICache>();
            var channelMock = new Mock<IModel>();
            var cancellationToken = CancellationToken.None;
            var projectService = new ProjectService(ziraApiClientMock.Object, configurationMock.Object, cacheMock.Object, channelMock.Object);
            var queueName = "api_to_server_external_bus";
            var projects = new List<Project>() { new Project { State = Enums.ProjectState.Active, DomainType = DomainType.Custom, Domain = "ziralink.local" } };


            foreach(var project in projects)
                cacheMock.Setup(m => m.SetProject(project.GetProjectHost(configurationMock.Object), project)).Returns(project);
            ziraApiClientMock.Setup(m => m.GetProjectsAsync(cancellationToken)).ReturnsAsync(projects);

            // Act
            await projectService.InitializeAsync(cancellationToken);

            // Assert
            ziraApiClientMock.Verify(m => m.GetProjectsAsync(cancellationToken), Times.Once);
            foreach (var project in projects)
                cacheMock.Verify(m => m.SetProject(project.GetProjectHost(configurationMock.Object), project), Times.Once);
            cacheMock.VerifyNoOtherCalls();
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.BasicConsume(queueName, false, "", false, false, null, It.IsAny<IBasicConsumer>()), Times.Once);
        }
    }
}
