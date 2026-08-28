using Ecommerce.OrderService.Domain.Abstractions;

namespace Ecommerce.OrderService.Domain.Orders;

/// Where the order is going. Snapshotted onto the order for the same reason the items are:
/// a customer moving house must not silently rewrite where last year's parcel was sent.
public sealed record Address
{
    public Address(string line1, string? line2, string city, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(line1))
        {
            throw new DomainException("An address needs a first line.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new DomainException("An address needs a city.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new DomainException("An address needs a postal code.");
        }

        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
        {
            throw new DomainException($"'{country}' is not an ISO 3166-1 alpha-2 country code.");
        }

        Line1 = line1.Trim();
        Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim();
        City = city.Trim();
        PostalCode = postalCode.Trim();
        Country = country.ToUpperInvariant();
    }

    public string Line1 { get; }
    public string? Line2 { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }
}
