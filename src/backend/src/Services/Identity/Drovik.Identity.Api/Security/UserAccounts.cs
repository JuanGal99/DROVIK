using Drovik.Identity.Api.Domain;
using Drovik.Identity.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Identity.Api.Security;

public static class UserAccounts
{
    // Failed sign-ins lock an account for minutes; only an administrator's deactivation lasts for years.
    public static bool IsActive(ApplicationUser user) => user.LockoutEnd is null || user.LockoutEnd < DateTimeOffset.UtcNow.AddYears(1);

    public static Task RevokeSessionsAsync(IdentityDbContext database, Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return database.RefreshTokens.Where(item => item.UserId == userId && item.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAtUtc, now), cancellationToken);
    }

    /// <summary>Sets a password without knowing the old one. If the new one is rejected the old one stays, and on success every session ends.</summary>
    public static async Task<IdentityResult> ReplacePasswordAsync(
        UserManager<ApplicationUser> users, IdentityDbContext database, ApplicationUser user, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var removed = await users.RemovePasswordAsync(user);
        if (!removed.Succeeded) return removed;
        var added = await users.AddPasswordAsync(user, password);
        if (!added.Succeeded) return added;
        await users.ResetAccessFailedCountAsync(user);
        // A forgotten password usually comes with failed attempts, but a deactivated account stays deactivated.
        if (IsActive(user)) await users.SetLockoutEndDateAsync(user, null);
        await RevokeSessionsAsync(database, user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return added;
    }
}
