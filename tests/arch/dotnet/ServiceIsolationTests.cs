namespace Ecommerce.ArchitectureTests;

/// CLAUDE.md rule 2: never let one service reference another service's packages or
/// database. Services talk over gRPC or Kafka, never by reaching in.
public class ServiceIsolationTests
{
    [Test]
    public async Task The_gateway_does_not_reference_any_order_service_project()
    {
        // It calls order-service over gRPC, using stubs generated from proto/. A project
        // reference would let it use the aggregate directly and the boundary would be
        // decoration rather than a boundary.
        var reachingIn = Layers.ReferencesOf(Layers.Gateway)
            .Where(name => name.StartsWith("Ecommerce.OrderService", StringComparison.Ordinal))
            .ToList();

        await Assert.That(reachingIn).IsEmpty();
    }

    [Test]
    public async Task Order_service_does_not_reference_the_gateway()
    {
        var everyOrderServiceLayer = new[] { Layers.Domain, Layers.Application, Layers.Infrastructure, Layers.Api };

        var reachingIn = everyOrderServiceLayer
            .SelectMany(Layers.ReferencesOf)
            .Where(name => name.StartsWith("Ecommerce.ApiGateway", StringComparison.Ordinal))
            .ToList();

        await Assert.That(reachingIn).IsEmpty();
    }

    [Test]
    public async Task Only_the_edges_of_a_service_touch_the_generated_contract()
    {
        // Api and the gateway translate to and from proto. Domain, Application and
        // Infrastructure must not, or the contract becomes impossible to version.
        var inner = new[]
        {
            ("Domain", Layers.Domain),
            ("Application", Layers.Application),
            ("Infrastructure", Layers.Infrastructure),
        };

        var offenders = inner
            .Where(layer => Layers.ReferencesOf(layer.Item2).Contains("Ecommerce.Contracts"))
            .Select(layer => layer.Item1)
            .ToList();

        await Assert.That(offenders).IsEmpty();
    }
}
