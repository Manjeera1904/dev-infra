using EI.API.Service.Data.Helpers.Entities;
using EI.API.Service.Data.Helpers.Model;

namespace EI.API.Service.Data.Helpers.Repository;

public interface IReadRepository<TEntity> : IAsyncDisposable
    where TEntity : IDatabaseEntity
{
    Task<IList<TEntity>> GetAllAsync();

    Task<TEntity?> GetAsync(Guid primaryKey);

    Task<IList<EntityHistory<TEntity>>> GetHistoryAsync(Guid primaryKey, DateTimeOffset? from = null, DateTimeOffset? to = null);
}