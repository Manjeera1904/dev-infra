using EI.API.Service.Data.Helpers.Model;

namespace EI.API.Service.Data.Helpers.Repository;

public interface IRepositoryWithTranslation<TEntity, TTranslation> : IReadWriteRepository<TEntity>, IReadRepositoryWithTranslation<TEntity, TTranslation>
    where TEntity : IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : IDatabaseTranslationsEntity
{
}