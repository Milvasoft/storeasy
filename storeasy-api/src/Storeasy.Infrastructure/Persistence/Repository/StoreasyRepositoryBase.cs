using Milvasoft.Core.EntityBases.Concrete;
using Milvasoft.Helpers.DataAccess.EfCore.Concrete;
using Storeasy.Application.Interfaces;
using Storeasy.Infrastructure.Persistence.Context;

namespace Storeasy.Infrastructure.Persistence.Repository;

/// <summary>
/// Base repository of the application entities.
/// </summary>
/// <param name="dbContext"></param>
public class StoreasyRepositoryBase<TEntity>(StoreasyDbContext dbContext) : BulkBaseRepository<TEntity, StoreasyDbContext>(dbContext), IStoreasyRepositoryBase<TEntity>
    where TEntity : EntityBase;
