using IdentityModel.Client;
using ZiraLink.Server.Enums;
using ZiraLink.Server.Framework.Services;

namespace ZiraLink.Server.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public IdentityService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<TokenResponse> GetTokenAsync(CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(NamedHttpClients.Default);

            // discover endpoints from metadata
            var uri = new Uri(_configuration["ZIRALINK_IDS_URL"]!);
            var disco = await client.GetDiscoveryDocumentAsync(uri.ToString(), cancellationToken);
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

            return tokenResponse;
        }
    }
}
