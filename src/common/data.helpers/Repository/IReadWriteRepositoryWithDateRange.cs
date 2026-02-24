using EI.API.Service.Data.Helpers.Model;

namespace EI.API.Service.Data.Helpers.Repository;

public interface IReadWriteRepositoryWithDateRange<TEntity> : IReadWriteRepository<TEntity>
    where TEntity : IDatabaseEntity, IDateRange
{
    Task<IEnumerable<TEntity>> GetConflictingDateRanges(TEntity entity);
}