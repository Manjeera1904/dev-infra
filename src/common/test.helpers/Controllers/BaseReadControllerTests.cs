using Autofac;
using Autofac.Extras.Moq;
using AutoMapper;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Entities;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Controllers;
using EI.API.Service.Rest.Helpers.Model;
using EI.Data.TestHelpers.Controllers.Helper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Reflection;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace EI.Data.TestHelpers.Controllers;

public abstract class BaseReadControllerTests<TRepo, TController, TEntity, TDto>
    : BaseReadControllerEmptyTests<TRepo, TController, TEntity, TDto>
    where TRepo : class, IReadRepository<TEntity>
    where TEntity : class, IDatabaseEntity, new()
    where TDto : BaseDto, new()
    where TController : BaseReadController<TDto, TEntity, TRepo>
{
    #region GetAll Tests
    protected virtual IList<(int, Type?)> GetAllResponseStatusCodes =>
    [
        (StatusCodes.Status200OK, typeof(IEnumerable<TDto>)),
        (StatusCodes.Status204NoContent, null),
        (StatusCodes.Status401Unauthorized, null),
    ];

    [TestMethod]
    public virtual void GetAll_DefinesResponseType()
    {
        ControllerTestHelpers.ValidateResponseTypes<TController>(c => c.Get(), GetAllResponseStatusCodes);
    }

    [TestMethod]
    public virtual async Task GetAll_ReturnsDtos()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();
        var (dto2, entity2) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.GetAllAsync()).ReturnsAsync([entity1, entity2]);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity1)).Returns(dto1);
        mockMapper.Setup(mapper => mapper.Map<TDto>(entity2)).Returns(dto2);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Get();

        // Assert
        var result = actionResult.GetOkList<TDto>();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(dto1.Id, result[0].Id);
        Assert.AreEqual(dto2.Id, result[1].Id);
    }

    [TestMethod]
    public virtual async Task GetAll_ReturnsEmpty_WhenNoneFound()
    {
        // Arrange
        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.GetAllAsync()).ReturnsAsync([]);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Get();

        // Assert
        _ = actionResult.GetNoContent();
    }
    #endregion GetAll Tests

    #region GetById Tests
    protected virtual IList<(int, Type?)> GetByIdResponseStatusCodes =>
    [
        (StatusCodes.Status200OK, typeof(TDto)),
        (StatusCodes.Status404NotFound, null),
        (StatusCodes.Status401Unauthorized, null),
    ];

    [TestMethod]
    public virtual void GetById_DefinesResponseType()
    {
        ControllerTestHelpers.ValidateResponseTypes<TController>(c => c.Get(It.IsAny<Guid>()), GetByIdResponseStatusCodes);
    }

    [TestMethod]
    public virtual async Task GetById_ReturnsResult()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.GetAsync(entity1.Id)).ReturnsAsync(entity1);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity1)).Returns(dto1);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Get(entity1.Id);

        // Assert
        var result = actionResult.GetOkResult<TDto>();
        Assert.AreEqual(dto1.Id, result.Id);
    }

    [TestMethod]
    public virtual async Task GetById_ReturnsNotFound_WhenNoMatch()
    {
        // Arrange
        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.GetAsync(It.IsAny<Guid>())).ReturnsAsync((TEntity)null!);

        mockMapper.Setup(mapper => mapper.Map<TDto>(null)).Returns((TDto?)null!);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Get(Guid.NewGuid());

        // Assert
        Assert.IsInstanceOfType<NotFoundResult>(actionResult);
    }
    #endregion GetById Tests

    #region GetHistory Tests
    protected virtual IList<(int, Type?)> GetHistoryResponseStatusCodes =>
    [
        (StatusCodes.Status200OK, typeof(IEnumerable<EntityHistoryDto<TDto>>)),
        (StatusCodes.Status204NoContent, null),
        (StatusCodes.Status401Unauthorized, null),
    ];

    [TestMethod]
    public virtual void GetHistory_DefinesResponseType()
    {
        ControllerTestHelpers.ValidateResponseTypes<TController>(c => c.GetHistory(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), GetHistoryResponseStatusCodes);
    }


    [TestMethod]
    public virtual async Task GetHistory_ReturnsEmpty_WhenNoneFound()
    {
        // Arrange
        var mockRepository = new Mock<TRepo>(MockBehavior.Strict);
        mockRepository.Setup(repo => repo.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>())).ReturnsAsync([]);

        var mockMapper = new Mock<IMapper>(MockBehavior.Strict);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.GetHistory(Guid.NewGuid());

        // Assert
        _ = actionResult.GetNoContent();
    }

    [TestMethod]
    public virtual async Task GetHistory_ReturnsResults()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();
        var (dto2, entity2) = BuildModels();

        var mockRepository = new Mock<TRepo>(MockBehavior.Strict);
        mockRepository.Setup(repo => repo.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>())).ReturnsAsync([
            new EntityHistory<TEntity> { Entity = entity1, ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow },
            new EntityHistory<TEntity> { Entity = entity2, ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow },
        ]);

        var mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        mockMapper.Setup(mapper => mapper.Map<TDto>(entity1)).Returns(dto1);
        mockMapper.Setup(mapper => mapper.Map<TDto>(entity2)).Returns(dto2);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.GetHistory(Guid.NewGuid());

        // Assert
        var results = ((OkObjectResult)actionResult).Value as System.Collections.IEnumerable;
        Assert.IsNotNull(results);

        var enumerator = results.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.IsTrue(enumerator.MoveNext());
    }
    #endregion GetHistory Tests
}