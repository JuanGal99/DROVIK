using Drovik.Observability;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Drovik.Identity.Api.Contracts;
using Drovik.Identity.Api.Domain;
using Drovik.Identity.Api.Persistence;
using Drovik.Identity.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

// A shared console: a session never outlives a working day, even if the browser restores its cookies.
var SessionLifetime = TimeSpan.FromHours(12);
var bootstrapMode = args.Contains("--bootstrap-admin", StringComparer.Ordinal);
var recoverMode = args.Contains("--recover-password", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args);
builder.AddDrovikObservability("identity");
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("IdentityDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:IdentityDatabase must be configured through local secrets.")));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddSignInManager();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<JwtSigningKey>();
builder.Services.AddScoped<TokenIssuer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => ConfigureJwtBearer(options, builder.Configuration));
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options => options.AddPolicy("login", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        })));
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseDrovikRequestLogging();
app.MapOpenApi("/openapi/v1.json");
app.UseForwardedHeaders();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (bootstrapMode && !app.Environment.IsDevelopment())
    throw new InvalidOperationException("The first-administrator bootstrap is available only in Development.");
if (app.Environment.IsDevelopment() || bootstrapMode)
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await database.Database.MigrateAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in new[] { "Administrator", "Operator" })
        if (!await roles.RoleExistsAsync(roleName))
            await roles.CreateAsync(new IdentityRole<Guid>(roleName));
    // Starting the application starts signed out, even if a browser still holds a session cookie.
    if (!bootstrapMode && !recoverMode) await UserAccounts.EndAllSessionsAsync(database, CancellationToken.None);
}
if (recoverMode)
{
    Environment.ExitCode = await RecoverPassword.RunAsync(app.Services, app.Configuration);
    return;
}
if (bootstrapMode)
{
    var result = await BootstrapAdmin.RunAsync(app.Services, app.Configuration);
    Environment.ExitCode = result;
    return;
}

var allowedOrigins = (builder.Configuration["Security:AllowedOrigins"] ?? "http://localhost:4200")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

