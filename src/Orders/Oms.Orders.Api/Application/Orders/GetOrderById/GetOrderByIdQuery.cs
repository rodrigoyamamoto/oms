using MediatR;
using Oms.Orders.Domain.Common;

namespace Oms.Orders.Application.Orders.GetOrderById;

internal sealed record GetOrderByIdQuery(Guid OrderId)
    : IRequest<Result<OrderDetails>>;
