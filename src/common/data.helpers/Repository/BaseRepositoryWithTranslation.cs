using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Platform;
using EI.API.Service.Data.Helpers.Repository.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Data.Helpers.Repository;

public abstract class BaseRepositoryWithTranslation<TContext, TEntity, TTranslation>(IDatabaseClientFactory dbContextFactory, Guid clientId)
    : BaseReadRepositoryWithTranslation<TContext, TEntity, TTranslation>(dbContextFactory, clientId)
    where TContext : DbContext
    where TEntity : BaseDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : BaseDatabaseTranslationsEntity<TEntity>
{
    public virtual async Task<TEntity> InsertAsync(TEntity entity)
        => await Exec(async (dbContext, _, _) =>
                          {
                              try
                              {
                                  var added = (await dbContext.AddAsync(entity)).Entity;
                                  await PreInsertSave(dbContext, entity);
                                  _ = await dbContext.SaveChangesAsync();
                                  return added;
                              }
                              catch (DbUpdateException e)
                              {
                                  throw e.ConvertException();
                              }
                          });

    public virtual async Task<TEntity> UpdateAsync(TEntity entity)
        => await Exec(async (dbContext, entitySet, translationSet) =>
                          {
                              var recordExists = await entitySet.AnyAsync(e => e.Id == entity.Id);
                              if (!recordExists)
                              {
                                  throw new DbUpdateConcurrencyException("Record not found for UPDATE");
                              }

                              try
                              {
                                  var reattached = dbContext.Attach(entity);
                                  reattached.State = EntityState.Modified;

                                  var entityEntry = dbContext.Update(entity);
                                  var updated = entityEntry.Entity;

                                  foreach (var translation in entity.Translations)
                                  {
                                      var translationEntry = dbContext.Attach(translation);
                                      var translationExists = await translationSet.AnyAsync(e => e.Id == translation.Id && e.CultureCode == translation.CultureCode);
                                      translationEntry.State = translationExists ? EntityState.Modified : EntityState.Added;
                                  }

                                  await PreUpdateSave(dbContext, entity);

                                  _ = await dbContext.SaveChangesAsync();

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