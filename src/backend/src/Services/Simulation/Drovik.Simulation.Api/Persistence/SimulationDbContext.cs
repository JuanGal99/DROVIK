using Microsoft.EntityFrameworkCore;

namespace Drovik.Simulation.Api.Persistence;

public sealed class SimulationDbContext(DbContextOptions<SimulationDbContext> options) : DbContext(options);
