using Microsoft.AspNetCore.Identity;

namespace Drovik.Identity.Api.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
