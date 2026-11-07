using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Drovik.Identity.Api.Contracts;
using Drovik.Identity.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Drovik.Identity.Api.Security;

public sealed class TokenIssuer(IOptions<JwtOptions> options, JwtSigningKey signingKey, UserManager<ApplicationUser> userManager)
{
    private readonly JwtOptions _options = options.Value;

    public async Task<AuthResponse> IssueAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credentials = new SigningCredentials(signingKey.SecurityKey, SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        var jwt = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now, expires, credentials);
        string encodedToken;
        lock (signingKey)
            encodedToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new AuthResponse(encodedToken, "Bearer", expires,
            new AuthUserDto(user.Id, user.Email ?? "", roles.ToArray()));
    }

    public static string CreateOpaqueToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
