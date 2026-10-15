using MediatR;
using Oms.Orders.Domain.Common;

namespace Oms.Orders.Application.Orders.CreateOrder;

internal readonly record struct CreateOrderItemInput(
    Guid ProductId,
    int Quantity,
    decimal UnitPriceAmount,
    string Currency);

internal sealed record CreateOrderCommand(IReadOnlyList<CreateOrderItemInput> Items)
    : IRequest<Result<Guid>>;
