using EI.API.Service.Data.Helpers.Entities;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Platform;
using EI.API.Service.Data.Helpers.Repository.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Data.Helpers.Repository;

public abstract class BaseReadRepository<TContext, TEntity> : IReadRepository<TEntity>
    where TContext : DbContext
    where TEntity : BaseDatabaseEntity
{
    protected readonly IDatabaseClientFactory _dbContextFactory;
    protected readonly Guid _clientId;

    protected TContext? _dbContextPrivate;
    protected DbSet<TEntity>? _entitySetPrivate;

    protected BaseReadRepository(IDatabaseClientFactory dbContextFactory, Guid clientId)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        if (!IsValidClientId(clientId)) throw new ArgumentException("Invalid ClientId", nameof(clientId));

        _dbContextFactory = dbContextFactory;
        _clientId = clientId;
    }

    protected virtual async Task<TResult> Exec<TResult>(Func<TContext, DbSet<TEntity>, Task<TResult>> action)
    {
        _dbContextPrivate ??= await BuildContext();
        _entitySetPrivate ??= _dbContextPrivate.Set<TEntity>();
        return await action(_dbContextPrivate, _entitySetPrivate);
    }

    protected abstract string DataSourceKey { get; }

    // Override in Platform's repos to allow for empty ID for bootstrapping
    protected virtual bool IsValidClientId(Guid clientId) => clientId != Guid.Empty;

    // protected virtual Task<DbSet<TEntity> EntitySet => DatabaseContext.Set<TEntity>();

    public virtual async Task<IList<TEntity>> GetAllAsync()
        => await Exec(async (_, entitySet) => await entitySet.ToListAsync());

    public virtual async Task<TEntity?> GetAsync(Guid entityId)
        => await Exec(async (_, entitySet) => await entitySet.FindAsync(entityId));

    public virtual async Task<IList<EntityHistory<TEntity>>> GetHistoryAsync(Guid primaryKey, DateTimeOffset? from = null, DateTimeOffset? to = null)
        => await Exec(async (_, entitySet) =>
                          {
                              var entities = await entitySet
                                                   .GetTemporalQuery(primaryKey, from, to)
                                                   .Select(t => new
                                                   {
                                                       ValidFrom = EF.Property<DateTime>(t, nameof(EntityHistory<TEntity>.ValidFrom)),
                                                       ValidTo = EF.Property<DateTime>(t, nameof(EntityHistory<TEntity>.ValidTo)),
                                                       Entity = t,
                                                   })
                                                   .OrderBy(tpl => tpl.ValidFrom)
                                                   .ToListAsync();

                              var history = entities.Select(tpl => new EntityHistory<TEntity>
                              {
                                  Entity = tpl.Entity,
                                  ValidFrom = tpl.ValidFrom,
                                  ValidTo = tpl.ValidTo,
                              })
                                                    .ToList();
                              return history;
                          });

    protected virtual async Task<TContext> BuildContext()
    {
        return await _dbContextFactory.GetDbContext<TContext>(_clientId, DataSourceKey);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_dbContextPrivate != null)
        {
            await _dbContextPrivate.DisposeAsync();
        }
    }
}