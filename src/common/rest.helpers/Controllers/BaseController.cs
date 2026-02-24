using System.Data;
using Autofac;
using AutoMapper;
using EI.API.Service.Data.Helpers.Exceptions;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Auth;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Rest.Helpers.Controllers;

public interface IBaseControllerServices
{
    public ILifetimeScope LifetimeScope { get; set; }
    public IMapper Mapper { get; set; }
}

public abstract class BaseController<TDto, TEntity, TRepo>(IBaseControllerServices controllerServices)
    : BaseReadController<TDto, TEntity, TRepo>(controllerServices)
    where TDto : BaseDto
    where TEntity : IDatabaseEntity
    where TRepo : IReadWriteRepository<TEntity>
{
    public abstract Task<IActionResult> Post(TDto dto);

    public abstract Task<IActionResult> Put(Guid id, TDto dto);

    protected virtual bool TrySetUpdatedBy(TEntity entity)
    {
        if (HttpContext.TryGetEclipseUserInfo(out var userInfo))
        {
            entity.UpdatedBy = userInfo.Username;
            return true;
        }

        return false;
    }

    protected virtual async Task<IActionResult> InternalPostAsync(TDto dto)
    {
        dto.Id ??= Guid.NewGuid();

        if (!await ValidateDtoForPost(dto))
        {
            return BadRequest(ModelState);
        }

        var entity = _mapper.Map<TEntity>(dto);

        return await InternalPostAsync(dto, entity);
    }

    protected virtual async Task<IActionResult> InternalPostAsync(TDto dto, TEntity entity)
    {
        if (!TrySetUpdatedBy(entity)) return Unauthorized();

        TEntity inserted;
        try
        {
            inserted = await _lazyRepository.Value.InsertAsync(entity);
        }
        catch (Exception e)
        {
            return ConvertExceptionToActionResult(e);
        }

        var result = MapOne(inserted);
        return Created($"{GetType().Name[..^"Controller".Length]}/{result.Id}?api-version=1.0", result);
    }

    protected virtual async Task<IActionResult> InternalPutAsync(Guid id, TDto dto)
    {
        if (!await ValidateDtoForPut(id, dto))
        {
            return BadRequest(ModelState);
        }

        var entity = _mapper.Map<TEntity>(dto);

        return await InternalPutAsync(dto, entity);
    }

    protected virtual async Task<IActionResult> InternalPutAsync(TDto dto, TEntity entity)
    {
        if (!TrySetUpdatedBy(entity)) return Unauthorized();

        TEntity updated;
        try
        {
            updated = await _lazyRepository.Value.UpdateAsync(entity);
        }
        catch (Exception e)
        {
            return ConvertExceptionToActionResult(e);
        }

        var result = MapOne(updated);

        return Ok(result);
    }

    protected virtual Task<bool> ValidateDtoForPost(TDto dto)
        => Task.FromResult(ModelState.IsValid);

    protected virtual Task<bool> ValidateDtoForPut(Guid id, TDto dto)
    {
        if (id == Guid.Empty || dto.Id == null || dto.Id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(BaseDto.Id), $"{nameof(BaseDto.Id)} is required for updates");
        }

        if (id != dto.Id)
        {
            ModelState.AddModelError(nameof(BaseDto.Id), $"URI {nameof(BaseDto.Id)} must match Model {nameof(BaseDto.Id)}");
        }

        if (dto.RowVersion == null || dto.RowVersion.Length == 0)
        {
            ModelState.AddModelError(nameof(BaseDto.RowVersion), $"{nameof(BaseDto.RowVersion)} is required for updates");
        }

        return Task.FromResult(ModelState.IsValid);
    }

    protected virtual IActionResult ConvertExceptionToActionResult(Exception exception)
    {
        // !!! IMPORTANT !!!
        // Keep this in sync with BaseTranslationController::ConvertExceptionToActionResult
        return exception switch
        {
            DBConcurrencyException => NotFound(),
            DbUpdateConcurrencyException => NotFound(),
            ForeignKeyMissingException => BadRequest("This record has a dependent record that was not found"),
            PrimaryKeyConflictException => Conflict("A record with this key already exists"),
            UniqueConstraintConflictException => Conflict("This record conflicts with an existing uniqueness constraint"),
            _ => throw exception
        };
    }
}