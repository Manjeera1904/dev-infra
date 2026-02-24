using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Platform;
using EI.API.Service.Data.Helpers.Repository.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Data.Helpers.Repository;

public abstract class BaseRepository<TContext, TEntity> : BaseReadRepository<TContext, TEntity>
    where TContext : DbContext
    where TEntity : BaseDatabaseEntity
{
    protected BaseRepository(IDatabaseClientFactory dbContextFactory, Guid clientId) : base(dbContextFactory, clientId) { }

    public virtual async Task<TEntity> InsertAsync(TEntity entity)
        => await Exec(async (context, entitySet) =>
                          {
                              try
                              {
                                  var added = (await entitySet.AddAsync(entity)).Entity;
                                  await PreInsertSave(context, entity);
                                  _ = await context.SaveChangesAsync();
                                  return added;
                              }
                              catch (DbUpdateException e)
                              {
                                  throw e.ConvertException();
                              }
                          });

    public virtual async Task<TEntity> UpdateAsync(TEntity entity)
        => await Exec(async (context, entitySet) =>
                          {
                              try
                              {
                                  var updated = entitySet.Update(entity).Entity;
                                  await PreUpdateSave(context, entity);
                                  _ = await context.SaveChangesAsync();
                                  return updated;
                              }
                              catch (DbUpdateException e)
                              {
                                  throw e.ConvertException();
                              }
                          });

    /// <summary>
    /// Override to insert custom logic during an INSERT operation, after the new entity
    /// has been added to the context, but prior to calling SaveChanges().
    /// </summary>
    protected virtual Task PreInsertSave(TContext context, TEntity entity) => Task.CompletedTask;

    /// <summary>
    /// Override to insert custom logic during an UPDATE operation, after marking the entity
    /// as updated in the context, but prior to calling SaveChanges().
    /// </summary>
    protected virtual Task PreUpdateSave(TContext context, TEntity entity) => Task.CompletedTask;
}