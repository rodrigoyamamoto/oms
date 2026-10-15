using System.Globalization;

namespace Oms.Orders.Domain;

internal readonly record struct Money
{
    private const int IsoCurrencyCodeLength = 3;

    public Money(decimal amount, string currency)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Monetary amount cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalisedCurrency = currency.Trim().ToUpperInvariant();
        if (!IsValidThreeLetterCurrencyCode(normalisedCurrency))
        {
            throw new ArgumentException(
                "Currency must be a three-letter ASCII alphabetic code (for example, GBP, EUR, or USD).",
                nameof(currency));
        }

        Amount = amount;
        Currency = normalisedCurrency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureInitialised();
        other.EnsureInitialised();
        EnsureSameCurrency(other);

        return new Money(checked(Amount + other.Amount), Currency);
    }

    public Money Multiply(int factor)
    {
        EnsureInitialised();

        if (factor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Multiplication factor cannot be negative.");
        }

        return new Money(checked(Amount * factor), Currency);
    }

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator *(Money money, int factor) => money.Multiply(factor);

    public static Money operator *(int factor, Money money) => money.Multiply(factor);

    public override string ToString() =>
        string.IsNullOrEmpty(Currency)
            ? "0 (Uninitialised)"
            : string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    private static bool IsValidThreeLetterCurrencyCode(string value)
    {
        if (value.Length != IsoCurrencyCodeLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterUpper(character))
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureInitialised()
    {
        if (string.IsNullOrEmpty(Currency))
        {
            throw new InvalidOperationException("Cannot perform monetary operations on an uninitialised Money value.");
        }
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine monetary amounts with different currencies ('{Currency}' and '{other.Currency}').");
        }
    }
}
