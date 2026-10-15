using Microsoft.EntityFrameworkCore;
using Oms.Orders.Application.Abstractions;
using Oms.Orders.Domain;

namespace Oms.Orders.Infrastructure.Persistence;

internal sealed class EfOrderStore : IOrderStore
{
    private readonly OrdersDbContext _dbContext;

    public EfOrderStore(OrdersDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);
}
