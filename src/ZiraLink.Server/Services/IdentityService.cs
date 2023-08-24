using IdentityModel.Client;
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
            var client = _httpClientFactory.CreateClient();

            // discover endpoints from metadata
            var disco = await client.GetDiscoveryDocumentAsync(_configuration["ZIRALINK_IDS_URL"], cancellationToken);
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