app.MapGet("/api/v1/auth/csrf", (HttpContext context) =>
{
    if (!IsAllowedOrigin(context, allowedOrigins)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    context.Response.Cookies.Append("drovik.csrf", token, CookieOptions(context, httpOnly: true, TimeSpan.FromMinutes(30)));
    return Results.Ok(new CsrfResponse(token));
});

app.MapPost("/api/v1/auth/login", async (LoginRequest request, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, TokenIssuer tokenIssuer, IdentityDbContext database,
    HttpContext context, CancellationToken cancellationToken) =>
{
    if (!IsAllowedOrigin(context, allowedOrigins) || !ValidCsrf(context))
        return Problem(StatusCodes.Status400BadRequest, "Solicitud rechazada", "La validación CSRF falló.");
    if (string.IsNullOrWhiteSpace(request.Email) || !new EmailAddressAttribute().IsValid(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        return Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "El correo y la contraseña son obligatorios.");
    var user = await users.FindByEmailAsync(request.Email.Trim());
    if (user is null)
    {
        _ = users.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
        return Problem(StatusCodes.Status401Unauthorized, "Autenticación fallida", "El correo o la contraseña no son válidos.");
    }
    if (!(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
        return Problem(StatusCodes.Status401Unauthorized, "Autenticación fallida", "El correo o la contraseña no son válidos.");

    var rawToken = TokenIssuer.CreateOpaqueToken();
    var now = DateTime.UtcNow;
    database.RefreshTokens.Add(new RefreshToken
    {
        UserId = user.Id, FamilyId = Guid.NewGuid(), TokenHash = TokenIssuer.HashToken(rawToken),
        CreatedAtUtc = now, ExpiresAtUtc = now.Add(SessionLifetime)
    });
    await database.SaveChangesAsync(cancellationToken);
    context.Response.Cookies.Append("drovik.refresh", rawToken, CookieOptions(context, httpOnly: true, lifetime: null));
    return Results.Ok(await tokenIssuer.IssueAsync(user, cancellationToken));
}).RequireRateLimiting("login");

app.MapPost("/api/v1/auth/refresh", async (HttpContext context, IdentityDbContext database,
    UserManager<ApplicationUser> users, TokenIssuer tokenIssuer, CancellationToken cancellationToken) =>
{
    if (!IsAllowedOrigin(context, allowedOrigins) || !ValidCsrf(context))
        return Problem(StatusCodes.Status400BadRequest, "Solicitud rechazada", "La validación CSRF falló.");
    if (!context.Request.Cookies.TryGetValue("drovik.refresh", out var rawToken) || string.IsNullOrEmpty(rawToken))
        return Results.Unauthorized();
    var hash = TokenIssuer.HashToken(rawToken);
    var oldToken = await database.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
    if (oldToken is null) return Results.Unauthorized();
    if (oldToken.RevokedAtUtc is not null)
    {
        await database.RefreshTokens.Where(token => token.FamilyId == oldToken.FamilyId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAtUtc, DateTime.UtcNow), cancellationToken);
        return Results.Unauthorized();
    }
    if (oldToken.ExpiresAtUtc <= DateTime.UtcNow) return Results.Unauthorized();

    var replacement = TokenIssuer.CreateOpaqueToken();
    var now = DateTime.UtcNow;
    var next = new RefreshToken
    {
        UserId = oldToken.UserId, FamilyId = oldToken.FamilyId, TokenHash = TokenIssuer.HashToken(replacement),
        CreatedAtUtc = now, ExpiresAtUtc = now.Add(SessionLifetime)
    };
    oldToken.ReplacedByTokenId = next.Id;
    try
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        oldToken.RevokedAtUtc = now;
        oldToken.ConcurrencyToken = Guid.NewGuid();
        database.RefreshTokens.Add(next);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        await database.RefreshTokens.Where(token => token.FamilyId == oldToken.FamilyId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAtUtc, DateTime.UtcNow), cancellationToken);
        return Results.Unauthorized();
    }

    var user = await users.FindByIdAsync(oldToken.UserId.ToString());
    if (user is null) return Results.Unauthorized();
    context.Response.Cookies.Append("drovik.refresh", replacement, CookieOptions(context, httpOnly: true, lifetime: null));
    return Results.Ok(await tokenIssuer.IssueAsync(user, cancellationToken));
});

app.MapPost("/api/v1/auth/logout", async (HttpContext context, IdentityDbContext database, CancellationToken cancellationToken) =>
{
    if (!IsAllowedOrigin(context, allowedOrigins) || !ValidCsrf(context))
        return Problem(StatusCodes.Status400BadRequest, "Solicitud rechazada", "La validación CSRF falló.");
    if (context.Request.Cookies.TryGetValue("drovik.refresh", out var rawToken))
    {
        var hash = TokenIssuer.HashToken(rawToken);
        var token = await database.RefreshTokens.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (token is not null)
            await database.RefreshTokens.Where(item => item.FamilyId == token.FamilyId && item.RevokedAtUtc == null)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAtUtc, DateTime.UtcNow), cancellationToken);
    }
    ExpireSessionCookies(context);
    return Results.NoContent();
});

app.MapGet("/api/v1/auth/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
{
    var id = principal.FindFirstValue("sub");
    var user = id is null ? null : await users.FindByIdAsync(id);
    if (user is null) return Results.Unauthorized();
    var roles = await users.GetRolesAsync(user);
    return Results.Ok(new AuthUserDto(user.Id, user.Email ?? "", roles.ToArray()));
}).RequireAuthorization();

const string PasswordPolicy = "La contraseña debe tener al menos 12 caracteres, con mayúscula, minúscula, número y símbolo.";

