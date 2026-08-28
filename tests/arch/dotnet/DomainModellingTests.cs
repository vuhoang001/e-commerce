using System.Reflection;
using Ecommerce.OrderService.Domain.Abstractions;
using NetArchTest.Rules;

namespace Ecommerce.ArchitectureTests;

/// The modelling rules from .agents/skills/ddd-dotnet. The skill and these tests must never
/// disagree; if they do, one of the two is out of date and this is the one that fails.
public class DomainModellingTests
{
    [Test]
    public async Task Every_domain_event_is_immutable_after_construction()
    {
        // A mutable event is a fact somebody can change after it happened.
        //
        // `init` accessors do not count. A positional record generates one for every
        // parameter, and they can only run while the object is being constructed — `with`
        // produces a copy rather than editing the original. Only a genuine setter is a way
        // to rewrite history.
        var mutable = Types.InAssembly(Layers.Domain)
            .That().ImplementInterface(typeof(IDomainEvent))
            .GetTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(IsSettableAfterConstruction)
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToList();

        await Assert.That(mutable).IsEmpty();
    }

    [Test]
    public async Task Every_domain_event_is_sealed()
    {
        var unsealed = Types.InAssembly(Layers.Domain)
            .That().ImplementInterface(typeof(IDomainEvent))
            .GetTypes()
            .Where(type => !type.IsSealed)
            .Select(type => type.Name)
            .ToList();

        await Assert.That(unsealed).IsEmpty();
    }

    [Test]
    public async Task An_aggregate_root_is_the_only_thing_that_raises_events()
    {
        var raisers = Types.InAssembly(Layers.Domain)
            .GetTypes()
            // AggregateRoot<T> declares Raise; it is the base class, not an aggregate.
            .Where(type => !type.IsGenericTypeDefinition)
            .Where(type => type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(method => method.Name == "Raise"))
            .ToList();

        var notAggregates = raisers
            .Where(type => !IsAggregateRoot(type))
            .Select(type => type.Name)
            .ToList();

        await Assert.That(notAggregates).IsEmpty();
    }

    [Test]
    public async Task An_order_item_cannot_be_changed_after_it_is_created()
    {
        // The same guard as the domain unit tests, kept here as well because this is the
        // rule PLAN.md section 18 says will be attacked in month 4 by someone tidying up.
        var settable = typeof(OrderService.Domain.Orders.OrderItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null)
            .Select(property => property.Name)
            .ToList();

        await Assert.That(settable).IsEmpty();
    }

    [Test]
    public async Task No_aggregate_exposes_a_public_constructor()
    {
        // Creation goes through a named factory that states intent — Order.Place(...) —
        // so an aggregate cannot be brought into existence in an invalid state.
        var withPublicConstructors = Types.InAssembly(Layers.Domain)
            .GetTypes()
            .Where(IsAggregateRoot)
            .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length != 0)
            .Select(type => type.Name)
            .ToList();

        await Assert.That(withPublicConstructors).IsEmpty();
    }

    [Test]
    public async Task Command_and_query_handlers_live_in_the_application_layer()
    {
        var strays = new[] { Layers.Domain, Layers.Infrastructure, Layers.Api }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("CommandHandler", StringComparison.Ordinal)
                           || type.Name.EndsWith("QueryHandler", StringComparison.Ordinal))
            .Select(type => $"{type.Assembly.GetName().Name}: {type.Name}")
            .ToList();

        await Assert.That(strays).IsEmpty();
    }

    /// True only for a property that can be written after the object exists. An `init`
    /// accessor is marked with the IsExternalInit modifier, which is how the compiler and
    /// the runtime tell the two apart.
    private static bool IsSettableAfterConstruction(PropertyInfo property) =>
        property.SetMethod is { IsPublic: true } setter
        && !setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

    private static bool IsAggregateRoot(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
            {
                return true;
            }
        }

        return false;
    }
}
