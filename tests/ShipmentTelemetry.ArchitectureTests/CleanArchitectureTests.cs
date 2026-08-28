using FluentAssertions;
using NetArchTest.Rules;

namespace ShipmentTelemetry.ArchitectureTests;

public sealed class CleanArchitectureTests
{
    private const string DomainNamespace = "ShipmentTelemetry.Domain";
    private const string ApplicationNamespace = "ShipmentTelemetry.Application";
    private const string InfrastructureNamespace = "ShipmentTelemetry.Infrastructure";
    private const string ApiNamespace = "ShipmentTelemetry.Api";

    [Fact]
    public void Domain_ShouldNotReferenceInfrastructureOrApplication()
    {
        var result = Types.InAssembly(typeof(Domain.Aggregates.ShipmentOperationalState).Assembly)
            .Should()
            .NotHaveDependencyOn(InfrastructureNamespace)
            .And()
            .NotHaveDependencyOn(ApplicationNamespace)
            .And()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void Application_ShouldNotReferenceInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOn(InfrastructureNamespace)
            .And()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void Infrastructure_ShouldNotReferenceApi()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void Handlers_ShouldResideInApplicationLayer()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .That()
            .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
            .Should()
            .ResideInNamespace(ApplicationNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypes ?? []));
    }
}
