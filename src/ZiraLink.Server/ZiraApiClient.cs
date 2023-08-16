using System.Text.Json;
using IdentityModel.Client;
using ZiraLink.Server.Models;

namespace ZiraLink.Server
{
    public class ZiraApiClient
    {
        public async Task<List<Project>> GetProjects(CancellationToken cancellationToken)
        {
            var client = new HttpClient();

            // discover endpoints from metadata
            var disco = await client.GetDiscoveryDocumentAsync("https://ids.ziralink.com:5001/");
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

            var response = await apiClient.GetAsync("https://api.ziralink.com:6001/Project/All");
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
