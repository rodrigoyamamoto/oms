using System.Text.Json;
using MediatR;
using Oms.Orders.Api.Contracts;
using Oms.Orders.Application.Errors;
using Oms.Orders.Application.Orders.CreateOrder;
using Oms.Orders.Application.Orders.GetOrderById;
using Oms.Orders.Domain.Common;
using Oms.Orders.Domain.Errors;

namespace Oms.Orders.Api.Endpoints;

internal static class OrdersEndpoints
{
    private const string GetOrderByIdEndpointName = "GetOrderById";
    private const string NotFoundErrorCode = "Orders.NotFound";

    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/orders");

        group.MapPost(string.Empty, CreateOrderAsync);
        group.MapGet("/{id}", GetOrderByIdAsync).WithName(GetOrderByIdEndpointName);

        return endpoints;
    }

    private static async Task<IResult> CreateOrderAsync(
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken cancellationToken)
    {
        CreateOrderRequest? request;
        try
        {
            if (httpRequest.ContentLength is 0)
            {
                return ToProblemResult(OrderErrors.EmptyItems);
            }

            if (!httpRequest.HasJsonContentType())
            {
                return ToProblemResult(OrderApplicationErrors.InvalidRequestPayload);
            }

            request = await httpRequest.ReadFromJsonAsync<CreateOrderRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return ToProblemResult(OrderApplicationErrors.InvalidRequestPayload);
        }

        if (request?.Items is null || request.Items.Count == 0)
        {
            return ToProblemResult(OrderErrors.EmptyItems);
        }

        var items = new List<CreateOrderItemInput>(request.Items.Count);
        foreach (var item in request.Items)
        {
            if (item is null)
            {
                return ToProblemResult(OrderErrors.InvalidProductId);
            }

            items.Add(new CreateOrderItemInput(
                item.ProductId,
                item.Quantity,
                item.UnitPriceAmount,
                item.Currency ?? string.Empty));
        }

        var result = await sender.Send(new CreateOrderCommand(items), cancellationToken);
        if (result.IsFailure)
        {
            return ToProblemResult(result.Error);
        }

        var orderId = result.Value;
        return TypedResults.CreatedAtRoute(
            new CreateOrderResponse(orderId),
            GetOrderByIdEndpointName,
            new { id = orderId });
    }

    private static async Task<IResult> GetOrderByIdAsync(
        string id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var orderId))
        {
            return ToProblemResult(OrderApplicationErrors.InvalidOrderId);
        }

        var result = await sender.Send(new GetOrderByIdQuery(orderId), cancellationToken);
        if (result.IsFailure)
        {
            return ToProblemResult(result.Error);
        }

        var details = result.Value;
        var itemResponses = new List<OrderItemResponse>(details.Items.Count);
        foreach (var item in details.Items)
        {
            itemResponses.Add(new OrderItemResponse(
                item.Id,
                item.ProductId,
                item.Quantity,
                item.UnitPriceAmount,
                item.Currency,
                item.LineTotalAmount));
        }

        var response = new OrderResponse(
            details.Id,
            details.Status.ToString(),
            details.TotalAmount,
            details.Currency,
            itemResponses.AsReadOnly());

        return TypedResults.Ok(response);
    }

    private static IResult ToProblemResult(Error error)
    {
        var isNotFound = string.Equals(error.Code, NotFoundErrorCode, StringComparison.Ordinal);

        return Results.Problem(
            statusCode: isNotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
            title: isNotFound ? "Not Found" : "Bad Request",
            type: "about:blank",
            detail: error.Message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
            });
    }
}
