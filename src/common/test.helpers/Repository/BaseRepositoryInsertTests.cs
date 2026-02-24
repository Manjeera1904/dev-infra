using EI.API.Service.Data.Helpers.Exceptions;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

[TestCategory("Integration")]
public abstract class BaseRepositoryInsertTests<TContext, TRepo, TModel> : BaseRepositoryTests<TContext, TRepo, TModel>
    where TContext : DbContext
    where TModel : class, IDatabaseEntity
    where TRepo : class, IReadWriteRepository<TModel>
{
    [TestMethod]
    public virtual async Task AddAsync_AddsRecord()
    {
        // Arrange
        var entity = BuildModel("1");

        // Act
        await _repository.InsertAsync(entity);
        var result = await DbSet.FindAsync(entity.Id);

        // Assert
        Assert.IsNotNull(result);

        Assert.IsNotNull(result.RowVersion);
        Assert.AreNotEqual(0, result.RowVersion.Length);
    }

    [TestMethod]
    [ExpectedException(typeof(PrimaryKeyConflictException))]
    public virtual async Task InsertAsync_FailsOnPrimaryKeyDuplicate()
    {
        // Arrange
        var entity = BuildModel("1");

        // Act
        await _repository.InsertAsync(entity);

        // ... try inserting again with a new context+repo
        await using var dupContext = MakeContext();
        await using var dupRepository = BuildRepo(dupContext);
        await dupRepository.InsertAsync(entity);

        // Assert
        Assert.Fail($"Should have thrown a ${nameof(DbUpdateConcurrencyException)}");
    }
}