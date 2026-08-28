using Ecommerce.OrderService.Domain.Abstractions;
using Ecommerce.OrderService.Domain.Orders;

namespace Ecommerce.OrderService.Domain.UnitTests;

public class MoneyTests
{
    [Test]
    public async Task Two_currencies_cannot_be_added_together()
    {
        var add = () => Money.Of(1m, "USD") + Money.Of(1m, "EUR");

        await Assert.That(add).Throws<DomainException>();
    }

    [Test]
    public async Task Money_cannot_be_negative()
    {
        var negative = () => Money.Of(-1m, "USD");

        await Assert.That(negative).Throws<DomainException>();
    }

    [Test]
    public async Task A_currency_code_has_three_letters()
    {
        var notACurrency = () => Money.Of(1m, "DOLLARS");

        await Assert.That(notACurrency).Throws<DomainException>();
    }

    [Test]
    public async Task Currency_codes_are_normalised_to_uppercase()
    {
        await Assert.That(Money.Of(1m, "usd").Currency).IsEqualTo("USD");
    }

    [Test]
    public async Task Rounding_goes_away_from_zero()
    {
        // 0.125 rounds to 0.13, not to 0.12 as banker's rounding would give.
        await Assert.That(Money.Of(0.125m, "USD").Round().Amount).IsEqualTo(0.13m);
    }

    [Test]
    public async Task A_tax_rate_cannot_exceed_one_hundred_percent()
    {
        var absurd = () => TaxRate.FromBasisPoints(10_001);

        await Assert.That(absurd).Throws<DomainException>();
    }
}
