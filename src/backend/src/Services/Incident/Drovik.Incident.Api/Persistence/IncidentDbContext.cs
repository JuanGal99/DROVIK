using Microsoft.EntityFrameworkCore;

namespace Drovik.Incident.Api.Persistence;

public sealed class IncidentDbContext(DbContextOptions<IncidentDbContext> options) : DbContext(options);
