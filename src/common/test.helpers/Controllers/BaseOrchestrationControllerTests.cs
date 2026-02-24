using Autofac;
using AutoMapper;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Rest.Helpers.Controllers;
using EI.Data.TestHelpers.Controllers.Helper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace EI.Data.TestHelpers.Controllers;

public abstract class BaseOrchestrationControllerTests<TController>
    where TController : ControllerBase
{

    protected virtual ClaimsIdentity DefaultIdentity =>
        new ClaimsIdentity(
            new List<Claim>
            {
            // Use local test values instead of HttpContextUserInfo
            new Claim(ServiceConstants.Authorization.UserId, Guid.NewGuid().ToString()),
            new Claim(ServiceConstants.Authorization.Username, "test.user@example.com"),
            new Claim(ServiceConstants.Authorization.Client, Guid.NewGuid().ToString())
            },
            authenticationType: "TestAuth");


    protected abstract TController ConstructController(
        IBaseControllerServices services,
        Func<ClaimsIdentity?>? identityConfigurator = null);

    protected TController GetController(
        Mock<IMapper> mapper,
        Func<ClaimsIdentity?>? identityConfigurator = null)
    {
        var services = new Mock<IBaseControllerServices>(MockBehavior.Strict);

        services.Setup(s => s.Mapper).Returns(mapper.Object);
        services.Setup(s => s.LifetimeScope).Returns(Mock.Of<ILifetimeScope>());

        var controller = ConstructController(services.Object, identityConfigurator);

        var identity = identityConfigurator?.Invoke() ?? DefaultIdentity;
        var httpContext = new DefaultHttpContext();

        if (identity != null)
        {
            httpContext.User = new ClaimsPrincipal(identity);
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }
}
