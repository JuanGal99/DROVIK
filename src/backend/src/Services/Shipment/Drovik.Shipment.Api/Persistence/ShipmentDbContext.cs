using Microsoft.EntityFrameworkCore;

namespace Drovik.Shipment.Api.Persistence;

public sealed class ShipmentDbContext(DbContextOptions<ShipmentDbContext> options) : DbContext(options);
