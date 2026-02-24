using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
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
using Newtonsoft.Json;

namespace EI.Data.TestHelpers.Controllers;

public abstract class BaseReadTranslationControllerTests<TRepo, TController, TEntity, TTranslation, TDto> //: ControllerBase
    where TRepo : class, IReadRepositoryWithTranslation<TEntity, TTranslation>
    where TEntity : class, IDatabaseEntityWithTranslation<TTranslation>, new()
    where TDto : BaseTranslationDto, new()
    where TTranslation : class, IDatabaseTranslationsEntity, new()
    where TController : BaseReadTranslationController<TDto, TEntity, TTranslation, TRepo>
{
    protected TController GetController(Mock<TRepo> mockRepository, Mock<IMapper> mockMapper, Func<ClaimsIdentity?>? identityConfigurator = null)
    {
        var mockServices = SetupBaseControllerServices(mockRepository, mockMapper);
        var services = mockServices.Object;

        return ConstructController(services, identityConfigurator);
    }

    protected virtual (Guid UserId, string Username, Guid? ClientId, ISet<Guid> ClientIds) HttpContextUserInfo { get; set; }
        = new(
              Guid.NewGuid(),
              $"unit-test-user@{Guid.NewGuid():N}.example.com",
              Guid.NewGuid(),
              new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }
              );

    protected virtual TController ConstructController(IBaseControllerServices mockServices, Func<ClaimsIdentity?>? identityConfigurator = null)
    {
        var ctor = typeof(TController).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                      .FirstOrDefault(c => c.GetParameters().Length == 1 && typeof(IBaseControllerServices).IsAssignableFrom(c.GetParameters()[0].ParameterType));

        Assert.IsNotNull(ctor, "If the controller being tested has a non-standard constructor, this method must be overridden");

        var controller = (TController)ctor.Invoke([mockServices]);

        // Set up HttpContext, with a ClientId:
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[ServiceConstants.HttpHeaders.ClientId] = Guid.NewGuid().ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        IIdentity? identity;
        if (identityConfigurator != null)
        {
            identity = identityConfigurator();
        }
        else
        {
            identity = new ClaimsIdentity(
                                          new List<Claim>
                                          {
                                              new(ServiceConstants.Authorization.UserId, HttpContextUserInfo.UserId.ToString()),
                                              new(ServiceConstants.Authorization.Username, HttpContextUserInfo.Username),
                                              new(ServiceConstants.Authorization.Client, HttpContextUserInfo.ClientId?.ToString() ?? string.Empty),
                                              new(ServiceConstants.Authorization.ClientList, JsonConvert.SerializeObject(HttpContextUserInfo.ClientIds.Select(x => x.ToString().ToUpper())?.ToArray())),
                                          });
        }

        if (identity != null)
        {
            httpContext.User = new ClaimsPrincipal(identity);
            return controller;
        }

        return controller;
    }

    protected virtual Mock<IBaseControllerServices> SetupBaseControllerServices(Mock<TRepo> mockRepository, Mock<IMapper> mockMapper)
    {
        // By convention, there will be a constructor that takes a single Autofac Aggregate Services interface
        // where that interface extends IBaseControllerServices. Let's find that constructor and mock the controller up...

        var mockServices = new Mock<IBaseControllerServices>();
        Assert.IsNotNull(mockServices);

        var context = AutoMock.GetLoose(builder =>
                                            {
                                                builder.RegisterInstance(mockRepository.Object).As<TRepo>();
                                            });

        mockServices.Setup(s => s.LifetimeScope).Returns(context.Container);
        mockServices.Setup(s => s.Mapper).Returns(mockMapper.Object);

        return mockServices;
    }

    protected virtual (TDto Dto, TEntity Entity) BuildModels()
    {
        var id = Guid.NewGuid();
        return (
                   new TDto { Id = id, CultureCode = ServiceConstants.CultureCode.Default, UpdatedBy = "unit-test@example.com", TranslationUpdatedBy = "unit-test@example.com" },
                   new TEntity { Id = id, Translations = [new TTranslation { Id = id, CultureCode = ServiceConstants.CultureCode.Default }] }
               );
    }

    protected virtual (Mock<TRepo> Repository, Mock<IMapper> Mapper) GetMocks()
    {
        return (
                   new Mock<TRepo>(MockBehavior.Strict),
                   new Mock<IMapper>(MockBehavior.Strict)
               );
    }
    #region HTTP Method Validations

    [TestMethod]
    public void ValidateControllerHttpMethodsVsAttributes()
    {
        ControllerTestHelpers.TestHttpMethods<TController>();
    }

    [TestMethod]
    public void ValidateControllerResponseCodes()
    {
        ControllerTestHelpers.TestResponseCodes<TController>();
    }

    #endregion HTTP Method Validations

    #region Query vs URL parameters

    [TestMethod]
    public void ValidateActionParamterLocationsSpecified()
    {
        ControllerTestHelpers.ValidateActionParameters<TController>();
    }
    #endregion Query vs URL parameters


    #region GetAll Tests
    [TestMethod]
    public virtual async Task GetAll_ReturnsDtos_WithCultureCode()
    {
        // Arrange
        const string fakeCultureCode = "ABC";
        var (dto1, entity1) = BuildModels();
        var (dto2, entity2) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.GetAllAsync(fakeCultureCode)).ReturnsAsync([entity1, entity2]);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity1)).Returns(dto1);
        mockMapper.Setup(mapper => mapper.Map<TDto>(entity2)).Returns(dto2);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        // - Ensure that the `FakeCultureCode` is passed to the repository as expected,
        //   otherwise the `Strict` behavior will cause Moq to throw an exception.
        var actionResult = await controller.Get(fakeCultureCode);

        // Assert
        var result = actionResult.GetOkList<TDto>();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(dto1.Id, result[0].Id);
        Assert.AreEqual(dto2.Id, result[1].Id);
    }

    [TestMethod]
    public virtual async Task GetAll_ReturnsDtos_WithOutCultureCode()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();
        var (dto2, entity2) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.GetAllAsync(ServiceConstants.CultureCode.Default)).ReturnsAsync([entity1, entity2]);

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
        mockRepository.Setup(repo => repo.GetAllAsync(ServiceConstants.CultureCode.Default)).ReturnsAsync([]);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Get();

        // Assert
        _ = actionResult.GetNoContent();
    }
    #endregion GetAll Tests

    #region GetById Tests
    [TestMethod]
    public virtual async Task GetById_ReturnsResult_WithCultureCode()
    {
        // Arrange
        const string fakeCultureCode = "ABC";
        var (dto1, entity1) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.GetAsync(entity1.Id, fakeCultureCode)).ReturnsAsync(entity1);

        mockMapper.Setup(mapper => mapper.Map<TDto>(entity1)).Returns(dto1);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Get(entity1.Id, fakeCultureCode);

        // Assert
        var result = actionResult.GetOkResult<TDto>();
        Assert.AreEqual(dto1.Id, result.Id);
    }

    [TestMethod]
    public virtual async Task GetById_ReturnsResult_WithOutCultureCode()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.GetAsync(entity1.Id, ServiceConstants.CultureCode.Default)).ReturnsAsync(entity1);

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
        mockRepository.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((TEntity)null!);

        mockMapper.Setup(mapper => mapper.Map<TDto>(null)).Returns((TDto?)null!);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Get(Guid.NewGuid());

        // Assert
        _ = actionResult.GetNotFound();
    }
    #endregion GetById Tests

    #region GetHistory Tests
    protected virtual IList<(int, Type?)> GetHistoryResponseStatusCodes =>
    [
        (StatusCodes.Status200OK, typeof(IEnumerable<EntityWithTranslationHistoryDto<TEntity, TTranslation>>)),
        (StatusCodes.Status204NoContent, null),
        (StatusCodes.Status401Unauthorized, null),
    ];

    [TestMethod]
    public virtual void GetHistory_DefinesResponseType()
    {
        ControllerTestHelpers.ValidateResponseTypes<TController>(c => c.GetHistory(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), GetHistoryResponseStatusCodes);
    }


    [TestMethod]
    public virtual async Task GetHistory_ReturnsEmpty_WhenNoneFound()
    {
        // Arrange
        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>())).ReturnsAsync([]);

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

        var (mockRepository, mockMapper) = GetMocks();
        mockRepository.Setup(repo => repo.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>())).ReturnsAsync([
            (
                new EntityHistory<TEntity> { Entity = entity1, ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow },
                new List<EntityHistory<TTranslation>> { new() { Entity = entity1.Translations[0], ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow } }
            ),
            (
                new EntityHistory<TEntity> { Entity = entity2, ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow },
                new List<EntityHistory<TTranslation>> { new() { Entity = entity2.Translations[0], ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow } }
            ),
        ]);

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