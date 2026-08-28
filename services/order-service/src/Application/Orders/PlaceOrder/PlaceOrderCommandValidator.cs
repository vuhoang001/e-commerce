using FluentValidation;

namespace Ecommerce.OrderService.Application.Orders.PlaceOrder;

/// Shape only. Whether a quantity is positive or a currency code is three letters is a
/// domain invariant and stays in the aggregate — this stops a request that is malformed
/// before the domain would even be asked.
public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.ShippingAddress).NotNull();
        RuleFor(command => command.Lines).NotEmpty();

        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.ProductName).NotEmpty();
            line.RuleFor(l => l.Sku).NotEmpty();
            line.RuleFor(l => l.Currency).NotEmpty();
        });
    }
}
