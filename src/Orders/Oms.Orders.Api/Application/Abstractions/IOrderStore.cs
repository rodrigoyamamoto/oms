using Oms.Orders.Domain;

namespace Oms.Orders.Application.Abstractions;

internal interface IOrderStore
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}
