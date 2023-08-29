using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;
using ZiraLink.Server.Services;

namespace ZiraLink.Server.UnitTests
{
    [ExcludeFromCodeCoverage]
    public class ZiraApiClientTests
    {
        [Fact]
        public async Task GetProjects_TokenIsNull_ShouldThrowAnException()
        {
            // Arrange
            var identityServiceMock = new Mock<IIdentityService>();
            var cancellationToken = CancellationToken.None;

            var ziraApiClient = new ZiraApiClient(null, null, identityServiceMock.Object);

            // Act
            var action = () => ziraApiClient.GetProjectsAsync(cancellationToken);

            // Assert
            var exception = await Assert.ThrowsAsync<ApplicationException>(action);
            Assert.Equal("Token request failed", exception.Message);
        }

        [Fact]
        public async Task GetProjects_TokenHasError_ShouldThrowAnException()
        {
            // Arrange
            var identityServiceMock = new Mock<IIdentityService>();
            var cancellationToken = CancellationToken.None;

            var tokenResponse = new IdentityModel.Client.TokenResponse();
            var tokenResponseType = tokenResponse.GetType();
            tokenResponseType.GetProperty("ErrorType")!.SetValue(tokenResponse, ResponseErrorType.Exception);
            tokenResponseType.GetProperty("ErrorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(tokenResponse, "ERROR_MESSAGE");

            identityServiceMock.Setup(m => m.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tokenResponse);

            var ziraApiClient = new ZiraApiClient(null, null, identityServiceMock.Object);

            // Act
            var action = () => ziraApiClient.GetProjectsAsync(cancellationToken);

            // Assert
            var exception = await Assert.ThrowsAsync<ApplicationException>(action);
            Assert.Equal("Token request failed, ERROR_MESSAGE", exception.Message);
        }

        [Fact]
        public async Task GetProjects_ApiDoesntReturnOk_ShouldThrowAnException()
        {
            // Arrange
            var identityServiceMock = new Mock<IIdentityService>();
            var cancellationToken = CancellationToken.None;

            var tokenResponse = new IdentityModel.Client.TokenResponse();
            identityServiceMock.Setup(m => m.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tokenResponse);

            var httpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var httpResponse = new HttpResponseMessage() { StatusCode = System.Net.HttpStatusCode.NotFound };
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(httpResponse)
                .Verifiable();
            var httpClient = new HttpClient(httpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://aghdam.nl/")
            };
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(m => m.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var configurations = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ZIRALINK_API_URL", "https://api.ziralink.aghdam.nl" }
                }).Build();

            var ziraApiClient = new ZiraApiClient(configurations, mockHttpClientFactory.Object, identityServiceMock.Object);

            // Act
            var action = () => ziraApiClient.GetProjectsAsync(cancellationToken);

            // Assert
            var exception = await Assert.ThrowsAsync<ApplicationException>(action);
            Assert.Equal("Failed to get projects", exception.Message);
        }

        [Fact]
        public async Task GetProjects_ApiReturnsError_ShouldThrowAnException()
        {
            // Arrange
            var identityServiceMock = new Mock<IIdentityService>();
            var cancellationToken = CancellationToken.None;

            var tokenResponse = new IdentityModel.Client.TokenResponse();
            identityServiceMock.Setup(m => m.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tokenResponse);

            var httpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var apiResult = ApiResponse<List<Project>>.CreateFailureResponse();
            apiResult.ErrorMessage = "ERROR_MESSAGE";
            var httpResponse = new HttpResponseMessage() { Content = new StringContent(JsonSerializer.Serialize(apiResult, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })) };
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(httpResponse)
                .Verifiable();
            var httpClient = new HttpClient(httpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://aghdam.nl/")
            };
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(m => m.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var configurations = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ZIRALINK_API_URL", "https://api.ziralink.aghdam.nl" }
                }).Build();

            var ziraApiClient = new ZiraApiClient(configurations, mockHttpClientFactory.Object, identityServiceMock.Object);

            // Act
            var action = () => ziraApiClient.GetProjectsAsync(cancellationToken);

            // Assert
            var exception = await Assert.ThrowsAsync<ApplicationException>(action);
            Assert.Equal("ERROR_MESSAGE", exception.Message);
        }

        [Fact]
        public async Task GetProjects_ApiReturnOk_ShouldReturnListOfProjects()
        {
            // Arrange
            var identityServiceMock = new Mock<IIdentityService>();
            var cancellationToken = CancellationToken.None;

            var tokenResponse = new IdentityModel.Client.TokenResponse();
            identityServiceMock.Setup(m => m.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tokenResponse);

            var httpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var projects = new List<Project> { new Project { Id = 1 } };
            var json = JsonSerializer.Serialize(ApiResponse<List<Project>>.CreateSuccessResponse(projects), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var httpResponse = new HttpResponseMessage() { Content = new StringContent(json) };
            httpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(httpResponse)
                .Verifiable();
            var httpClient = new HttpClient(httpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://aghdam.nl/")
            };
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(m => m.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var configurations = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ZIRALINK_API_URL", "https://api.ziralink.aghdam.nl" }
                }).Build();

            var ziraApiClient = new ZiraApiClient(configurations, mockHttpClientFactory.Object, identityServiceMock.Object);

            // Act
            var result = await ziraApiClient.GetProjectsAsync(cancellationToken);

            // Assert
            Assert.Equal(1, result[0].Id);
            Assert.Single(result);
        }
    }
}
