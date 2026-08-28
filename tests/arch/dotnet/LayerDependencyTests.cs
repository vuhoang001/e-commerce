using NetArchTest.Rules;

namespace Ecommerce.ArchitectureTests;

/// The dependency rules from .agents/skills/ddd-dotnet:
///
///     Api  →  Application  →  Domain
///                  ↓
///            Infrastructure  →  Domain
///
/// Every arrow that is absent above is a test below.
public class LayerDependencyTests
{
    [Test]
    public async Task Domain_references_nothing_but_the_framework()
    {
        // The strongest rule in the repository, and the reason it is checked at assembly
        // level rather than by name: no package can sneak in, not just the ones anyone
        // thought to forbid. A domain test must never need a container or a database.
        var offenders = Layers.ReferencesOf(Layers.Domain)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                           && name != "netstandard"
                           && !name.StartsWith("Microsoft.CSharp", StringComparison.Ordinal))
            .ToList();

        await Assert.That(offenders).IsEmpty();
    }

    [Test]
    public async Task Domain_does_not_know_about_Application()
    {
        await Assert.That(Layers.ReferencesOf(Layers.Domain)).DoesNotContain("Ecommerce.OrderService.Application");
    }

    [Test]
    public async Task Domain_does_not_know_about_Infrastructure()
    {
        await Assert.That(Layers.ReferencesOf(Layers.Domain)).DoesNotContain("Ecommerce.OrderService.Infrastructure");
    }

    [Test]
    public async Task Domain_does_not_know_about_the_wire_contract()
    {
        // An aggregate that referenced the generated protobuf types would make the contract
        // impossible to version independently of the model.
        await Assert.That(Layers.ReferencesOf(Layers.Domain)).DoesNotContain("Ecommerce.Contracts");
    }

    [Test]
    public async Task Application_does_not_know_about_Infrastructure()
    {
        // Handlers orchestrate; they do not know about SQL or Kafka. Infrastructure is
        // reached only through interfaces the Domain declares.
        await Assert.That(Layers.ReferencesOf(Layers.Application))
            .DoesNotContain("Ecommerce.OrderService.Infrastructure");
    }

    [Test]
    public async Task Application_does_not_reference_a_database_library()
    {
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        await Assert.That(result.FailingTypeNames ?? []).IsEmpty();
    }

    [Test]
    public async Task Application_does_not_reference_the_transport()
    {
        // A handler that knows what a gRPC status is has taken on a job belonging to Api.
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot()
            .HaveDependencyOnAny("Grpc", "Google.Protobuf")
            .GetResult();

        await Assert.That(result.FailingTypeNames ?? []).IsEmpty();
    }

    [Test]
    public async Task Infrastructure_does_not_know_about_Application()
    {
        // Infrastructure implements what Domain declares. If it needed Application, the
        // dependency would be pointing outwards and the layers would be a cycle.
        await Assert.That(Layers.ReferencesOf(Layers.Infrastructure))
            .DoesNotContain("Ecommerce.OrderService.Application");
    }

    [Test]
    public async Task No_layer_pair_depends_on_each_other_in_both_directions()
    {
        // The single most valuable rule: a cycle is how a set of services quietly becomes a
        // distributed monolith, and it is invisible in any one file.
        var layers = new[]
        {
            ("Domain", Layers.Domain),
            ("Application", Layers.Application),
            ("Infrastructure", Layers.Infrastructure),
            ("Api", Layers.Api),
        };

        var cycles = (from first in layers
                      from second in layers
                      where first.Item1 != second.Item1
                      where Layers.ReferencesOf(first.Item2).Contains(second.Item2.GetName().Name)
                            && Layers.ReferencesOf(second.Item2).Contains(first.Item2.GetName().Name)
                      select $"{first.Item1} <-> {second.Item1}").ToList();

        await Assert.That(cycles).IsEmpty();
    }
}
