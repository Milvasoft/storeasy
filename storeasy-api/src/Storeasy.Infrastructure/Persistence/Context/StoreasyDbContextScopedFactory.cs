using Microsoft.EntityFrameworkCore;
using Milvasoft.DataAccess.EfCore.Configuration;

namespace Storeasy.Infrastructure.Persistence.Context;

/// <summary>
/// Storeasy scoped factory.
/// </summary>
/// <remarks>
/// Initializes new instance of <see cref="StoreasyDbContextScopedFactory"/>.
/// </remarks>
/// <param name="pooledFactory"></param>
/// <param name="dataAccessConfiguration"></param>
/// <param name="serviceProvider"></param>
public class StoreasyDbContextScopedFactory(IDbContextFactory<StoreasyDbContext> pooledFactory,
                                           IDataAccessConfiguration dataAccessConfiguration,
                                           IServiceProvider serviceProvider) : IDbContextFactory<StoreasyDbContext>
{
    private readonly IDbContextFactory<StoreasyDbContext> _pooledFactory = pooledFactory;
    private readonly IDataAccessConfiguration _dataAccessConfiguration = dataAccessConfiguration;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <summary>
    /// Db context creation implementation.
    /// </summary>
    /// <returns></returns>
    public StoreasyDbContext CreateDbContext()
    {
        var context = _pooledFactory.CreateDbContext();

        context.ServiceProvider = _serviceProvider;
        context.SetDataAccessConfiguration(_dataAccessConfiguration);

        return context;
    }
}
