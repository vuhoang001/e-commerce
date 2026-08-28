using Ecommerce.OrderService.Domain.Abstractions;

namespace Ecommerce.OrderService.Domain.Orders;

/// An amount in one currency. There is no way to construct money without saying which
/// currency it is in, which is the whole point: a bare decimal price is a bug waiting for
/// a second currency to arrive.
public readonly record struct Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    /// ISO 4217, uppercase.
    public string Currency { get; }

    public static Money Of(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new DomainException($"'{currency}' is not an ISO 4217 currency code.");
        }

        if (amount < 0)
        {
            throw new DomainException($"Money cannot be negative, but was {amount}.");
        }

        return new Money(amount, currency.ToUpperInvariant());
    }

    public static Money Zero(string currency) => Of(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int quantity)
    {
        if (quantity < 0)
        {
            throw new DomainException($"Cannot multiply money by a negative quantity ({quantity}).");
        }

        return new Money(money.Amount * quantity, money.Currency);
    }

    /// Rounds to two decimal places, away from zero — the ordinary commercial convention.
    ///
    /// Two places is correct for USD, EUR and GBP and wrong for JPY, which has none, and
    /// for KWD, which has three. This service is single-currency until it is not; when a
    /// second currency arrives, this needs a currency-exponent table. See ADR-005.
    public Money Round() => new(Math.Round(Amount, 2, MidpointRounding.AwayFromZero), Currency);

    public override string ToString() => $"{Amount} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new DomainException(
                $"Cannot combine {left.Currency} with {right.Currency}. Convert first, deliberately.");
        }
    }
}
