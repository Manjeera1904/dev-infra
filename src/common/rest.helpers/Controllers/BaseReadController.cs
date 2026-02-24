using System.Diagnostics.CodeAnalysis;
using Autofac;
using AutoMapper;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;

namespace EI.API.Service.Rest.Helpers.Controllers;

public abstract class BaseReadController<TDto, TEntity, TRepo>(IBaseControllerServices controllerServices) : BaseReadControllerEmpty<TDto, TEntity, TRepo>(controllerServices)
    where TDto : BaseDto
    where TEntity : IDatabaseEntity
    where TRepo : IReadRepository<TEntity>
{
    public abstract Task<IActionResult> Get();

    public abstract Task<IActionResult> Get(Guid id);

    public abstract Task<IActionResult> GetHistory(Guid id, DateTime? fromDate = null, DateTime? toDate = null);

    protected virtual async Task<IActionResult> InternalGetAllAsync()
    {
        var entities = await _lazyRepository.Value.GetAllAsync();
        if (entities.Count == 0)
        {
            return NoContent();
        }

        var dtos = MapAll(entities);
        return Ok(dtos);
    }

    protected virtual async Task<IActionResult> InternalGetAsync(Guid id)
    {
        var entity = await _lazyRepository.Value.GetAsync(id);
        var dto = MapOne(entity);
        return dto == null ? NotFound() : Ok(dto);
    }

    protected virtual async Task<IActionResult> InternalGetHistoryAsync(Guid id, DateTime? fromDate, DateTime? toDate)
    {
        var entities = await _lazyRepository.Value.GetHistoryAsync(id, fromDate, toDate);
        if (entities.Count == 0)
        {
            return NoContent();
        }

        var dtos = entities.Select(e => new EntityHistoryDto<TDto>
        {
            Entity = MapOne(e.Entity),
            ValidFrom = e.ValidFrom,
            ValidTo = e.ValidTo,
        });
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
