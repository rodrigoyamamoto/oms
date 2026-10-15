using Oms.Orders.Api;
using Xunit;

namespace Oms.Orders.UnitTests.Architecture;

public sealed class OrdersDomainArchitectureTests
{
    private const string DomainNamespacePrefix = "Oms.Orders.Domain";

    [Fact]
    public void DomainLayerTypesShouldRemainInternal()
    {
        var domainTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.Namespace is not null &&
                           (string.Equals(type.Namespace, DomainNamespacePrefix, StringComparison.Ordinal) ||
                            type.Namespace.StartsWith(DomainNamespacePrefix + ".", StringComparison.Ordinal)))
            .ToArray();

        Assert.NotEmpty(domainTypes);
        Assert.All(
            domainTypes,
            type => Assert.False(
                type.IsPublic || type.IsNestedPublic,
                $"Domain type '{type.FullName}' must be internal, not public."));
    }
}
