using MediatR;
using Oms.Orders.Application.Abstractions;
using Oms.Orders.Domain;
using Oms.Orders.Domain.Common;
using Oms.Orders.Domain.Errors;

namespace Oms.Orders.Application.Orders.CreateOrder;

internal sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly IOrderStore _orderStore;

    public CreateOrderCommandHandler(IOrderStore orderStore)
    {
        ArgumentNullException.ThrowIfNull(orderStore);
        _orderStore = orderStore;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items is null || request.Items.Count == 0)
        {
            return Result.Failure<Guid>(OrderErrors.EmptyItems);
        }

        var drafts = new List<OrderItemDraft>(request.Items.Count);

        foreach (var item in request.Items)
        {
            var moneyResult = Money.Create(item.UnitPriceAmount, item.Currency);
            if (moneyResult.IsFailure)
            {
                return Result.Failure<Guid>(moneyResult.Error);
            }

            drafts.Add(new OrderItemDraft(item.ProductId, item.Quantity, moneyResult.Value));
        }

        var orderResult = Order.Create(drafts);
        if (orderResult.IsFailure)
        {
            return Result.Failure<Guid>(orderResult.Error);
        }

        var order = orderResult.Value;
        await _orderStore.AddAsync(order, cancellationToken);

        return Result.Success(order.Id);
    }
}
