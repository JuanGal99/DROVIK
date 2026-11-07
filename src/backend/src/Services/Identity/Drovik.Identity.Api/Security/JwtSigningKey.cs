using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Drovik.Identity.Api.Security;

public sealed class JwtSigningKey : IDisposable
{
    public RSA Rsa { get; }
    public RsaSecurityKey SecurityKey { get; }

    public JwtSigningKey(IOptions<JwtOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.PrivateKeyPkcs8Base64))
            throw new InvalidOperationException("Jwt:PrivateKeyPkcs8Base64 must be configured as a local secret.");
        Rsa = RSA.Create();
        Rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(options.Value.PrivateKeyPkcs8Base64), out _);
        if (Rsa.KeySize < 2048)
            throw new InvalidOperationException("The Identity RSA signing key must be at least 2048 bits.");
        SecurityKey = new RsaSecurityKey(Rsa);
    }

    public void Dispose() => Rsa.Dispose();
}
