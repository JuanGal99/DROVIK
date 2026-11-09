using Drovik.Shipment.Api.Contracts;
using Drovik.Shipment.Api.Domain;
using Drovik.Shipment.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Drovik.Shipment.Api.Application;

/// <summary>
/// Read-only on purpose: the delivery points are a closed catalog (ADR 0012) and change through a seed migration.
/// </summary>
public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var customers = app.MapGroup("/api/v1/customers")
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Administrator,Operator" });
        customers.MapGet("", ListAsync);
        customers.MapGet("{id:guid}", GetAsync);
        app.MapPost("/api/v1/shipments/demo-reset", ResetOperationAsync).RequireAuthorization(new AuthorizeAttribute { Roles = "Administrator" });
    }

    private static async Task<IResult> ResetOperationAsync(ShipmentDbContext database, ClaimsPrincipal principal, ILogger<ShipmentDbContext> logger, CancellationToken cancellationToken)
    {
        await database.StatusHistory.ExecuteDeleteAsync(cancellationToken);
        await database.Shipments.ExecuteDeleteAsync(cancellationToken);
        await database.Outbox.Where(item => item.PublishedAtUtc == null).ExecuteDeleteAsync(cancellationToken);
        logger.LogWarning("Envíos reiniciados por {Actor}.", principal.FindFirst("sub")?.Value);
        return Results.NoContent();
    }

    private static async Task<IResult> ListAsync(ShipmentDbContext database, string? search, bool? isActive,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || item.AddressLabel.Contains(term));
        }
        if (isActive.HasValue) query = query.Where(item => item.IsActive == isActive.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<CustomerResponse>(items.Select(ToResponse).ToList(), totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, ShipmentDbContext database, CancellationToken cancellationToken)
    {
        var customer = await database.Customers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return customer is null ? Results.NotFound() : Results.Ok(ToResponse(customer));
    }

    private static CustomerResponse ToResponse(Customer item) =>
        new(item.Id, item.Name, item.AddressLabel, item.Latitude, item.Longitude, item.IsActive);
}
