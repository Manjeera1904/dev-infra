using System.Data;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

[TestCategory("Integration")]
public abstract class BaseRepositoryInsertWithDateRangeTests<TContext, TRepo, TModel> : BaseRepositoryTests<TContext, TRepo, TModel>
    where TContext : DbContext
    where TModel : class, IDatabaseEntity, IDateRange
    where TRepo : class, IReadWriteRepositoryWithDateRange<TModel>
{
    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task InsertAsync_FailsWhenDateRangeOverlaps_Start()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        await _context.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        var entity2 = BuildModel("2");
        entity2.StartDate = entity1.StartDate.AddDays(-30);
        entity2.EndDate = entity1.StartDate; // <-- Overlaps

        await _repository.InsertAsync(entity2);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task InsertAsync_FailsWhenDateRangeOverlaps_End()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        await _context.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        var entity2 = BuildModel("2");
        entity2.StartDate = entity1.EndDate; // <-- Overlaps
        entity2.EndDate = entity1.EndDate.AddDays(30);

        await _repository.InsertAsync(entity2);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task InsertAsync_FailsWhenDateRangeOverlaps_Within()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        await _context.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        var entity2 = BuildModel("2");
        // entity2 range starts 1 day after and ends 1 day before
        entity2.StartDate = entity1.StartDate.AddDays(1);
        entity2.EndDate = entity1.EndDate.AddDays(-1);

        await _repository.InsertAsync(entity2);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task InsertAsync_FailsWhenDateRangeOverlaps_Contains()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        await _context.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        var entity2 = BuildModel("2");
        // entity2 range is 1 day earlier to 1 day after
        entity2.StartDate = entity1.StartDate.AddDays(-1);
        entity2.EndDate = entity1.EndDate.AddDays(1);

        await _repository.InsertAsync(entity2);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task InsertAsync_FailsWhenDateIsDefault_StartDate()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = default;
        entity1.EndDate = new DateOnly(1990, 12, 31);

        // Act
        await _repository.InsertAsync(entity1);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task InsertAsync_FailsWhenDateIsDefault_EndDate()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = default;

        // Act
        await _repository.InsertAsync(entity1);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }
}
