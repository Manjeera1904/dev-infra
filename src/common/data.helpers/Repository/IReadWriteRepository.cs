using EI.API.Service.Data.Helpers.Model;

namespace EI.API.Service.Data.Helpers.Repository;

public interface IReadWriteRepository<TEntity> : IReadRepository<TEntity>
    where TEntity : IDatabaseEntity
{
    Task<TEntity> InsertAsync(TEntity entity);
    Task<TEntity> UpdateAsync(TEntity entity);
}