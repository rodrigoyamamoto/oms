using Oms.Orders.Api;
using Xunit;

namespace Oms.Orders.UnitTests.Architecture;

public sealed class OrdersDomainArchitectureTests
{
    private const string DomainNamespacePrefix = "Oms.Orders.Domain";
    private const string ApplicationNamespacePrefix = "Oms.Orders.Application";

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

    [Fact]
    public void ApplicationLayerTypesShouldRemainInternal()
    {
        var applicationTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.Namespace is not null &&
                           (string.Equals(type.Namespace, ApplicationNamespacePrefix, StringComparison.Ordinal) ||
                            type.Namespace.StartsWith(ApplicationNamespacePrefix + ".", StringComparison.Ordinal)))
            .ToArray();

        Assert.NotEmpty(applicationTypes);
        Assert.All(
            applicationTypes,
            type => Assert.False(
                type.IsPublic || type.IsNestedPublic,
                $"Application type '{type.FullName}' must be internal, not public."));
    }
}
