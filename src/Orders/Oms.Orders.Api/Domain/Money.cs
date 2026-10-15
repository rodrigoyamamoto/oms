using System.Globalization;
using Oms.Orders.Domain.Common;
using Oms.Orders.Domain.Errors;

namespace Oms.Orders.Domain;

internal readonly record struct Money
{
    private const int IsoCurrencyCodeLength = 3;

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public bool IsInitialised => !string.IsNullOrEmpty(Currency);

    public static Result<Money> Create(decimal amount, string? currency)
    {
        if (amount < 0m)
        {
            return Result.Failure<Money>(MoneyErrors.NegativeAmount);
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure<Money>(MoneyErrors.InvalidCurrency);
        }

        var normalisedCurrency = currency.Trim().ToUpperInvariant();
        if (!IsValidThreeLetterCurrencyCode(normalisedCurrency))
        {
            return Result.Failure<Money>(MoneyErrors.InvalidCurrency);
        }

        return Result.Success(new Money(amount, normalisedCurrency));
    }

    public static Money Zero(string currency)
    {
        var result = Create(0m, currency);
        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Message, nameof(currency));
    }

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
        !IsInitialised
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
        if (!IsInitialised)
        {
            throw new InvalidOperationException(MoneyErrors.Uninitialised.Message);
        }
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(MoneyErrors.CurrencyMismatch(Currency, other.Currency).Message);
        }
    }
}
