using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;

namespace EI.Data.TestHelpers.Repository;

public abstract class BaseRepositoryWithTranslationTests<TContext, TRepo, TEntity, TTranslation> : BaseRepositoryTests<TContext, TRepo, TEntity>
    where TContext : DbContext
    where TRepo : class, IRepositoryWithTranslation<TEntity, TTranslation>
    where TEntity : class, IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : BaseDatabaseTranslationsEntity<TEntity>
{
    protected abstract TEntity BuildModel(string label, string cultureCode);

    protected override TEntity BuildModel(string label) => BuildModel(label, ServiceConstants.CultureCode.Default);

    protected virtual DbSet<TTranslation> TranslationSet => _context.Set<TTranslation>();

    [TestMethod]
    public virtual async Task GetAsync_ReturnsDefaultCulture_WhenRequestedCultureNotExists()
    {
        // Arrange
        var entity = BuildModel("1");
        var extraTranslation = BuildModel("2", "en-GB").Translations.Single();
        extraTranslation.Id = entity.Id;
        entity.Translations.Add(extraTranslation);

        // Act
        await _repository.InsertAsync(entity);
        var result = await _repository.GetAsync(entity.Id, "DOESNT_EXIST");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.RowVersion);
        Assert.AreNotEqual(0, result.RowVersion.Length);
        Assert.AreEqual(1, result.Translations.Count);
        Assert.AreEqual(ServiceConstants.CultureCode.Default, result.Translations.Single().CultureCode);
    }

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
    public virtual async Task UpdateAsync_CanUpdateTranslations()
    {
        // Arrange
        var entity = BuildModel("1");

        await using var insertContext = MakeContext();
        await using var insertRepo = BuildRepo(insertContext);
        {
            await insertRepo.InsertAsync(entity);
        }

        var original = await _repository.GetAsync(entity.Id);
        Assert.IsNotNull(original);

        // Modify the Translation record:
        var setName = TryUpdateProperty(original.Translations.Single(), "Name", out var nameProperty, out var updatedName);
        var setDesc = TryUpdateProperty(original.Translations.Single(), "Description", out var descProperty, out var updatedDescription);
        var updatedBy = "updated-" + original.Translations.Single().UpdatedBy;
        original.Translations.Single().UpdatedBy = updatedBy;

        // Act
        await using var updateContext = MakeContext();
        await using var updateRepo = BuildRepo(updateContext);
        {
            _ = await updateRepo.UpdateAsync(original);
        }

        // Assert
        await using var validateContext = MakeContext();
        await using var validateRepo = BuildRepo(updateContext);
        var result = await validateRepo.GetAsync(entity.Id, ServiceConstants.CultureCode.Default);
        Assert.IsNotNull(result);

        Assert.IsNotNull(result.RowVersion);
        Assert.AreNotEqual(0, result.RowVersion.Length);

        var updatedTranslation = result.Translations.Single();
        Assert.AreEqual(updatedBy, updatedTranslation.UpdatedBy);

        if (setName)
        {
            Assert.AreEqual(updatedName, nameProperty!.GetValue(updatedTranslation));
        }

        if (setDesc)
        {
            Assert.AreEqual(updatedDescription, descProperty!.GetValue(updatedTranslation));
        }
    }

    [TestMethod]
    public virtual async Task UpdateAsync_CanAddNewTranslations()
    {
        // Arrange
        var entity = BuildModel("1");

        await _repository.InsertAsync(entity);
        var original = await DbSet.FindAsync(entity.Id);
        Assert.IsNotNull(original);

        var newCultureCode = "en-GB";
        var updatedBy = "updated-" + original.Translations.Single().UpdatedBy;

        original.Translations.Single().CultureCode = newCultureCode;
        original.Translations.Single().UpdatedBy = updatedBy;

        // Act
        await using var updateContext = MakeContext();
        await using var updateRepo = BuildRepo(updateContext);
        {
            _ = await updateRepo.UpdateAsync(original);
        }

        // Assert
        await using var validateContext = MakeContext();
        await using var validateRepo = BuildRepo(updateContext);

        // -- Original Default Culture Code
        {
            var result = await validateRepo.GetAsync(entity.Id, ServiceConstants.CultureCode.Default);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.RowVersion);
            Assert.AreNotEqual(0, result.RowVersion.Length);
            Assert.AreEqual(ServiceConstants.CultureCode.Default, result.Translations.Single().CultureCode);
            Assert.AreNotEqual(updatedBy, result.UpdatedBy);
            Assert.AreNotEqual(updatedBy, result.Translations.Single().UpdatedBy);
        }

        // -- New Culture Code
        {
            var result2 = await validateRepo.GetAsync(entity.Id, newCultureCode);
            Assert.IsNotNull(result2);
            Assert.IsNotNull(result2.RowVersion);
            Assert.AreNotEqual(0, result2.RowVersion.Length);
            Assert.AreEqual(newCultureCode, result2.Translations.Single().CultureCode);
            Assert.AreNotEqual(updatedBy, result2.UpdatedBy);
            Assert.AreEqual(updatedBy, result2.Translations.Single().UpdatedBy);
        }
    }

    protected virtual bool TryUpdateProperty<TObj>(TObj model, string propertyName,
                                                   [NotNullWhen(true)] out PropertyInfo? property,
                                                   [NotNullWhen(true)] out object? updatedValue)
    {
        property = typeof(TObj).GetProperty(propertyName);
        if (property != null)
        {
            if (property.PropertyType == typeof(string))
            {
                var original = property.GetValue(model) as string;
                updatedValue = "updated-" + original;
                property.SetValue(model, updatedValue);
                return true;
            }
        }

        updatedValue = null;
        return false;
    }
}