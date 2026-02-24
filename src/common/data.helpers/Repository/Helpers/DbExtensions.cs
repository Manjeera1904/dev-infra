using EI.API.Service.Data.Helpers.Exceptions;
using EI.API.Service.Data.Helpers.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Data.Helpers.Repository.Helpers;

internal static class DbExtensions
{
    internal static IQueryable<T> GetTemporalQuery<T>(this DbSet<T> dbSet, Guid primaryKey, DateTimeOffset? fromDate, DateTimeOffset? toDate)
        where T : BaseDatabaseEntity
    {
        IQueryable<T> query;
        if (fromDate is null && toDate is null)
        {
            query = dbSet.TemporalAll().Where(e => e.Id == primaryKey);
        }
        else
        {
            query = dbSet.TemporalBetween(
                                          (fromDate ?? DateTimeOffset.UnixEpoch).UtcDateTime,
                                          (toDate ?? DateTimeOffset.UtcNow).UtcDateTime
                                         )
                         .Where(e => e.Id == primaryKey);
        }

        return query;
    }

    internal static Exception ConvertException(this DbUpdateException dbException)
    {
        switch (dbException.InnerException)
        {
            case SqlException { Number: 547 }:
                return new ForeignKeyMissingException();

            case SqlException { Number: 2627 }:
            case SqlException { Number: 2601 }:
                // Could be either a PK or AK; SQL Server error numbers don't
                // seem to distinguish without trying to parse the message :-(
                if (dbException.InnerException.Message?.Contains("PRIMARY") ?? false)
                {
                    return new PrimaryKeyConflictException();
                }

                return new UniqueConstraintConflictException();

            default:
                return dbException;
        }
    }
}
