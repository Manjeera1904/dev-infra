using Autofac;
using Autofac.Extras.Moq;
using AutoMapper;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Controllers;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using Newtonsoft.Json;

namespace EI.Data.TestHelpers.Controllers;

public abstract class BaseReadControllerEmptyTests<TRepo, TController, TEntity, TDto> : ControllerBase
    where TRepo : class, IReadRepository<TEntity>
    where TEntity : class, IDatabaseEntity, new()
    where TDto : BaseDto, new()
    where TController : BaseReadControllerEmpty<TDto, TEntity, TRepo>
{
    protected virtual bool SupportsPut => true;

    protected virtual TController GetController(
        Mock<TRepo> mockRepository,
        Mock<IMapper> mockMapper,
        Func<ClaimsIdentity?>? identityConfigurator = null)
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

        var controller = (TController)ctor.Invoke(new object[] { mockServices });

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

    protected virtual Mock<IBaseControllerServices> SetupBaseControllerServices(Mock<TRepo> mockRepository, Mock<IMapper> mockMapper) =>

        // By convention, there will be a constructor that takes a single Autofac Aggregate Services interface
        // where that interface extends IBaseControllerServices. Let's find that constructor and mock the controller up...
        SetupBaseControllerServices<IBaseControllerServices>(mockRepository, mockMapper);

    protected virtual Mock<TServices> SetupBaseControllerServices<TServices>(Mock<TRepo> mockRepository, Mock<IMapper> mockMapper)
        where TServices : class, IBaseControllerServices

    {
        // By convention, there will be a constructor that takes a single Autofac Aggregate Services interface
        // where that interface extends IBaseControllerServices. Let's find that constructor and mock the controller up...

        var mockServices = new Mock<TServices>();
        Assert.IsNotNull(mockServices);

        var context = AutoMock.GetLoose(builder =>
                                            {
                                                builder.RegisterInstance(mockRepository.Object).As<TRepo>();
                                            });

        mockServices.Setup(s => s.LifetimeScope).Returns(context.Container);
        mockServices.Setup(s => s.Mapper).Returns(mockMapper.Object);

        return mockServices;
    }

    protected virtual (Mock<TRepo> Repository, Mock<IMapper> Mapper) GetMocks()
    {
        return (
                   new Mock<TRepo>(MockBehavior.Strict),
                   new Mock<IMapper>(MockBehavior.Strict)
               );
    }

    protected virtual (TDto Dto, TEntity Entity) BuildModels()
    {
        var id = Guid.NewGuid();
        return (
                   new TDto { Id = id },
                   new TEntity { Id = id }
               );
    }

    #region HTTP Method Validations

    [TestMethod]
    public void ValidateControllerHttpMethodsVsAttributes()
    {
        if (!SupportsPut)
        {
            ControllerTestHelpers.TestHttpMethods<TController>(
                methodFilter: m => !m.Name.StartsWith("Put", StringComparison.OrdinalIgnoreCase)
            );
            return;
        }

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
}