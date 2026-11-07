using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Drovik.Incident.Api.Persistence;

public sealed class IncidentDbContextFactory : IDesignTimeDbContextFactory<IncidentDbContext>
{
    public IncidentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IncidentDbContext>()
            .UseSqlServer("Server=localhost;Database=DrovikIncident;TrustServerCertificate=True")
            .Options;

        return new IncidentDbContext(options);
    }
}
