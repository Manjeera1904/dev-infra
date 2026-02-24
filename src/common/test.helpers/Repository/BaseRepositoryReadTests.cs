using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

[TestCategory("Integration")]
public abstract class BaseRepositoryReadTests<TContext, TRepo, TEntity> : BaseRepositoryTests<TContext, TRepo, TEntity>
    where TContext : DbContext
    where TEntity : class, IDatabaseEntity
    where TRepo : class, IReadRepository<TEntity>
{
    [TestMethod]
    public virtual async Task GetAllAsync_ReturnsAll()
    {
        // Arrange
        var entity1 = BuildModel("one");
        var entity2 = BuildModel("two");

        await _context.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.IsTrue(2 <= result.Count);
        Assert.IsTrue(result.Any(e => e.Id == entity1.Id));
        Assert.IsTrue(result.Any(e => e.Id == entity2.Id));
    }

    [TestMethod]
    public virtual async Task GetAsync_RetrievesRecord()
    {
        // Arrange
        var entity = BuildModel("1");

        await DbSet.AddAsync(entity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAsync(entity.Id);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.RowVersion);
        Assert.AreNotEqual(0, result.RowVersion.Length);
    }

    [TestMethod]
    public virtual async Task GetAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange

        // Act
        var result = await _repository.GetAsync(Guid.NewGuid());

        // Assert
        Assert.IsNull(result);
    }
}