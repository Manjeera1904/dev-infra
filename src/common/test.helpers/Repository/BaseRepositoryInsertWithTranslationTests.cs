using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

public abstract class BaseRepositoryInsertWithTranslationTests<TContext, TRepo, TEntity, TTranslation> : BaseRepositoryInsertTests<TContext, TRepo, TEntity>
    where TContext : DbContext
    where TRepo : class, IRepositoryWithTranslation<TEntity, TTranslation>
    where TEntity : class, IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : BaseDatabaseTranslationsEntity<TEntity>
{
    protected abstract TEntity BuildModel(string label, string cultureCode);

    protected override TEntity BuildModel(string label) => BuildModel(label, ServiceConstants.CultureCode.Default);

    protected virtual DbSet<TTranslation> TranslationSet => _context.Set<TTranslation>();

    // Nothing to do beyond base class INSERT tests?
}
