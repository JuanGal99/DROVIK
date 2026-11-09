using Drovik.Identity.Api.Domain;
using Drovik.Identity.Api.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Drovik.Identity.Api.Security;

/// <summary>Last resort when no administrator can sign in. Requires access to the machine running the service.</summary>
public static class RecoverPassword
{
    public static async Task<int> RunAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["DROVIK_RECOVER_EMAIL"];
        var password = configuration["DROVIK_RECOVER_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Recovery requires DROVIK_RECOVER_EMAIL and DROVIK_RECOVER_PASSWORD.");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            Console.Error.WriteLine("Recovery refused: the account does not exist.");
            return 3;
        }

        var result = await UserAccounts.ReplacePasswordAsync(users, database, user, password, CancellationToken.None);
        if (!result.Succeeded)
        {
            Console.Error.WriteLine("Recovery refused: the password does not meet the policy.");
            return 4;
        }
        await users.SetLockoutEndDateAsync(user, null);
        Console.WriteLine("Password replaced. Sessions for this account were closed.");
        return 0;
    }
}
