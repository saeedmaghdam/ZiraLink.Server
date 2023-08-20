using System.Text.Json;
using IdentityModel.Client;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class ZiraApiClient
    {
        private readonly IConfiguration _configuration;

        public ZiraApiClient(IConfiguration configuration) => _configuration = configuration;

        public async Task<List<Project>> GetProjects(CancellationToken cancellationToken)
        {
            var client = new HttpClient();

            // discover endpoints from metadata
            var disco = await client.GetDiscoveryDocumentAsync(_configuration["ZIRALINK_IDS_URL"]);
            if (disco.IsError)
                throw new ApplicationException("Discovery not found");

            // request token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,

                ClientId = "back",
                ClientSecret = "secret",
                Scope = "ziralink"
            });

            if (tokenResponse.IsError)
                throw new ApplicationException("Token request failed");

            var apiClient = new HttpClient();
            apiClient.SetBearerToken(tokenResponse.AccessToken!);

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
    }
}
