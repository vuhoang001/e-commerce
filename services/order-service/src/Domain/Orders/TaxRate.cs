using Ecommerce.OrderService.Domain.Abstractions;

namespace Ecommerce.OrderService.Domain.Orders;

/// A tax rate held in basis points: 1000 is 10.00%.
///
/// An integer because a rate multiplies money, so a floating-point rate would put its own
/// rounding error into every total. Basis points give two decimal places of precision on
/// the percentage, which is more than any real tax authority uses.
public readonly record struct TaxRate
{
    private const int BasisPointsPerUnit = 10_000;

    private TaxRate(int basisPoints) => BasisPoints = basisPoints;

    public int BasisPoints { get; }

    public decimal Percent => BasisPoints / 100m;

    public static TaxRate Zero { get; } = new(0);

    public static TaxRate FromBasisPoints(int basisPoints)
    {
        if (basisPoints is < 0 or > BasisPointsPerUnit)
        {
            throw new DomainException(
                $"A tax rate must be between 0 and {BasisPointsPerUnit} basis points, but was {basisPoints}.");
        }

        return new TaxRate(basisPoints);
    }

    public Money ApplyTo(Money amount) =>
        Money.Of(amount.Amount * BasisPoints / BasisPointsPerUnit, amount.Currency).Round();

    public override string ToString() => $"{Percent}%";
}
