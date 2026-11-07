using Microsoft.EntityFrameworkCore;

namespace Drovik.Identity.Api.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options);
