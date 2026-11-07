using System.Data;
using Drovik.Identity.Api.Domain;
using Drovik.Identity.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Identity.Api.Security;

public static class BootstrapAdmin
{
    public static async Task<int> RunAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["DROVIK_BOOTSTRAP_ADMIN_EMAIL"];
        var password = configuration["DROVIK_BOOTSTRAP_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Bootstrap requires DROVIK_BOOTSTRAP_ADMIN_EMAIL and DROVIK_BOOTSTRAP_ADMIN_PASSWORD.");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var administrators = await (from userRole in db.UserRoles
                                    join role in db.Roles on userRole.RoleId equals role.Id
                                    where role.Name == "Administrator"
                                    select userRole.UserId).AnyAsync();
        if (administrators)
        {
            Console.Error.WriteLine("Bootstrap refused: an Administrator already exists.");
            return 3;
        }

        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email.Trim(), UserName = email.Trim(), EmailConfirmed = true };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            Console.Error.WriteLine("Bootstrap refused: the supplied account did not meet identity validation rules.");
            return 4;
        }
        var roleResult = await users.AddToRoleAsync(user, "Administrator");
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            Console.Error.WriteLine("Bootstrap failed while assigning the Administrator role.");
            return 5;
        }
        await transaction.CommitAsync();
        Console.WriteLine("Initial Administrator created. Remove the bootstrap environment variables.");
        return 0;
    }
}
