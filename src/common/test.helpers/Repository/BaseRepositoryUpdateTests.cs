using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

[TestCategory("Integration")]
public abstract class BaseRepositoryUpdateTests<TContext, TRepo, TModel> : BaseRepositoryTests<TContext, TRepo, TModel>
    where TContext : DbContext
    where TModel : class, IDatabaseEntity
    where TRepo : class, IReadWriteRepository<TModel>
{
    [TestMethod]
    public virtual async Task UpdateAsync_UpdatesRecord()
    {
        // Arrange
        var entity = BuildModel("1");
        await DbSet.AddAsync(entity);
        await _context.SaveChangesAsync();

        var originalRowVersion = entity.RowVersion;

        var updatedBy = nameof(UpdateAsync_UpdatesRecord);
        entity.UpdatedBy = updatedBy;

        // Act
        await _repository.UpdateAsync(entity);
        var result = await DbSet.FindAsync(entity.Id);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(updatedBy, result.UpdatedBy);
        Assert.AreNotEqual(originalRowVersion, result.RowVersion);
    }

    [TestMethod]
    [ExpectedException(typeof(DbUpdateConcurrencyException))]
    public virtual async Task UpdateAsync_FailsWhenRecordNotFound()
    {
        // Arrange
        var entity = BuildModel("1");

        // Act
        _ = await _repository.UpdateAsync(entity);

        // Assert
        Assert.Fail($"Should have thrown a ${nameof(DbUpdateConcurrencyException)}");
    }

    [TestMethod]
    public virtual async Task UpdateAsync_FailsOnConcurrentUpdate()
    {
        const string updatedBy = "Updated By 1";

        // Arrange
        var entity = BuildModel("1");

        _ = await DbSet.AddAsync(entity);
        _ = await _context.SaveChangesAsync();

        // Act
        using var readWaitLock = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var updateWaitLock = new EventWaitHandle(false, EventResetMode.ManualReset);

        var updateTask =
            Task.Run(
                     async () =>
                         {
                             await using var context = MakeContext();
                             await using var repository = BuildRepo(context);

                             var entity1 = await repository.GetAsync(entity.Id);
                             Assert.IsNotNull(entity1);

                             // Wait for the other thread to have read the record
                             // ReSharper disable once AccessToDisposedClosure
                             readWaitLock.WaitOne(TimeSpan.FromSeconds(2));

                             entity1.UpdatedBy = updatedBy;

                             _ = await repository.UpdateAsync(entity1);

                             // ReSharper disable once AccessToDisposedClosure
                             updateWaitLock.Set();
                         });

        var synchronousTask =
            Task.Run(
                     async () =>
                         {
                             await using var context = MakeContext();
                             await using var repository = BuildRepo(context);

                             var entity2 = await repository.GetAsync(entity.Id);
                             Assert.IsNotNull(entity2);

                             // ReSharper disable once AccessToDisposedClosure
                             readWaitLock.Set();

                             entity2.UpdatedBy = "Not updated by this thread";

                             // Wait for the first thread to update the record
                             // ReSharper disable once AccessToDisposedClosure
                             updateWaitLock.WaitOne(TimeSpan.FromSeconds(2));

                             _ = await repository.UpdateAsync(entity2);
                         });

        try
        {
            Task.WaitAll(updateTask, synchronousTask);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        // Assert
        await using var refetchContext = MakeContext();
        await using var refetchRepository = BuildRepo(refetchContext);
        Assert.IsTrue(updateTask.IsCompletedSuccessfully);

        Assert.IsFalse(synchronousTask.IsCompletedSuccessfully);
        Assert.IsNotNull(synchronousTask.Exception);
        Assert.AreEqual(1, synchronousTask.Exception.InnerExceptions.Count);
        Assert.IsInstanceOfType<DbUpdateConcurrencyException>(synchronousTask.Exception.InnerExceptions[0]);

        var finalApplication = await refetchRepository.GetAsync(entity.Id);
        Assert.IsNotNull(finalApplication);
        Assert.AreEqual(updatedBy, finalApplication.UpdatedBy);
    }
}