app.MapPost("/api/v1/auth/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users,
    IdentityDbContext database, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        return Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "La contraseña actual y la nueva son obligatorias.");
    var id = principal.FindFirstValue("sub");
    var user = id is null ? null : await users.FindByIdAsync(id);
    if (user is null) return Results.Unauthorized();
    var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    if (!result.Succeeded)
        return Problem(StatusCodes.Status400BadRequest, "No se pudo cambiar la contraseña",
            result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)) ? "La contraseña actual no es correcta." : PasswordPolicy);
    await UserAccounts.RevokeSessionsAsync(database, user.Id, cancellationToken);
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("login");

app.MapPost("/api/v1/users", async (CreateUserRequest request, UserManager<ApplicationUser> users, IdentityDbContext database, CancellationToken cancellationToken) =>
{
    var role = request.Role?.Trim();
    if (string.IsNullOrWhiteSpace(request.Email) || !new EmailAddressAttribute().IsValid(request.Email) || string.IsNullOrWhiteSpace(request.Password)
        || role is not ("Administrator" or "Operator"))
        return Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "El correo, la contraseña y un rol permitido son obligatorios.");
    var user = new ApplicationUser { Id = Guid.NewGuid(), Email = request.Email.Trim(), UserName = request.Email.Trim(), EmailConfirmed = true };
    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    var result = await users.CreateAsync(user, request.Password);
    if (!result.Succeeded)
        return Problem(StatusCodes.Status409Conflict, "No se pudo crear el usuario", "El correo puede existir ya o la contraseña no cumple la política.");
    var roleResult = await users.AddToRoleAsync(user, role);
    if (!roleResult.Succeeded)
    {
        return Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "No se pudo asignar el rol.");
    }
    await transaction.CommitAsync(cancellationToken);
    return Results.Created((string?)null, new AuthUserDto(user.Id, user.Email!, [role]));
}).RequireAuthorization(policy => policy.RequireRole("Administrator"));

app.MapGet("/api/v1/users", async (UserManager<ApplicationUser> users, CancellationToken cancellationToken) =>
{
    var all = await users.Users.AsNoTracking().OrderBy(item => item.Email).Take(200).ToListAsync(cancellationToken);
    var response = new List<ManagedUserDto>();
    foreach (var user in all) response.Add(new ManagedUserDto(user.Id, user.Email!, [.. await users.GetRolesAsync(user)], UserAccounts.IsActive(user)));
    return Results.Ok(response);
}).RequireAuthorization(policy => policy.RequireRole("Administrator"));

app.MapPatch("/api/v1/users/{id:guid}/status", async (Guid id, ChangeUserStatusRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users,
    IdentityDbContext database, CancellationToken cancellationToken) =>
{
    if (principal.FindFirst("sub")?.Value == id.ToString()) return Problem(StatusCodes.Status409Conflict, "Conflicto", "No puedes cambiar el estado de tu propia cuenta.");
    var user = await users.FindByIdAsync(id.ToString());
    if (user is null) return Results.NotFound();
    if (!request.IsActive)
    {
        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        // A deactivated account must not be able to renew its session.
        await UserAccounts.RevokeSessionsAsync(database, id, cancellationToken);
    }
    else
    {
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
    }
    return Results.Ok(new ManagedUserDto(user.Id, user.Email!, [.. await users.GetRolesAsync(user)], UserAccounts.IsActive(user)));
}).RequireAuthorization(policy => policy.RequireRole("Administrator"));

// There is no email service, so a forgotten password is replaced by an administrator.
app.MapPost("/api/v1/users/{id:guid}/password", async (Guid id, ResetPasswordRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users,
    IdentityDbContext database, CancellationToken cancellationToken) =>
{
    if (principal.FindFirst("sub")?.Value == id.ToString()) return Problem(StatusCodes.Status409Conflict, "Conflicto", "Cambia tu propia contraseña con la opción de tu perfil.");
    if (string.IsNullOrWhiteSpace(request.NewPassword)) return Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", PasswordPolicy);
    var user = await users.FindByIdAsync(id.ToString());
    if (user is null) return Results.NotFound();
    var result = await UserAccounts.ReplacePasswordAsync(users, database, user, request.NewPassword, cancellationToken);
    return result.Succeeded ? Results.NoContent() : Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", PasswordPolicy);
}).RequireAuthorization(policy => policy.RequireRole("Administrator"));

