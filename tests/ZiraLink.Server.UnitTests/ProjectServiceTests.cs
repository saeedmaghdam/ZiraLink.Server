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
            var projectService = new ProjectService(null, null, cacheMock.Object);

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
            var projectService = new ProjectService(null, null, cacheMock.Object);

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
            var projectService = new ProjectService(null, null, cacheMock.Object);

            // Act
            var action = () => projectService.GetByHost(host);
            var exception = Record.Exception(action);

            // Assert
            Assert.Null(exception);
            cacheMock.Verify(m => m.TryGetProject(host, out It.Ref<Project>.IsAny), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }
    }
}
