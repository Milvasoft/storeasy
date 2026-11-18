using Milvasoft.Core.EntityBases.Concrete;
using Milvasoft.DataAccess.EfCore.Bulk.RepositoryBase.Abstract;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Base repository for Storeasy.
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface IStoreasyRepositoryBase<TEntity> : IBulkBaseRepository<TEntity> where TEntity : EntityBase;