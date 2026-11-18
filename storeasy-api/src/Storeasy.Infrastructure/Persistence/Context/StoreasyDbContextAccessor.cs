using Milvasoft.DataAccess.EfCore.Bulk;
using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.Persistence.Context;

/// <summary>
/// Storeasy scoped factory.
/// </summary>
/// <remarks>
/// Initializes new instance of <see cref="StoreasyDbContextScopedFactory"/>.
/// </remarks>
/// <param name="context"></param>
public class StoreasyDbContextAccessor(StoreasyDbContext context) : IStoreasyDbContextAccessor
{
    private readonly StoreasyDbContext _context = context;

    /// <summary>
    /// Db context creation implementation.
    /// </summary>
    /// <returns></returns>
    public IMilvaBulkDbContextBase GetDbContext() => _context;
}
