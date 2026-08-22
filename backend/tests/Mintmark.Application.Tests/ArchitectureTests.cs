using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using FluentAssertions;
using Mintmark.Application.Validators;
using Mintmark.Domain.ValueObjects;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Mintmark.Application.Tests;

/// <summary>
/// Layering rules, enforced with ArchUnitNET over the loaded type graph:
/// Domain stays dependency-free (BCL only); Application adds only
/// FluentValidation on top of Domain; neither references Infrastructure/Api.
/// </summary>
public class ArchitectureTests
{
    /// <summary>Domain + Application plus their referenced assemblies, loaded once.</summary>
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssembliesIncludingDependencies(
            typeof(Money).Assembly,
            typeof(CreateHoldingValidator).Assembly)
        .Build();

    private static IEnumerable<string> ViolationsOf(IArchRule rule) =>
        rule.Evaluate(Architecture)
            .Where(result => !result.Passed)
            .Select(result => result.Description);

    [Fact]
    public void Architecture_Loaded_ContainsBothLayers()
    {
        Architecture.Types.Should().Contain(t => t.FullName.StartsWith("Mintmark.Domain", StringComparison.Ordinal));
        Architecture.Types.Should().Contain(t => t.FullName.StartsWith("Mintmark.Application", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_HasZeroTypeDependencies_OutsideSystemOrMicrosoftOrItself()
    {
        IArchRule rule = Types()
            .That().ResideInNamespaceMatching("^Mintmark\\.Domain")
            .Should().OnlyDependOnTypesThat()
            .ResideInNamespaceMatching("^(System|Microsoft|Mintmark\\.Domain)");

        ViolationsOf(rule).Should().BeEmpty(
            "Mintmark.Domain must be a pure domain model: no dependencies outside System/Microsoft BCL and itself.");
    }

    [Fact]
    public void Domain_DoesNotDepend_OnApplicationInfrastructureOrApi()
    {
        IArchRule rule = Types()
            .That().ResideInNamespaceMatching("^Mintmark\\.Domain")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching("^Mintmark\\.(Application|Infrastructure|Api)");

        ViolationsOf(rule).Should().BeEmpty();
    }

    [Fact]
    public void Application_DependsOnlyOn_DomainAndFluentValidation_BeyondBcl()
    {
        IArchRule rule = Types()
            .That().ResideInNamespaceMatching("^Mintmark\\.Application")
            .Should().OnlyDependOnTypesThat()
            .ResideInNamespaceMatching("^(System|Microsoft|Mintmark\\.(Domain|Application)|FluentValidation)");

        ViolationsOf(rule).Should().BeEmpty(
            "Mintmark.Application may depend on Mintmark.Domain and FluentValidation only (plus the BCL).");
    }

    [Fact]
    public void Application_DoesNotDepend_OnInfrastructureOrApi()
    {
        IArchRule rule = Types()
            .That().ResideInNamespaceMatching("^Mintmark\\.Application")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching("^Mintmark\\.(Infrastructure|Api)");

        ViolationsOf(rule).Should().BeEmpty();
    }

    [Fact]
    public void Domain_Assembly_References_NoMintmarkProjects()
    {
        var references = typeof(Money).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => name.StartsWith("Mintmark", StringComparison.Ordinal))
            .ToList();

        references.Should().BeEmpty("Mintmark.Domain must have zero project references.");
    }

    [Fact]
    public void Application_Assembly_References_OnlyTheDomainProject()
    {
        var references = typeof(CreateHoldingValidator).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => name.StartsWith("Mintmark", StringComparison.Ordinal))
            .ToList();

        references.Should().BeEquivalentTo(["Mintmark.Domain"]);
        references.Should().NotContain("Mintmark.Infrastructure", "Mintmark.Api");
    }
}
