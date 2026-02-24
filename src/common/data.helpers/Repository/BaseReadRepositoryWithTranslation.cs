using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using EI.API.Service.Data.Helpers.Entities;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Platform;
using EI.API.Service.Data.Helpers.Repository.Helpers;
using EI.API.Service.Data.Helpers.Util;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Data.Helpers.Repository;

public abstract class BaseReadRepositoryWithTranslation<TContext, TEntity, TTranslation> : IReadRepositoryWithTranslation<TEntity, TTranslation>
    where TContext : DbContext
    where TEntity : BaseDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : BaseDatabaseTranslationsEntity<TEntity>
{
    protected readonly IDatabaseClientFactory _dbContextFactory;
    protected readonly Guid _clientId;

    protected readonly ConcurrentDictionary<string, ISet<string>> _cultureCodeSearches = new(StringComparer.OrdinalIgnoreCase);

    protected TContext? _dbContextPrivate;
    protected DbSet<TEntity>? _entitySetPrivate;
    protected DbSet<TTranslation>? _translationSetPrivate;

    protected BaseReadRepositoryWithTranslation(IDatabaseClientFactory dbContextFactory, Guid clientId)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        if (!IsValidClientId(clientId)) throw new ArgumentException("Invalid ClientId", nameof(clientId));

        _dbContextFactory = dbContextFactory;
        _clientId = clientId;
    }

    protected virtual async Task<TResult> Exec<TResult>(Func<TContext, DbSet<TEntity>, DbSet<TTranslation>, Task<TResult>> action)
    {
        _dbContextPrivate ??= await BuildContext();
        _entitySetPrivate ??= _dbContextPrivate.Set<TEntity>();
        _translationSetPrivate ??= _dbContextPrivate.Set<TTranslation>();
        return await action(_dbContextPrivate, _entitySetPrivate, _translationSetPrivate);
    }

    protected abstract string DataSourceKey { get; }

    // Override in Platform's repos to allow for empty ID for bootstrapping
    protected virtual bool IsValidClientId(Guid clientId) => clientId != Guid.Empty;

    protected virtual async Task<TContext> BuildContext()
    {
        return await _dbContextFactory.GetDbContext<TContext>(_clientId, DataSourceKey);
    }

    protected virtual ISet<string> GetCultureCodeSet(string cultureCode)
    {
        return TranslationsHelper.GetCultureCodeSet(cultureCode);
    }

    [return: NotNullIfNotNull(nameof(entity))]
    protected virtual TEntity? SelectBestTranslation(TEntity? entity, IList<TTranslation> translations, string preferredCultureCode)
    {
        return TranslationsHelper.SelectBestTranslation(entity, translations, preferredCultureCode);
    }

    protected virtual async Task<IList<TEntity>> GetHelper(string cultureCode, Expression<Func<TEntity, bool>> filterExpression)
        => await Exec(async (_, entitySet, translationSet) =>
                          {
                              var cultureCodes = GetCultureCodeSet(cultureCode);
                              var results =
                                  await entitySet.Where(filterExpression)
                                                 .Select(entity => new
                                                 {
                                                     Entity = entity,
                                                     Translations = translationSet.Where(t => t.Id == entity.Id && cultureCodes.Contains(t.CultureCode)).ToList(),
                                                 })
                                                 .ToListAsync();

                              var filteredAndMerged =
                                  results.Select(anon => SelectBestTranslation(anon.Entity, anon.Translations, cultureCode))
                                         .ToList();

                              return filteredAndMerged;
                          });

    protected virtual async Task<IList<TEntity>> GetByTranslationHelper(string cultureCode, Expression<Func<TTranslation, bool>> filterExpression)
        => await Exec(async (_, entitySet, translationSet) =>
                          {
                              var cultureCodes = GetCultureCodeSet(cultureCode);
                              var translations =
                                  await (
                                            from translation in translationSet.Where(t => cultureCodes.Contains(t.CultureCode))
                                                                              .Where(filterExpression)
                                            join entity in entitySet on translation.Id equals entity.Id
                                            select new
                                            {
                                                Entity = entity,
                                                Translation = translation
                                            }
                                            into tuples
                                            group tuples by tuples.Entity.Id
                                        )
                                      .ToListAsync();

                              return translations.Select(grouping =>
                                                             {
                                                                 var listified = grouping.ToList();
                                                                 var entity = listified.First().Entity;
                                                                 var entityTranslations = listified.Select(t => t.Translation).ToList();
                                                                 return SelectBestTranslation(entity, entityTranslations, cultureCode);
                                                             })
                                                 .ToList();
                          });


    public virtual async Task<IList<TEntity>> GetAllAsync() => await GetAllAsync(ServiceConstants.CultureCode.Default);

    public virtual async Task<TEntity?> GetAsync(Guid primaryKey) => await GetAsync(primaryKey, ServiceConstants.CultureCode.Default);

    public virtual async Task<IList<TEntity>> GetAllAsync(string cultureCode)
    {
        return await GetHelper(cultureCode, _ => true);
    }

    public virtual async Task<TEntity?> GetAsync(Guid primaryKey, string cultureCode) =>
        (await GetHelper(cultureCode, e => e.Id == primaryKey)).SingleOrDefault();

    public virtual async Task<IList<EntityHistory<TEntity>>> GetHistoryAsync(Guid primaryKey, DateTimeOffset? from = null, DateTimeOffset? to = null)
        => (await GetHistoryAsync(primaryKey, ServiceConstants.CultureCode.Default, from, to)).Select(tp => tp.Entity).ToList();

    public virtual async Task<IList<(EntityHistory<TEntity> Entity, IList<EntityHistory<TTranslation>> Translations)>> GetHistoryAsync(Guid primaryKey, string cultureCode, DateTimeOffset? from = null, DateTimeOffset? to = null)
        => await Exec(async (dbContext, entitySet, translationSet) =>
                          {
                              dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

                              var entities = (
                                                 await entitySet.GetTemporalQuery(primaryKey, from, to)
                                                                .Select(t => new
                                                                {
                                                                    ValidFrom = EF.Property<DateTime>(t, nameof(EntityHistory<TTranslation>.ValidFrom)),
                                                                    ValidTo = EF.Property<DateTime>(t, nameof(EntityHistory<TTranslation>.ValidTo)),
                                                                    Entity = t,
                                                                })
                                                                .OrderBy(e => e.ValidFrom)
                                                                .ToListAsync()
                                             )
                                             .Select(t => new EntityHistory<TEntity>
                                             {
                                                 Entity = t.Entity,
                                                 ValidFrom = t.ValidFrom,
                                                 ValidTo = t.ValidTo,
                                             })
                                             .ToList();

                              var cultureCodeSearch = new HashSet<string>
                                                      {
                                                          ServiceConstants.CultureCode.Default,
                                                          cultureCode
                                                      };
                              var translations = (
                                                     await translationSet.GetTemporalQuery(primaryKey, from, to)
                                                                         .Where(t => cultureCodeSearch.Contains(t.CultureCode))
                                                                         .Select(t => new
                                                                         {
                                                                             ValidFrom = EF.Property<DateTime>(t, nameof(EntityHistory<TTranslation>.ValidFrom)),
                                                                             ValidTo = EF.Property<DateTime>(t, nameof(EntityHistory<TTranslation>.ValidTo)),
                                                                             Entity = t,
                                                                         })
                                                                         .OrderBy(e => e.ValidFrom)
                                                                         .ToListAsync()
                                                 )
                                                 .Select(t => new EntityHistory<TTranslation>
                                                 {
                                                     Entity = t.Entity,
                                                     ValidFrom = t.ValidFrom,
                                                     ValidTo = t.ValidTo,
                                                 })
                                                 .ToList();

                              var results = new List<(EntityHistory<TEntity> Entity, IList<EntityHistory<TTranslation>> Translations)>();

                              foreach (var entity in entities)
                              {
                                  var matchingTranslations = translations.Where(t => DateRange.Overlaps(entity.ValidFrom, entity.ValidTo, t.ValidFrom, t.ValidTo))
                                                                         .OrderBy(t => t.ValidFrom)
                                                                         .ToList();

                                  results.Add((entity, matchingTranslations));
                              }

                              return results;
                          });

    protected bool _disposed = false;
    public virtual async ValueTask DisposeAsync()
    {
        if (_dbContextPrivate != null)
        {
            await _dbContextPrivate.DisposeAsync();
        }
    }
}