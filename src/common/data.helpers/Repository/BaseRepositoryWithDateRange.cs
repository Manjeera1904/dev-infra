using System.Data;
using System.Linq.Expressions;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Platform;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Data.Helpers.Repository;

public abstract class BaseRepositoryWithDateRange<TContext, TEntity>(IDatabaseClientFactory dbContextFactory, Guid clientId) : BaseRepository<TContext, TEntity>(dbContextFactory, clientId), IReadWriteRepositoryWithDateRange<TEntity>
    where TContext : DbContext
    where TEntity : BaseDatabaseEntity, IDateRange
{
    public async Task<IEnumerable<TEntity>> GetConflictingDateRanges(TEntity entity)
        => await Exec(async (_, entitySet) =>
                          await entitySet

                                // Don't look at self!
                                .Where(existing => existing.Id != entity.Id)

                                // Only look at entities that have the same "uniqueness" criteria
                                .Where(TestForOverlapCandidate(entity))

                                // Now check for overlapping date ranges:
                                .Where(conflict =>

                                           // The entity's StartDate is included in the existing record's Date Range
                                           (conflict.StartDate <= entity.StartDate && entity.StartDate <= conflict.EndDate) ||

                                           // The existing record's StartDate is included in the conflict's Date Range
                                           (entity.StartDate <= conflict.StartDate && conflict.StartDate <= entity.EndDate)
                                      )
                                .ToListAsync());

    protected abstract Expression<Func<TEntity, bool>> TestForOverlapCandidate(TEntity other);

    public override async Task<TEntity> InsertAsync(TEntity entity)
    {
        // Re-validate for now; this should be caught in the controller
        // and a better error message given, but this is the failsafe.
        await ValidateDateRangeAsync(entity);
        ValidateDates(entity);

        return await base.InsertAsync(entity);
    }

    public override async Task<TEntity> UpdateAsync(TEntity entity)
    {
        // Re-validate for now; this should be caught in the controller
        // and a better error message given, but this is the failsafe.
        await ValidateDateRangeAsync(entity);
        ValidateDates(entity);

        return await base.UpdateAsync(entity);
    }

    protected virtual async ValueTask ValidateDateRangeAsync(TEntity entity)
    {
        var conflicts = (await GetConflictingDateRanges(entity))
#if DEBUG
                .ToList()
#endif
            ;
        if (conflicts.Any())
        {
#if DEBUG
            var incomingRange = $"Incoming: {entity.StartDate:yyyy-MM-dd}-{entity.EndDate:yyyy-MM-dd}";
            var existingRange = conflicts.Aggregate("Existing: ", (agg, ent) => $"{agg}[{ent.StartDate:yyyy-MM-dd}-{ent.EndDate:yyyy-MM-dd}]");

#endif
            throw new DataException("Date range conflicts with existing records"
#if DEBUG
                                    + $"\n{incomingRange}\n{existingRange}"
#endif
                                   );
        }
    }

    protected virtual void ValidateDates(TEntity entity)
    {
        if (entity.StartDate == default || entity.EndDate == default)
        {
            throw new DataException($"Dates cannot be {DateOnly.MinValue}");
        }
    }
}