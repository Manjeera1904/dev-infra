using System.Data;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

public abstract class BaseRepositoryUpdateWithDateRangeTests<TContext, TRepo, TModel> : BaseRepositoryTests<TContext, TRepo, TModel>
    where TContext : DbContext
    where TModel : class, IDatabaseEntity, IDateRange
    where TRepo : class, IReadWriteRepositoryWithDateRange<TModel>
{
    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task UpdateAsync_FailsWhenDateRangeOverlaps_Start()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        var entity2 = BuildModel("2");
        entity2.StartDate = new DateOnly(1991, 01, 01);
        entity2.EndDate = new DateOnly(1991, 12, 31);

        await _context.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        entity2.StartDate = entity1.EndDate;
        await _repository.UpdateAsync(entity2);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task UpdateAsync_FailsWhenDateRangeOverlaps_End()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        var entity2 = BuildModel("2");
        entity2.StartDate = new DateOnly(1989, 01, 01);
        entity2.EndDate = new DateOnly(1991, 12, 31);

        await _context.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        entity1.EndDate = entity1.StartDate;
        await _repository.UpdateAsync(entity1);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task UpdateAsync_FailsWhenDateRangeOverlaps_Within()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        var entity2 = BuildModel("2");
        entity2.StartDate = new DateOnly(1991, 01, 01);
        entity2.EndDate = new DateOnly(1991, 12, 31);

        await _context.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        entity2.StartDate = entity1.StartDate.AddDays(1);
        entity2.EndDate = entity1.EndDate.AddDays(-1);
        await _repository.UpdateAsync(entity2);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task UpdateAsync_FailsWhenDateRangeOverlaps_Contains()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        var entity2 = BuildModel("2");
        entity2.StartDate = new DateOnly(1991, 01, 01);
        entity2.EndDate = new DateOnly(1991, 12, 31);

        await _context.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        entity2.StartDate = entity1.StartDate.AddDays(-1);
        entity2.EndDate = entity1.EndDate.AddDays(1);
        await _repository.UpdateAsync(entity2);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task UpdateAsync_FailsWhenDateIsDefault_StartDate()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        await _context.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        entity1.StartDate = default;
        await _repository.UpdateAsync(entity1);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }

    [TestMethod]
    [ExpectedException(typeof(DataException))]
    public virtual async Task UpdateAsync_FailsWhenDateIsDefault_EndDate()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.StartDate = new DateOnly(1990, 01, 01);
        entity1.EndDate = new DateOnly(1990, 12, 31);

        await _context.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        entity1.EndDate = default;
        await _repository.UpdateAsync(entity1);

        // Assert
        Assert.Fail($"Should have thrown {nameof(DataException)}");
    }
}