using Oms.Orders.Domain.Common;

namespace Oms.Orders.Domain.Errors;

internal static class MoneyErrors
{
    internal static readonly Error NegativeAmount = new(
        "Money.NegativeAmount",
        "Monetary amount cannot be negative.");

    internal static readonly Error InvalidCurrency = new(
        "Money.InvalidCurrency",
        "Currency must be a non-empty three-letter ASCII alphabetic code (for example, GBP, EUR, or USD).");

    internal static readonly Error Uninitialised = new(
        "Money.Uninitialised",
        "Monetary value must be initialised with a valid currency.");

    internal static Error CurrencyMismatch(string expectedCurrency, string actualCurrency) => new(
        "Money.CurrencyMismatch",
        $"Cannot combine monetary amounts with different currencies ('{expectedCurrency}' and '{actualCurrency}').");
}
