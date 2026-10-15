using Microsoft.EntityFrameworkCore;
using Oms.Orders.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Oms.Orders.IntegrationTests.Persistence;

public sealed class OrdersPostgreSqlFixture : IAsyncLifetime
{
    private const string PostgreSqlImage = "postgres:17.2-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(PostgreSqlImage)
        .WithDatabase("orders_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    internal OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new OrdersDbContext(options);
    }
}
