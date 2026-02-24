using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

[TestCategory("Integration")]
public abstract class BaseRepositoryReadWithTranslationTests<TContext, TRepo, TEntity, TTranslation> : BaseRepositoryReadTests<TContext, TRepo, TEntity>
    where TContext : DbContext
    where TRepo : class, IReadRepositoryWithTranslation<TEntity, TTranslation>
    where TEntity : class, IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : BaseDatabaseTranslationsEntity<TEntity>
{
    protected abstract TEntity BuildModel(string label, string? cultureCode = null);

    protected override TEntity BuildModel(string label) => BuildModel(label, ServiceConstants.CultureCode.Default);

    [TestMethod]
    public virtual async Task GetAllAsync_WithCultureCode_ReturnsAll()
    {
        // Arrange
        var entity1 = BuildModel("1");
        var entity2 = BuildModel("2");

        await DbSet.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(ServiceConstants.CultureCode.Default);

        // Assert
        Assert.IsTrue(2 <= result.Count);
        Assert.IsTrue(result.Any(e => e.Id == entity1.Id));
        Assert.IsTrue(result.Any(e => e.Id == entity2.Id));
        Assert.IsTrue(result.All(e => e.Translations.All(t => t.CultureCode.Equals(ServiceConstants.CultureCode.Default, StringComparison.CurrentCultureIgnoreCase))));
    }

    [TestMethod]
    public virtual async Task GetAllAsync_WithUniqueCultureCode_ReturnsAll()
    {
        // Arrange
        var entity1 = BuildModel("1");
        var entity2 = BuildModel("2");

        await DbSet.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync("DOESNT_EXIST");

        // Assert
        Assert.IsTrue(2 <= result.Count);
        Assert.IsTrue(result.Any(e => e.Id == entity1.Id));
        Assert.IsTrue(result.Any(e => e.Id == entity2.Id));
        Assert.IsTrue(result.All(e => e.Translations.All(t => t.CultureCode.Equals(ServiceConstants.CultureCode.Default, StringComparison.CurrentCultureIgnoreCase))));
    }

    [TestMethod]
    public virtual async Task GetAllAsync_WithMissingCultureCode_ReturnsDefaultCultureCode()
    {
        // Arrange
        var entity1 = BuildModel("1");
        var entity2 = BuildModel("2");

        await DbSet.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(ServiceConstants.CultureCode.Default);

        // Assert
        Assert.IsTrue(2 <= result.Count);
        Assert.IsTrue(result.Any(e => e.Id == entity1.Id));
        Assert.IsTrue(result.Any(e => e.Id == entity2.Id));
        Assert.IsTrue(result.All(e => e.Translations.All(t => t.CultureCode.Equals(ServiceConstants.CultureCode.Default, StringComparison.CurrentCultureIgnoreCase))));
    }

    [TestMethod]
    public virtual async Task GetAllAsync_WithCultureCodeInLowerCase_ReturnsAll()
    {
        // Arrange
        var entity1 = BuildModel("1");
        var entity2 = BuildModel("2");
        entity1.Translations[0].CultureCode = ServiceConstants.CultureCode.Default.ToLower();
        entity2.Translations[0].CultureCode = ServiceConstants.CultureCode.Default.ToLower();

        await DbSet.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(ServiceConstants.CultureCode.Default);

        // Assert
        Assert.IsTrue(2 <= result.Count);
        Assert.IsTrue(result.Any(e => e.Id == entity1.Id));
        Assert.IsTrue(result.Any(e => e.Id == entity2.Id));
        Assert.IsTrue(2 <= result.Where(e => e.Translations.All(t => t.CultureCode == ServiceConstants.CultureCode.Default.ToLower())).Count());
    }

    [TestMethod]
    public virtual async Task GetAllAsync_WithCultureCodeInUpperCaseCase_ReturnsAll()
    {
        // Arrange
        var entity1 = BuildModel("1");
        var entity2 = BuildModel("2");
        entity1.Translations[0].CultureCode = ServiceConstants.CultureCode.Default.ToUpper();
        entity2.Translations[0].CultureCode = ServiceConstants.CultureCode.Default.ToUpper();

        await DbSet.AddRangeAsync(entity1, entity2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(ServiceConstants.CultureCode.Default);

        // Assert
        Assert.IsTrue(2 <= result.Count);
        Assert.IsTrue(result.Any(e => e.Id == entity1.Id));
        Assert.IsTrue(result.Any(e => e.Id == entity2.Id));
        Assert.IsTrue(2 <= result.Where(e => e.Translations.All(t => t.CultureCode == ServiceConstants.CultureCode.Default.ToUpper())).Count());
    }

    [TestMethod]
    public virtual async Task GetAsync_WithCultureCodeInLowerCase_ReturnsAll()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.Translations[0].CultureCode = ServiceConstants.CultureCode.Default.ToLower();

        await DbSet.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAsync(entity1.Id, ServiceConstants.CultureCode.Default);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Id == entity1.Id);
        Assert.IsTrue(result.Translations.All(t => t.CultureCode == ServiceConstants.CultureCode.Default.ToLower()));
    }

    [TestMethod]
    public virtual async Task GetAsync_WithCultureCodeInUpperCaseCase_ReturnsAll()
    {
        // Arrange
        var entity1 = BuildModel("1");
        entity1.Translations[0].CultureCode = ServiceConstants.CultureCode.Default.ToUpper();

        await DbSet.AddAsync(entity1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAsync(entity1.Id, ServiceConstants.CultureCode.Default);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Id == entity1.Id);
        Assert.IsTrue(result.Translations.All(t => t.CultureCode == ServiceConstants.CultureCode.Default.ToUpper()));
    }
}
