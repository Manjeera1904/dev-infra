using EI.API.Service.Data.Helpers.Entities;
using EI.API.Service.Data.Helpers.Model;

namespace EI.API.Service.Data.Helpers.Repository;

public interface IReadRepositoryWithTranslation<TEntity, TTranslation> : IReadRepository<TEntity>
    where TEntity : IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : IDatabaseTranslationsEntity
{
    Task<IList<TEntity>> GetAllAsync(string cultureCode);

    Task<TEntity?> GetAsync(Guid primaryKey, string cultureCode);

    Task<IList<(EntityHistory<TEntity> Entity, IList<EntityHistory<TTranslation>> Translations)>> GetHistoryAsync(Guid primaryKey, string cultureCode, DateTimeOffset? from = null, DateTimeOffset? to = null);
}