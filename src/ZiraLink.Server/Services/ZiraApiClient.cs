using System.Text.Json;
using IdentityModel.Client;
using ZiraLink.Server.Enums;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class ZiraApiClient : IZiraApiClient
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IIdentityService _identityService;

        public ZiraApiClient(IConfiguration configuration, IHttpClientFactory httpClientFactory, IIdentityService identityService)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _identityService = identityService;
        }

        public async Task<List<Project>> GetProjectsAsync(CancellationToken cancellationToken)
        {
            var token = await _identityService.GetTokenAsync(cancellationToken);
            if (token == null) throw new ApplicationException("Token request failed");

            if (token.IsError)
                throw new ApplicationException($"Token request failed, {token.Error}");

            var apiClient = _httpClientFactory.CreateClient(NamedHttpClients.Default);
            apiClient.SetBearerToken(token.AccessToken!);

            var baseUri = new Uri(_configuration["ZIRALINK_API_URL"]!);
            var uri = new Uri(baseUri, "Project/All");
            var response = await apiClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
                throw new ApplicationException("Failed to get projects");

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<List<Project>>>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (result.Status == false)
                throw new ApplicationException(result.ErrorMessage);

            return result.Data;
        }

        public async Task<List<AppProject>> GetAppProjectsAsync(CancellationToken cancellationToken)
        {
            var token = await _identityService.GetTokenAsync(cancellationToken);
            if (token == null) throw new ApplicationException("Token request failed");

            if (token.IsError)
                throw new ApplicationException($"Token request failed, {token.Error}");

            var apiClient = _httpClientFactory.CreateClient(NamedHttpClients.Default);
            apiClient.SetBearerToken(token.AccessToken!);

            var baseUri = new Uri(_configuration["ZIRALINK_API_URL"]!);
            var uri = new Uri(baseUri, "AppProject/All");
            var response = await apiClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
                throw new ApplicationException("Failed to get app projects");

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<List<AppProject>>>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (result.Status == false)
                throw new ApplicationException(result.ErrorMessage);

            return result.Data;
        }
    }
}
