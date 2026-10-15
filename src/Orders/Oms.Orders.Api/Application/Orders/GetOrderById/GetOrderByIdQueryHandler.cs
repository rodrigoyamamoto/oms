using MediatR;
using Oms.Orders.Application.Abstractions;
using Oms.Orders.Application.Errors;
using Oms.Orders.Domain.Common;

namespace Oms.Orders.Application.Orders.GetOrderById;

internal sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDetails>>
{
    private readonly IOrderStore _orderStore;

    public GetOrderByIdQueryHandler(IOrderStore orderStore)
    {
        ArgumentNullException.ThrowIfNull(orderStore);
        _orderStore = orderStore;
    }

    public async Task<Result<OrderDetails>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OrderId == Guid.Empty)
        {
            return Result.Failure<OrderDetails>(OrderApplicationErrors.InvalidOrderId);
        }

        var order = await _orderStore.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderDetails>(OrderApplicationErrors.NotFound(request.OrderId));
        }

        var itemDetails = new List<OrderItemDetails>(order.Items.Count);
        foreach (var item in order.Items)
        {
            itemDetails.Add(new OrderItemDetails(
                item.Id,
                item.ProductId,
                item.Quantity,
                item.UnitPrice.Amount,
                item.UnitPrice.Currency,
                item.LineTotal.Amount));
        }

        var total = order.Total;
        var details = new OrderDetails(
            order.Id,
            order.Status,
            total.Amount,
            total.Currency,
            itemDetails.AsReadOnly());

        return Result.Success(details);
    }
}
