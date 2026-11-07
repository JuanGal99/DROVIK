namespace Drovik.Identity.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);
public sealed record CreateUserRequest(string Email, string Password, string Role);
public sealed record AuthUserDto(Guid Id, string Email, IReadOnlyList<string> Roles);
public sealed record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, AuthUserDto User);
public sealed record CsrfResponse(string Token);
public sealed record ApiProblem(string Type, string Title, int Status, string Detail);
