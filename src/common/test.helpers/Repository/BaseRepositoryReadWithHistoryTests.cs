using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

public abstract class BaseRepositoryReadWithHistoryTests<TContext, TRepo, TEntity>
    : BaseRepositoryTests<TContext, TRepo, TEntity>
    where TContext : DbContext
    where TEntity : class, IDatabaseEntity
    where TRepo : class, IReadRepository<TEntity>
{
    #region Test Reading Temporal History
    [TestMethod]
    public virtual async Task GetHistory_ReturnsEmptyWhenNone()
    {
        // Arrange

        // Act
        var result = await _repository.GetHistoryAsync(Guid.NewGuid());

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public virtual async Task GetHistory_ReturnsHistory()
    {
        // Arrange
        var entity1 = BuildModel("one");
        await _context.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetHistoryAsync(entity1.Id);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(entity1.Id, result[0].Entity.Id);
    }

    [TestMethod]
    public virtual async Task GetHistory_ReturnsMultipleHistory()
    {
        // Arrange
        var testStart = DateTime.UtcNow;

        Guid entityId;
        await using (var localContext = MakeContext())
        {
            var entity1 = BuildModel("one");
            await localContext.AddAsync(entity1);
            await localContext.SaveChangesAsync();

            entityId = entity1.Id;
        }

        await Task.Delay(10);

        await using (var localContext = MakeContext())
        {
            var updateEntity = await localContext.Set<TEntity>().FindAsync(entityId);
            Assert.IsNotNull(updateEntity);
            updateEntity.UpdatedBy = Guid.NewGuid().ToString();
            _context.Update(updateEntity);
            await _context.SaveChangesAsync();
        }

        // Act
        var result = await _repository.GetHistoryAsync(entityId);

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(entityId, result[0].Entity.Id);
        Assert.IsTrue(testStart <= result[0].ValidTo);

        Assert.AreEqual(entityId, result[1].Entity.Id);
        Assert.IsTrue(result[0].ValidTo <= result[1].ValidFrom);
        Assert.AreEqual(DateTime.MaxValue.Date, result[1].ValidTo.Date);
    }

    [TestMethod]
    public virtual async Task GetHistory_ReturnsEmptyWhenNone_WithDateRange()
    {
        Guid entityId;
        await using (var localContext = MakeContext())
        {
            var entity1 = BuildModel("one");
            await localContext.AddAsync(entity1);
            await localContext.SaveChangesAsync();

            entityId = entity1.Id;
        }

        await Task.Delay(10);

        await using (var localContext = MakeContext())
        {
            var updateEntity = await localContext.Set<TEntity>().FindAsync(entityId);
            Assert.IsNotNull(updateEntity);
            updateEntity.UpdatedBy = Guid.NewGuid().ToString();
            _context.Update(updateEntity);
            await _context.SaveChangesAsync();
        }

        // Act
        var result = await _repository.GetHistoryAsync(entityId, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9));

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public virtual async Task GetHistory_ReturnsMultipleHistory_WithClosedDateRange()
    {
        // Arrange
        var testStart = DateTime.UtcNow;

        Guid entityId;
        await using (var localContext = MakeContext())
        {
            var entity1 = BuildModel("one");
            await localContext.AddAsync(entity1);
            await localContext.SaveChangesAsync();

            entityId = entity1.Id;
        }

        await Task.Delay(10);

        await using (var localContext = MakeContext())
        {
            var updateEntity = await localContext.Set<TEntity>().FindAsync(entityId);
            Assert.IsNotNull(updateEntity);
            updateEntity.UpdatedBy = Guid.NewGuid().ToString();
            _context.Update(updateEntity);
            await _context.SaveChangesAsync();
        }

        // Act
        var result = await _repository.GetHistoryAsync(entityId, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(entityId, result[0].Entity.Id);
        Assert.IsTrue(testStart <= result[0].ValidTo);

        Assert.AreEqual(entityId, result[1].Entity.Id);
        Assert.IsTrue(result[0].ValidTo <= result[1].ValidFrom);
        Assert.AreEqual(DateTime.MaxValue.Date, result[1].ValidTo.Date);
    }

    [TestMethod]
    public virtual async Task GetHistory_ReturnsMultipleHistory_WithOpenDateRange()
    {
        // Arrange
        var testStart = DateTime.UtcNow;

        Guid entityId;
        await using (var localContext = MakeContext())
        {
            var entity1 = BuildModel("one");
            await localContext.AddAsync(entity1);
            await localContext.SaveChangesAsync();

            entityId = entity1.Id;
        }

        await Task.Delay(10);

        await using (var localContext = MakeContext())
        {
            var updateEntity = await localContext.Set<TEntity>().FindAsync(entityId);
            Assert.IsNotNull(updateEntity);
            updateEntity.UpdatedBy = Guid.NewGuid().ToString();
            _context.Update(updateEntity);
            await _context.SaveChangesAsync();
        }

        // Act
        var result = await _repository.GetHistoryAsync(entityId, DateTime.UtcNow.AddDays(-10));

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(entityId, result[0].Entity.Id);
        Assert.IsTrue(testStart <= result[0].ValidTo);

        Assert.AreEqual(entityId, result[1].Entity.Id);
        Assert.IsTrue(result[0].ValidTo <= result[1].ValidFrom);
        Assert.AreEqual(DateTime.MaxValue.Date, result[1].ValidTo.Date);
    }
    #endregion Test Reading Temporal History
}
