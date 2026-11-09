using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Drovik.Identity.Api.Domain;
using Drovik.Identity.Api.Contracts;
using Drovik.Identity.Api.Persistence;
using Drovik.Identity.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Drovik.Foundation.Tests;

public sealed class IdentityServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly TestIdentityFactory _factory;
    private HttpClient _client = null!;

    public IdentityServiceTests() => _factory = new TestIdentityFactory(_connection);

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await database.Database.EnsureCreatedAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Administrator", "Operator" })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Login_rejects_unknown_and_wrong_credentials_without_enumerating_accounts()
    {
        await CreateUser("operator@example.test", "Operator");
        using var wrongPassword = await Login("operator@example.test", "Incorrect-Password-1");
        using var unknownUser = await Login("missing@example.test", "Incorrect-Password-1");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownUser.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_issues_valid_access_token_and_expired_token_is_rejected()
    {
        var user = await CreateUser("operator@example.test", "Operator");
        using var login = await Login("operator@example.test");
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(session);
        Assert.Equal("Bearer", session.TokenType);
        Assert.InRange((session.ExpiresAtUtc - DateTime.UtcNow).TotalMinutes, 14.9, 15.0);
        Assert.Equal("Operator", Assert.Single(session.User.Roles));
        var rawRefreshToken = CookieValue(login, "drovik.refresh");
        Assert.DoesNotContain(rawRefreshToken, await login.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var refreshSetCookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("drovik.refresh=", StringComparison.Ordinal));
        Assert.Contains("httponly", refreshSetCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", refreshSetCookie, StringComparison.OrdinalIgnoreCase);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var storedToken = await database.RefreshTokens.SingleAsync();
            Assert.NotEqual(rawRefreshToken, storedToken.TokenHash);
            Assert.Equal(TokenIssuer.HashToken(rawRefreshToken), storedToken.TokenHash);
            Assert.InRange((storedToken.ExpiresAtUtc - DateTime.UtcNow).TotalDays, 6.999, 7.0);
            var storedUser = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id.ToString());
            Assert.NotNull(storedUser?.PasswordHash);
            Assert.NotEqual("Strong-Pass-2026", storedUser.PasswordHash);
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var valid = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        var expired = _factory.CreateToken(user.Id, DateTime.UtcNow.AddMinutes(-1));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);
        using var invalid = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task Refresh_rotates_tokens_rejects_reuse_and_logout_revokes_session()
    {
        await CreateUser("operator@example.test", "Operator");
        using var login = await Login("operator@example.test");
        var oldRefresh = CookieValue(login, "drovik.refresh");
        var csrf = await GetCsrf();
        using var refresh = await SendCsrfRequest(HttpMethod.Post, "/api/v1/auth/refresh", csrf, oldRefresh);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.NotEqual(oldRefresh, CookieValue(refresh, "drovik.refresh"));
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual((await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken, refreshed.AccessToken);

        using var replay = await SendCsrfRequest(HttpMethod.Post, "/api/v1/auth/refresh", csrf, oldRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var currentRefresh = CookieValue(refresh, "drovik.refresh");
        using var afterReplay = await SendCsrfRequest(HttpMethod.Post, "/api/v1/auth/refresh", csrf, currentRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    [Fact]
    public async Task Role_authorization_allows_only_administrators_to_create_users()
    {
        await CreateUser("operator@example.test", "Operator");
        await CreateUser("admin@example.test", "Administrator");
        var operatorToken = await LoginAndGetToken("operator@example.test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        using var forbidden = await _client.PostAsJsonAsync("/api/v1/users", new { email = "new@example.test", password = "Strong-Pass-2026", role = "Operator" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var adminToken = await LoginAndGetToken("admin@example.test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var created = await _client.PostAsJsonAsync("/api/v1/users", new { email = "new@example.test", password = "Strong-Pass-2026", role = "Operator" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Logout_expires_cookies_and_revokes_refresh_session()
    {
        await CreateUser("operator@example.test", "Operator");
        using var login = await Login("operator@example.test");
        var csrf = await GetCsrf();
        var refreshCookie = CookieValue(login, "drovik.refresh");

        using var logout = await SendCsrfRequest(HttpMethod.Post, "/api/v1/auth/logout", csrf, refreshCookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), value => value.StartsWith("drovik.refresh=;", StringComparison.Ordinal) && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        using var refreshAfterLogout = await SendCsrfRequest(HttpMethod.Post, "/api/v1/auth/refresh", csrf, refreshCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Refresh_and_logout_require_valid_csrf_and_origin()
    {
        using var response = await _client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Origin", "http://attacker.example");
        request.Headers.Add("X-CSRF-TOKEN", "invalid");
        request.Headers.Add("Cookie", "drovik.csrf=invalid");
        using var rejected = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task Openapi_document_is_published_for_identity_contract()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/auth/login", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/users", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        await CreateUser("operator@example.test", "Operator");
        using var login = await Login("operator@example.test");
        var refreshCookie = CookieValue(login, "drovik.refresh");
        var csrf = await GetCsrf();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var row = await database.RefreshTokens.SingleAsync();
            row.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await database.SaveChangesAsync();
        }

        using var response = await SendCsrfRequest(HttpMethod.Post, "/api/v1/auth/refresh", csrf, refreshCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_creates_first_administrator_once_and_refuses_repetition()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal(0, await BootstrapAdmin.RunAsync(_factory.Services, configuration));
        Assert.Equal(3, await BootstrapAdmin.RunAsync(_factory.Services, configuration));
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await userManager.FindByEmailAsync("first-admin@example.test");
        Assert.NotNull(created);
        Assert.Contains("Administrator", await userManager.GetRolesAsync(created));
    }

    [Fact]
    public async Task Administrator_lists_deactivates_and_reactivates_users()
    {
        var target = await CreateUser("operator@example.test", "Operator");
        await CreateUser("admin@example.test", "Administrator");
        var token = await LoginAndGetToken("admin@example.test");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var list = await client.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(2, (await list.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());

        using var off = await client.PatchAsJsonAsync($"/api/v1/users/{target.Id}/status", new { isActive = false });
        Assert.False((await off.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean());
        await Assert.ThrowsAnyAsync<Exception>(() => LoginAndGetToken("operator@example.test"));

        using var on = await client.PatchAsJsonAsync($"/api/v1/users/{target.Id}/status", new { isActive = true });
        Assert.True((await on.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean());
        Assert.False(string.IsNullOrEmpty(await LoginAndGetToken("operator@example.test")));
    }

    [Fact]
    public async Task Administrator_cannot_deactivate_their_own_account()
    {
        var admin = await CreateUser("admin@example.test", "Administrator");
        var token = await LoginAndGetToken("admin@example.test");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PatchAsJsonAsync($"/api/v1/users/{admin.Id}/status", new { isActive = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<ApplicationUser> CreateUser(string email, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "Strong-Pass-2026")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user;
    }

    private async Task<HttpResponseMessage> Login(string email, string password = "Strong-Pass-2026")
    {
        var csrf = await GetCsrf();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", $"drovik.csrf={csrf}");
        return await _client.SendAsync(request);
    }

    private async Task<string> LoginAndGetToken(string email)
    {
        using var response = await Login(email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> GetCsrf()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        request.Headers.Add("Origin", "http://localhost:4200");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csrfCookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("drovik.csrf=", StringComparison.Ordinal));
        Assert.Contains("httponly", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", csrfCookie, StringComparison.OrdinalIgnoreCase);
        return (await response.Content.ReadFromJsonAsync<CsrfResponse>())!.Token;
    }

    private async Task<HttpResponseMessage> SendCsrfRequest(HttpMethod method, string path, string csrf, string refreshCookie)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", $"drovik.csrf={csrf}; drovik.refresh={refreshCookie}");
        return await _client.SendAsync(request);
    }

    private static string CookieValue(HttpResponseMessage response, string name)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(name + "=", StringComparison.Ordinal));
        return setCookie[(name.Length + 1)..].Split(';')[0];
    }

    private sealed class TestIdentityFactory(SqliteConnection connection) : WebApplicationFactory<Drovik.Identity.Api.IdentityAssemblyMarker>
    {
        private readonly RSA _rsa = RSA.Create(2048);

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            var privateKey = Convert.ToBase64String(_rsa.ExportPkcs8PrivateKey());
            var publicKey = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDatabase"] = "Data Source=ignored",
                ["Jwt:PrivateKeyPkcs8Base64"] = privateKey,
                ["Jwt:PublicKeySpkiBase64"] = publicKey,
                ["DROVIK_BOOTSTRAP_ADMIN_EMAIL"] = "first-admin@example.test",
                ["DROVIK_BOOTSTRAP_ADMIN_PASSWORD"] = "Strong-Pass-2026",
                ["Security:AllowedOrigins"] = "http://localhost:4200"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<IdentityDbContext>>();
                services.RemoveAll<DbContextOptions<IdentityDbContext>>();
                services.RemoveAll<IdentityDbContext>();
                services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
            });
        }

        public string CreateToken(Guid userId, DateTime expires)
        {
            var key = new RsaSecurityKey(_rsa);
            var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
            var token = new JwtSecurityToken("DROVIK.Identity", "DROVIK.Services",
                [new Claim("sub", userId.ToString()), new Claim("role", "Operator")], DateTime.UtcNow.AddMinutes(-5), expires, credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