// Refreshing a session reads the roles again, so the change reaches an open session within one access token's lifetime.
app.MapPatch("/api/v1/users/{id:guid}/role", async (Guid id, ChangeUserRoleRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users,
    IdentityDbContext database, CancellationToken cancellationToken) =>
{
    var role = request.Role?.Trim();
    if (role is not ("Administrator" or "Operator")) return Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "El rol no es válido.");
    // Refusing one's own change also means the system can never lose its last administrator this way.
    if (principal.FindFirst("sub")?.Value == id.ToString()) return Problem(StatusCodes.Status409Conflict, "Conflicto", "No puedes cambiar tu propio rol.");
    var user = await users.FindByIdAsync(id.ToString());
    if (user is null) return Results.NotFound();
    var current = await users.GetRolesAsync(user);
    if (current.Count != 1 || current[0] != role)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!(await users.RemoveFromRolesAsync(user, current)).Succeeded || !(await users.AddToRoleAsync(user, role)).Succeeded)
            return Problem(StatusCodes.Status409Conflict, "Conflicto", "No se pudo cambiar el rol.");
        await transaction.CommitAsync(cancellationToken);
    }
    return Results.Ok(new ManagedUserDto(user.Id, user.Email!, [role], UserAccounts.IsActive(user)));
}).RequireAuthorization(policy => policy.RequireRole("Administrator"));

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (IdentityDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "Healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.Run();

static void ConfigureJwtBearer(JwtBearerOptions options, IConfiguration configuration)
{
    var jwt = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    if (jwt.AccessTokenMinutes != 15 || string.IsNullOrWhiteSpace(jwt.PrivateKeyPkcs8Base64))
        throw new InvalidOperationException("Identity JWT lifetime or local signing key is invalid.");
    var encoded = configuration["Jwt:PublicKeySpkiBase64"];
    if (string.IsNullOrWhiteSpace(encoded))
        throw new InvalidOperationException("Jwt:PublicKeySpkiBase64 must be configured through local secrets.");
    var rsa = RSA.Create();
    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(encoded), out _);
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new RsaSecurityKey(rsa),
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
        NameClaimType = "sub", RoleClaimType = "role"
    };
}

static bool IsAllowedOrigin(HttpContext context, string[] origins) =>
    context.Request.Headers.Origin.Any(origin => origins.Contains(origin, StringComparer.Ordinal));

static bool ValidCsrf(HttpContext context)
{
    var header = context.Request.Headers["X-CSRF-TOKEN"].ToString();
    return context.Request.Cookies.TryGetValue("drovik.csrf", out var cookie)
        && !string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(cookie)
        && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(header), System.Text.Encoding.UTF8.GetBytes(cookie));
}

// Without a lifetime the cookie is a browser-session cookie: closing the browser ends the session.
static CookieOptions CookieOptions(HttpContext context, bool httpOnly, TimeSpan? lifetime) => new()
{
    HttpOnly = httpOnly,
    Secure = context.Request.IsHttps,
    SameSite = SameSiteMode.Lax,
    Path = "/api/v1/auth",
    MaxAge = lifetime,
    IsEssential = true
};

static void ExpireSessionCookies(HttpContext context)
{
    foreach (var name in new[] { "drovik.refresh", "drovik.csrf" })
        context.Response.Cookies.Delete(name, new CookieOptions { Path = "/api/v1/auth", SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps });
}

static IResult Problem(int status, string title, string detail) => Results.Problem(
    statusCode: status, title: title, detail: detail, type: "about:blank");

public partial class Program { }
