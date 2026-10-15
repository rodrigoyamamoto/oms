using Microsoft.EntityFrameworkCore;
using Oms.Orders.Application.Abstractions;
using Oms.Orders.Infrastructure.Persistence;

namespace Oms.Orders.Api;

/// <summary>
/// Application composition root and entry point for the Orders HTTP API service.
/// </summary>
public sealed class Program
{
    private Program()
    {
    }

    /// <summary>
    /// Configures and runs the Orders ASP.NET Core web application host.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the host process.</param>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("OrdersDatabase")));

        builder.Services.AddScoped<IOrderStore, EfOrderStore>();

        builder.Services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<Program>());

        builder.Services.AddHealthChecks();

        var app = builder.Build();

        app.MapHealthChecks("/health/live");

        app.Run();
    }
}
