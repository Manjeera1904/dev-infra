using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Controllers;
using EI.API.Service.Rest.Helpers.Model;
using EI.Data.TestHelpers.Controllers.Helper;

namespace EI.Data.TestHelpers.Controllers;

public abstract class BaseReadTranslationControllerWithoutHistoryTests<TRepo, TController, TEntity, TTranslation, TDto>
    : BaseReadTranslationControllerTests<TRepo, TController, TEntity, TTranslation, TDto>
    where TRepo : class, IReadRepositoryWithTranslation<TEntity, TTranslation>
    where TEntity : class, IDatabaseEntityWithTranslation<TTranslation>, new()
    where TDto : BaseTranslationDto, new()
    where TTranslation : class, IDatabaseTranslationsEntity, new()
    where TController : BaseReadTranslationController<TDto, TEntity, TTranslation, TRepo>
{
    public override void GetHistory_DefinesResponseType()
    {
        // Nothing to validate - controller does not support history
    }

    [TestMethod]
    public override async Task GetHistory_ReturnsEmpty_WhenNoneFound()
    {
        // Arrange
        var (mockRepository, mockMapper) = GetMocks();

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.GetHistory(Guid.NewGuid());

        // Assert
        _ = actionResult.GetNotFound();
    }

    [TestMethod]
    public override async Task GetHistory_ReturnsResults()
    {
        // Arrange
        var (mockRepository, mockMapper) = GetMocks();

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.GetHistory(Guid.NewGuid());

        // Assert
        _ = actionResult.GetNotFound();
    }
}

public abstract class BaseControllerWithoutHistoryTests<TRepo, TController, TEntity, TDto>
    : BaseControllerTests<TRepo, TController, TEntity, TDto>
    where TRepo : class, IReadWriteRepository<TEntity>
    where TEntity : class, IDatabaseEntity, new()
    where TDto : BaseDto, new()
    where TController : BaseController<TDto, TEntity, TRepo>
{
    public override void GetHistory_DefinesResponseType()
    {
        // Nothing to validate - controller does not support history
    }

    [TestMethod]
    public override async Task GetHistory_ReturnsEmpty_WhenNoneFound()
    {
        // Arrange
        var (mockRepository, mockMapper) = GetMocks();

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.GetHistory(Guid.NewGuid());

        // Assert
        _ = actionResult.GetNotFound();
    }

    [TestMethod]
    public override async Task GetHistory_ReturnsResults()
    {
        // Arrange
        var (mockRepository, mockMapper) = GetMocks();

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.GetHistory(Guid.NewGuid());

        // Assert
        _ = actionResult.GetNotFound();
    }
}
