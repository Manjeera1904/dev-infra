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

public abstract class BaseTranslationControllerTests<TRepo, TController, TEntity, TTranslation, TDto>
    : BaseReadTranslationControllerTests<TRepo, TController, TEntity, TTranslation, TDto>
    where TRepo : class, IRepositoryWithTranslation<TEntity, TTranslation>
    where TEntity : class, IDatabaseEntityWithTranslation<TTranslation>, new()
    where TDto : BaseTranslationDto, new()
    where TTranslation : class, IDatabaseTranslationsEntity, new()
    where TController : BaseTranslationController<TDto, TEntity, TTranslation, TRepo>
{
    #region POST Tests
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
        var (_, resultDto) = actionResult.GetCreatedResult<TDto>();
        Assert.AreEqual(entity.Id, resultDto.Id);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsConflict_WhenPkViolation()
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
        _ = actionResult.GetConflictResult<string>();
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
    public virtual async Task Post_ReturnsCreated_AndAssignsId()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        dto.Id = null;

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        var (_, resultDto) = actionResult.GetCreatedResult<TDto>();

        // the controller should have assigned an ID to the DTO prior to calling the Repo
        Assert.IsNotNull(resultDto.Id);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsCreated_AndAssignsUpdatedBy()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.InsertAsync(It.Is<TEntity>(e => ReferenceEquals(entity, e) && e.UpdatedBy == HttpContextUserInfo.Username && e.Translations.All(t => t.UpdatedBy == HttpContextUserInfo.Username))))
                      .ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        _ = actionResult.GetCreatedResult<TDto>();
    }

    [TestMethod]
    public virtual async Task Post_ReturnsBadRequest_WhenNotDefaultCultureCode()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        dto.CultureCode = "not-default"; // <-- This should cause a failure

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        var errors = actionResult.GetBadRequestResult();
        Assert.IsTrue(errors.ContainsKey(nameof(BaseTranslationDto.CultureCode)));
    }

    [TestMethod]
    public virtual async Task Post_ReturnsBadRequest_WhenCultureCodeIsMissing()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for insert
        dto.CultureCode = null; // <-- This should cause a failure

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        var errors = actionResult.GetBadRequestResult();
        Assert.IsTrue(errors.ContainsKey(nameof(BaseTranslationDto.CultureCode)));
    }

    [TestMethod]
    public virtual async Task Post_ReturnsBadRequest_WhenCultureCodeIsEmpty()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for insert
        dto.CultureCode = string.Empty; // <-- This should cause a failure

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.InsertAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto);

        // Assert
        var errors = actionResult.GetBadRequestResult();
        Assert.IsTrue(errors.ContainsKey(nameof(BaseTranslationDto.CultureCode)));
    }

    [TestMethod]
    public virtual async Task Post_ReturnsUnauthorized_WhenNoAuthorizationToken()
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
    public virtual async Task Post_ReturnsUnauthorized_AuthorizationTokenMissingUserId()
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
    public virtual async Task Post_ReturnsUnauthorized_AuthorizationTokenMissingUsername()
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
        ControllerTestHelpers.ValidateResponseTypes<TController>(c => c.Put(It.IsAny<Guid>(), It.IsAny<TDto>()), PutResponseStatusCodes);
    }


    [TestMethod]
    public virtual async Task Put_ReturnsOk_WhenDtoIsValid()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
    public virtual async Task Put_ReturnsOk_AndSetsUpdatedBy()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.UpdateAsync(It.Is<TEntity>(e => ReferenceEquals(entity, e) && e.UpdatedBy == HttpContextUserInfo.Username && e.Translations.All(t => t.UpdatedBy == HttpContextUserInfo.Username))))
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
    public virtual async Task Put_ReturnsConflict_WhenUniqueKeyConflict()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
    public virtual async Task Put_ReturnsNotFound_WhenNotFoundInRepo()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
    public virtual async Task Put_ReturnsBadRequest_WhenCultureCodeIsNull()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];
        dto.CultureCode = null;

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(dto.Id!.Value, dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.CultureCode)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenCultureCodeIsEmpty()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];
        dto.CultureCode = string.Empty;

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(dto.Id!.Value, dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.CultureCode)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenUriIdMissing()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(Guid.Empty, dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.Id)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenDtoIdMissing()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.Id)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenUriIdAndDtoIdMismatch()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.Id)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenRowVersionIsMissing()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.RowVersion)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsUnauthorized_WhenNoAuthorizationToken()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
    public virtual async Task Put_ReturnsUnauthorized_WhenAuthorizationTokenMissingUserId()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
    public virtual async Task Put_ReturnsUnauthorized_WhenAuthorizationTokenMissingUsername()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
    public virtual async Task Put_ReturnsBadRequest_WhenRowVersionIsEmpty()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.RowVersion)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenTranslationRowVersionIsMissing()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

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
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.RowVersion)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsBadRequest_WhenTranslationRowVersionIsEmpty()
    {
        // Arrange
        var (dto, entity) = BuildModels();

        // Required properties for update
        dto.RowVersion = [0x01, 0x02];
        dto.TranslationRowVersion = [0x03, 0x04];

        dto.TranslationRowVersion = []; // <-- This should case a validation error

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.UpdateAsync(entity)).ReturnsAsync(entity);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity)).Returns(dto);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto)).Returns(entity);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity.Id, dto);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.IsTrue(result.ContainsKey(nameof(BaseTranslationDto.TranslationRowVersion)));
    }

    #endregion PUT Tests
}