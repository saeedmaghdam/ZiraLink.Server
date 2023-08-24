using IdentityModel.Client;

namespace ZiraLink.Server.Framework.Services
{
    public interface IIdentityService
    {
        Task<TokenResponse> GetTokenAsync(CancellationToken cancellationToken);
    }
}
