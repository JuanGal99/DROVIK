using Microsoft.EntityFrameworkCore;

namespace Drovik.Fleet.Api.Persistence;

public sealed class FleetDbContext(DbContextOptions<FleetDbContext> options) : DbContext(options);
