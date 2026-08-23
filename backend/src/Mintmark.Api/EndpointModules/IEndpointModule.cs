namespace Mintmark.Api.EndpointModules;

/// <summary>
/// Endpoint modules: each maps one resource under /api/v1. The composition
/// root registers the module list; modules own their routes, metadata,
/// authorization and rate-limit policies.
/// </summary>
public interface IEndpointModule
{
    /// <summary>Maps the module's endpoints onto the route builder.</summary>
    void Map(IEndpointRouteBuilder app);
}

/// <summary>Extension that maps every registered module.</summary>
public static class EndpointModuleRegistration
{
    /// <summary>Maps all modules in one call (called once from Program).</summary>
    public static IEndpointRouteBuilder MapMintmarkModules(this IEndpointRouteBuilder app)
    {
        foreach (var module in ModuleList)
        {
            module.Map(app);
        }

        return app;
    }

    private static IEnumerable<IEndpointModule> ModuleList =>
    [
        new AuthModule(),
        new HoldingsModule(),
        new CatalogModule(),
        new ImagesModule(),
        new IdentificationModule(),
        new PricesModule(),
        new ValuationsModule(),
        new PortfolioModule(),
    ];
}
