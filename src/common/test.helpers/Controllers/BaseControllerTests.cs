using System.Data;
using System.Security.Claims;
using System.Transactions;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Exceptions;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Controllers;
using EI.API.Service.Rest.Helpers.Model;
using EI.Data.TestHelpers.Controllers.Helper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EI.Data.TestHelpers.Controllers;

public abstract class BaseControllerTests<TRepo, TController, TEntity, TDto> : BaseReadControllerTests<TRepo, TController, TEntity, TDto>
    where TRepo : class, IReadWriteRepository<TEntity>
    where TEntity : class, IDatabaseEntity, new()
    where TDto : BaseDto, new()
    where TController : BaseController<TDto, TEntity, TRepo>
{
    #region POST Tests

    protected virtual bool SupportsPut => true;

    protected virtual IList<(int, Type?)> PostResponseStatusCodes =>
    [
        (StatusCodes.Status201Created, typeof(TDto)),
        (StatusCodes.Status400BadRequest, null),
        (StatusCodes.Status401Unauthorized, null),
        (StatusCodes.Status404NotFound, null),
        (StatusCodes.Status409Conflict, null),
    ];

    [TestMethod]
    public virtual void Post_DefinesResponseType()
    {
        ControllerTestHelpers.ValidateResponseTypes<TController>(c => c.Post(It.IsAny<TDto>()), PostResponseStatusCodes);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsCreated_WhenDtoHasId()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        var (_, result) = actionResult.GetCreatedResult<TDto>();
        Assert.AreEqual(entity.Id, result.Id);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsCreated_AndAssignsId()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for insert
        dto.Id = null;

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        _ = actionResult.GetCreatedResult<TDto>();

        // the controller should have assigned an ID to the DTO prior to calling the Repo
        Assert.IsNotNull(entity.Id);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsCreated_AndAssignsUpdatedBy()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for insert
        dto.Id = null;

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(It.Is<TEntity>(e => ReferenceEquals(entity, e) && e.UpdatedBy == HttpContextUserInfo.Username)))
                      .ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        _ = actionResult.GetCreatedResult<TDto>();

        // the controller should have assigned an ID to the DTO prior to calling the Repo
        Assert.IsNotNull(entity.Id);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsUnauthorized_WhenMissingAuthToken()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper, () => null);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        _ = actionResult.GetUnauthorized();
    }

    [TestMethod]
    public virtual async Task Post_ReturnsUnauthorized_WhenAuthToken_MissingUserId()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper,
                                       () => new ClaimsIdentity(
                                                                new List<Claim>
                                                                {
                                                                    // new(ServiceConstants.Authorization.UserId, HttpContextUserInfo.UserId.ToString()),
                                                                    new(ServiceConstants.Authorization.Username, HttpContextUserInfo.Username),
                                                                    new(ServiceConstants.Authorization.Client, Guid.NewGuid().ToString())
                                                                }));

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        _ = actionResult.GetUnauthorized();
    }

    [TestMethod]
    public virtual async Task Post_ReturnsUnauthorized_WhenAuthToken_MissingUsername()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper,
                                       () => new ClaimsIdentity(
                                                                new List<Claim>
                                                                {
                                                                    new(ServiceConstants.Authorization.UserId, HttpContextUserInfo.UserId.ToString()),
                                                                    // new(ServiceConstants.Authorization.Username, HttpContextUserInfo.Username),
                                                                    new(ServiceConstants.Authorization.Client, Guid.NewGuid().ToString())
                                                                }));

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        _ = actionResult.GetUnauthorized();
    }

    [TestMethod]
    public virtual async Task Post_ReturnsConflict_WhenPrimaryKeyConflict()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).Throws(new PrimaryKeyConflictException());

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        var message = actionResult.GetConflictResult<string>();
        Assert.IsNotNull(message);
        Assert.AreEqual(1, message.Count);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsConflict_WhenUniqueKeyConflict()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).Throws(new UniqueConstraintConflictException());

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        var message = actionResult.GetConflictResult<string>();
        Assert.IsNotNull(message);
        Assert.AreEqual(1, message.Count);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsBadRequest_WhenForeignKeyMissing()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).Throws(new ForeignKeyMissingException());

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        _ = actionResult.GetBadRequestResultWithMessage();
    }

    [TestMethod]
    [ExpectedException(typeof(TransactionAbortedException))]
    public virtual async Task Post_ThrowsError_WhenNotMappedErrorType()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.InsertAsync(entity)).Throws(new TransactionAbortedException());

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        _ = await controller.Post(dto);

        // Assert
        Assert.Fail("Should have thrown an error that would be translated to 500 Internal Server Error");
    }

    #endregion POST Tests

    #region PUT Tests
    protected virtual IList<(int, Type?)> PutResponseStatusCodes =>
    [
        (StatusCodes.Status200OK, typeof(TDto)),
        (StatusCodes.Status400BadRequest, null),
        (StatusCodes.Status401Unauthorized, null),
        (StatusCodes.Status404NotFound, null),
        (StatusCodes.Status409Conflict, null),
    ];

    [TestMethod]
    public virtual void Put_DefinesResponseType()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");
        ControllerTestHelpers.ValidateResponseTypes<TController>(c => c.Put(It.IsAny<Guid>(), It.IsAny<TDto>()), PutResponseStatusCodes);
    }

    [TestMethod]
    public virtual async Task Put_ReturnsOk_WhenDtoIsValid()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(dto.Id!.Value, dto);

        // Assert
        var result = actionResult.GetOkResult<TDto>();
        Assert.IsNotNull(result);
        Assert.AreEqual(dto.Id, result.Id);
    }

    [TestMethod]
    public virtual async Task Put_ReturnsOk_AndAssignsUpdatedBy()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(It.Is<TEntity>(e => ReferenceEquals(entity, e) && e.UpdatedBy == HttpContextUserInfo.Username)))
                      .ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(dto.Id!.Value, dto);

        // Assert
        _ = actionResult.GetOkResult<TDto>();
    }

    [TestMethod]
    public virtual async Task Put_ReturnsNotFound_WhenNotFoundInRepo()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).Throws(new DBConcurrencyException());

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(dto.Id!.Value, dto);

        // Assert
        Assert.IsInstanceOfType<NotFoundResult>(actionResult);
    }

    [TestMethod]
    public virtual async Task Put_ReturnsNotFound_WhenNotFoundInRepoAlt()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).Throws(new DbUpdateConcurrencyException());

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(dto.Id!.Value, dto);

        // Assert
        Assert.IsInstanceOfType<NotFoundResult>(actionResult);
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenUriIdMissing()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(Guid.Empty, dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseDto.Id)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenDtoIdMissing()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];

        dto.Id = null; // <-- This should case a validation error

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseDto.Id)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenUriIdAndDtoIdMismatch()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];

        dto.Id = null; // <-- This should case a validation error

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(Guid.NewGuid(), dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseDto.Id)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsUnauthorized_WhenAuthTokenMissing()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.UpdatedBy = "unit-test@example.com";
        dto.RowVersion = [0x01, 0x02];

        dto.UpdatedBy = null; // <-- This should case a validation error

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper, () => null);

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        _ = actionResult.GetUnauthorized();
    }

    [TestMethod]
    public virtual async Task Put_ReturnsUnauthorized_WhenAuthTokenMissingUserId()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.UpdatedBy = "unit-test@example.com";
        dto.RowVersion = [0x01, 0x02];

        dto.UpdatedBy = null; // <-- This should case a validation error

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper,
                                       () => new ClaimsIdentity(
                                                                new List<Claim>
                                                                {
                                                                    // new(ServiceConstants.Authorization.UserId, HttpContextUserInfo.UserId.ToString()),
                                                                    new(ServiceConstants.Authorization.Username, HttpContextUserInfo.Username),
                                                                    new(ServiceConstants.Authorization.Client, Guid.NewGuid().ToString())
                                                                }));

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        _ = actionResult.GetUnauthorized();
    }

    [TestMethod]
    public virtual async Task Put_ReturnsUnauthorized_WhenAuthTokenMissingUsername()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.UpdatedBy = "unit-test@example.com";
        dto.RowVersion = [0x01, 0x02];

        dto.UpdatedBy = null; // <-- This should case a validation error

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper,
                                       () => new ClaimsIdentity(
                                                                new List<Claim>
                                                                {
                                                                    new(ServiceConstants.Authorization.UserId, HttpContextUserInfo.UserId.ToString()),
                                                                    // new(ServiceConstants.Authorization.Username, HttpContextUserInfo.Username),
                                                                    new(ServiceConstants.Authorization.Client, Guid.NewGuid().ToString())
                                                                }));

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        _ = actionResult.GetUnauthorized();
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenRowVersionIsMissing()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.UpdatedBy = "unit-test@example.com";
        dto.RowVersion = [0x01, 0x02];

        dto.RowVersion = null; // <-- This should case a validation error

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseDto.RowVersion)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenRowVersionIsEmpty()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.UpdatedBy = "unit-test@example.com";
        dto.RowVersion = [0x01, 0x02];

        dto.RowVersion = []; // <-- This should case a validation error

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseDto.RowVersion)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsConflict_WhenUniqueKeyConflict()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.UpdatedBy = "unit-test@example.com";
        dto.RowVersion = [0x01, 0x02];

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).Throws(new UniqueConstraintConflictException());

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        var message = actionResult.GetConflictResult<string>();
        Assert.IsNotNull(message);
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenForeignKeyMissing()
    {
        if (!SupportsPut)
            Assert.Inconclusive("PUT not supported by this controller.");

        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.UpdatedBy = "unit-test@example.com";
        dto.RowVersion = [0x01, 0x02];

        var (mockRepository, mockMapper) = GetMocks();

        mockRepository.Setup(repo => repo.UpdateAsync(entity)).Throws(new ForeignKeyMissingException());

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        var message = actionResult.GetBadRequestResultWithMessage();
        Assert.IsNotNull(message);
    }

    #endregion PUT Tests
}