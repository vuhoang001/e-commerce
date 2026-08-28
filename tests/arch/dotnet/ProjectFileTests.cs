using System.Xml.Linq;

namespace Ecommerce.ArchitectureTests;

/// Reads the .csproj files themselves.
///
/// Why, when LayerDependencyTests already checks assembly references: a compiled assembly
/// only lists what it actually uses. Adding a PackageReference and not using it yet leaves
/// no trace in the IL, so the assembly-level rule stays green until someone writes the
/// first offending line. These rules fail at the moment the dependency is declared, which
/// is where the reviewer is looking.
public class ProjectFileTests
{
    [Test]
    public async Task The_domain_project_declares_no_package_and_no_project_reference()
    {
        var domain = LoadProject("services/order-service/src/Domain/Ecommerce.OrderService.Domain.csproj");

        var references = domain.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference")
            .Select(element => $"{element.Name.LocalName} {element.Attribute("Include")?.Value}")
            .ToList();

        await Assert.That(references).IsEmpty();
    }

    [Test]
    public async Task The_application_project_declares_no_persistence_package()
    {
        var application = LoadProject(
            "services/order-service/src/Application/Ecommerce.OrderService.Application.csproj");

        var forbidden = application.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(name => name.Contains("EntityFrameworkCore", StringComparison.Ordinal)
                           || name.Contains("Npgsql", StringComparison.Ordinal)
                           || name.Contains("Grpc", StringComparison.Ordinal))
            .ToList();

        await Assert.That(forbidden).IsEmpty();
    }

    [Test]
    public async Task Every_package_version_is_declared_centrally()
    {
        // A Version attribute on a PackageReference bypasses Directory.Packages.props, which
        // is how two projects quietly end up on different releases of the same library.
        var offenders = RepositoryRoot()
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => XDocument.Load(file.FullName)
                .Descendants()
                .Where(element => element.Name.LocalName == "PackageReference")
                .Where(element => element.Attribute("Version") is not null)
                .Select(element => $"{file.Name}: {element.Attribute("Include")?.Value}"))
            .ToList();

        await Assert.That(offenders).IsEmpty();
    }

    private static XDocument LoadProject(string relativePath) =>
        XDocument.Load(Path.Combine(RepositoryRoot().FullName, relativePath));

    private static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !directory.EnumerateFiles("ecommerce-polyglot.slnx").Any())
        {
            directory = directory.Parent;
        }

        return directory
               ?? throw new InvalidOperationException("Could not find the repository root above the test assembly.");
    }
}
