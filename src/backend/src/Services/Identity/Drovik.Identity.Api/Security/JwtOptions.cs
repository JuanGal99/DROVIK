namespace Drovik.Identity.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "DROVIK.Identity";
    public string Audience { get; set; } = "DROVIK.Services";
    public string PrivateKeyPkcs8Base64 { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
}
