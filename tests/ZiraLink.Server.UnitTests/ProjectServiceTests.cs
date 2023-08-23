using Microsoft.Extensions.Caching.Memory;
using Moq;
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
        public void GetByHost_HostExistsInCache_ShouldThrowAnApplicationException()
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
        }
    }
}
