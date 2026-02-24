using System.Diagnostics.CodeAnalysis;
using Autofac;
using AutoMapper;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;

namespace EI.API.Service.Rest.Helpers.Controllers;

public abstract class BaseReadControllerEmpty<TDto, TEntity, TRepo> : ControllerBase
    where TDto : BaseDto
    where TEntity : IDatabaseEntity
    where TRepo : IReadRepository<TEntity>
{
    protected readonly ILifetimeScope _lifetimeScope;
    protected readonly IMapper _mapper;

    protected readonly Lazy<TRepo> _lazyRepository;

    protected BaseReadControllerEmpty(IBaseControllerServices controllerServices)
    {
        _lifetimeScope = controllerServices.LifetimeScope;
        _mapper = controllerServices.Mapper;

        _lazyRepository = new Lazy<TRepo>(GetRepository);
    }

    protected virtual TRepo GetRepository()
    {
        if (TryGetClientId(out var clientId))
        {
            return _lifetimeScope.Resolve<TRepo>(new NamedParameter("clientId", clientId));
        }

        return _lifetimeScope.Resolve<TRepo>();
    }

    protected bool TryGetClientId(out Guid clientId)
    {
        if (HttpContext.Request.Headers.TryGetValue(ServiceConstants.HttpHeaders.ClientId, out var clientIdHeader))
        {
            var stringValue = clientIdHeader.FirstOrDefault();
            return Guid.TryParse(stringValue, out clientId);
        }

        clientId = Guid.Empty;
        return false;
    }
}
