using Milvasoft.DataAccess.EfCore.Bulk;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Interface for StoreasyDbContextAccessor.
/// </summary>
public interface IStoreasyDbContextAccessor
{
    /// <summary>
    /// Get db context.
    /// </summary>
    /// <returns></returns>
    IMilvaBulkDbContextBase GetDbContext();
}