using System.Diagnostics.CodeAnalysis;
using Autofac;
using AutoMapper;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;

namespace EI.API.Service.Rest.Helpers.Controllers;

public abstract class BaseReadTranslationController<TDto, TEntity, TTranslation, TRepo> : ControllerBase
    where TDto : BaseDto
    where TEntity : IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : class, IDatabaseTranslationsEntity
    where TRepo : IReadRepositoryWithTranslation<TEntity, TTranslation>
{
    protected readonly ILifetimeScope _lifetimeScope;
    protected readonly IMapper _mapper;

    protected readonly Lazy<TRepo> _lazyRepository;

    protected BaseReadTranslationController(IBaseControllerServices controllerServices)
    {
        _lifetimeScope = controllerServices.LifetimeScope;
        _mapper = controllerServices.Mapper;

        _lazyRepository = new Lazy<TRepo>(GetRepository);
    }

    protected virtual TRepo GetRepository()
    {
        if (HttpContext.Request.Headers.TryGetValue(ServiceConstants.HttpHeaders.ClientId, out var clientIdHeader))
        {
            var stringValue = clientIdHeader.FirstOrDefault();
            if (Guid.TryParse(stringValue, out var clientId))
            {
                return _lifetimeScope.Resolve<TRepo>(new NamedParameter("clientId", clientId));
            }
        }

        return _lifetimeScope.Resolve<TRepo>();
    }

    public abstract Task<IActionResult> Get(string? cultureCode = null);
    // => await InternalGetAsync(cultureCode);
    public abstract Task<IActionResult> Get(Guid id, string? cultureCode = null);
    // => await InternalGetAsync(id, cultureCode);

    public abstract Task<IActionResult> GetHistory(Guid id, string? cultureCode = null, DateTime? fromDate = null, DateTime? toDate = null);
    // => await InternalGetHistoryAsync(id, cultureCode, fromDate, toDate);

    protected virtual async Task<IActionResult> InternalGetAsync(string? cultureCode = null)
    {
        var entities = await _lazyRepository.Value.GetAllAsync(cultureCode ?? ServiceConstants.CultureCode.Default);

        if (entities.Count == 0)
        {
            return NoContent();
        }

        var dtos = MapAll(entities);
        return Ok(dtos);
    }

    protected virtual async Task<IActionResult> InternalGetAsync(Guid id, string? cultureCode = null)
    {
        var entity = await _lazyRepository.Value.GetAsync(id, cultureCode ?? ServiceConstants.CultureCode.Default);
        var dto = MapOne(entity);
        return dto == null ? NotFound() : Ok(dto);
    }

    protected virtual async Task<IActionResult> InternalGetHistoryAsync(Guid id, string? cultureCode = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var entities = await _lazyRepository.Value.GetHistoryAsync(id, cultureCode ?? ServiceConstants.CultureCode.Default, fromDate, toDate);

        if (entities.Count == 0)
        {
            return NoContent();
        }

        // We aren't mapping to DTOs here since the history really makes it nearly
        // impossible to cleanly do so, so instead we just return the DB models...

        var dtos = entities.Select(e => new
        {
            Entity = new EntityHistoryDto<TEntity>
            {
                Entity = e.Entity.Entity,
                ValidFrom = e.Entity.ValidFrom,
                ValidTo = e.Entity.ValidTo,
            },
            Translations = e.Translations.Select(t => new EntityHistoryDto<TTranslation>
            {
                Entity = t.Entity,
                ValidFrom = t.ValidFrom,
                ValidTo = t.ValidTo,
            }).ToList(),
        }).ToList();

        return Ok(dtos);
    }

    protected virtual async Task<IActionResult> GetOne(Func<TRepo, Task<TEntity?>> action)
    {
        var entity = await action(_lazyRepository.Value);
        return entity == null ? NotFound() : Ok(MapOne(entity));
    }

    protected virtual async Task<IActionResult> GetMany(Func<TRepo, Task<IEnumerable<TEntity>>> action)
    {
        var entities = (await action(_lazyRepository.Value)).ToList();

        if (entities.Count == 0)
        {
            return NoContent();
        }

        var dtos = MapAll(entities);
        return Ok(dtos);
    }

    [return: NotNullIfNotNull(nameof(entity))]
    protected virtual TDto? MapOne(TEntity? entity)
    {
        return _mapper.Map<TDto>(entity);
    }

    protected virtual IEnumerable<TDto> MapAll(IEnumerable<TEntity> entities)
    {
        return entities.Select(e => MapOne(e));
    }
}