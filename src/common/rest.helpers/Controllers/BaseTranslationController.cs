using System.Data;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Data.Helpers.Exceptions;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Auth;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Rest.Helpers.Controllers;

public abstract class BaseTranslationController<TDto, TEntity, TTranslation, TRepo>(IBaseControllerServices controllerServices)
    : BaseReadTranslationController<TDto, TEntity, TTranslation, TRepo>(controllerServices)
    where TDto : BaseTranslationDto
    where TEntity : IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : class, IDatabaseTranslationsEntity
    where TRepo : IRepositoryWithTranslation<TEntity, TTranslation>
{
    public abstract Task<IActionResult> Post(TDto dto);
    // => await InternalPostAsync(dto);

    public abstract Task<IActionResult> Put(Guid id, TDto dto);
    // => await InternalPutAsync(id, dto);

    protected virtual bool TrySetUpdatedBy(TEntity entity)
    {
        if (HttpContext.TryGetEclipseUserInfo(out var userInfo))
        {
            entity.UpdatedBy = userInfo.Username;
            entity.Translations.ForEach(t => t.UpdatedBy = userInfo.Username);
            return true;
        }
        return false;
    }

    protected virtual async Task<IActionResult> InternalPostAsync(TDto dto)
    {
        if (!ValidateDtoForPost(dto))
        {
            return BadRequest(ModelState);
        }

        dto.Id ??= Guid.NewGuid();

        var db = _mapper.Map<TEntity>(dto);

        if (!TrySetUpdatedBy(db)) return Unauthorized();

        TEntity updated;
        try
        {
            updated = await _lazyRepository.Value.InsertAsync(db);
        }
        catch (Exception e)
        {
            return ConvertExceptionToActionResult(e);
        }

        var result = MapOne(updated);
        return Created($"{GetType().Name[..^"Controller".Length]}/{result.Id}?cultureCode={result.CultureCode}&api-version=1.0", result);
    }

    protected virtual async Task<IActionResult> InternalPutAsync(Guid id, TDto dto)
    {
        if (!ValidateDtoForPut(id, dto))
        {
            return BadRequest(ModelState);
        }

        var db = _mapper.Map<TEntity>(dto);

        if (!TrySetUpdatedBy(db)) return Unauthorized();

        TEntity updated;
        try
        {
            updated = await _lazyRepository.Value.UpdateAsync(db);
        }
        catch (Exception e)
        {
            return ConvertExceptionToActionResult(e);
        }

        var result = MapOne(updated);
        return Ok(result);
    }

    protected virtual bool ValidateDtoForPost(TDto dto)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(dto.CultureCode, ServiceConstants.CultureCode.Default))
        {
            ModelState.AddModelError(nameof(BaseTranslationDto.CultureCode), $"{nameof(BaseTranslationDto.CultureCode)} must be the default (\"{ServiceConstants.CultureCode.Default}\") upon record Creation");
        }

        if (string.IsNullOrWhiteSpace(dto.CultureCode))
        {
            ModelState.AddModelError(nameof(BaseTranslationDto.CultureCode), $"{nameof(BaseTranslationDto.CultureCode)} is required for updates");
        }

        return ModelState.IsValid;
    }

    protected virtual bool ValidateDtoForPut(Guid id, TDto dto)
    {
        if (id == Guid.Empty || dto.Id == null || dto.Id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(BaseTranslationDto.Id), $"{nameof(BaseTranslationDto.Id)} is required for updates");
        }

        if (id != dto.Id)
        {
            ModelState.AddModelError(nameof(BaseTranslationDto.Id), $"URI {nameof(BaseTranslationDto.Id)} must match Model {nameof(BaseTranslationDto.Id)}");
        }

        if (string.IsNullOrWhiteSpace(dto.CultureCode))
        {
            ModelState.AddModelError(nameof(BaseTranslationDto.CultureCode), $"{nameof(BaseTranslationDto.CultureCode)} is required for updates");
        }

        if (dto.RowVersion == null || dto.RowVersion.Length == 0)
        {
            ModelState.AddModelError(nameof(BaseTranslationDto.RowVersion), $"{nameof(BaseTranslationDto.RowVersion)} is required for updates");
        }

        if (dto.TranslationRowVersion == null || dto.TranslationRowVersion.Length == 0)
        {
            ModelState.AddModelError(nameof(BaseTranslationDto.TranslationRowVersion), $"{nameof(BaseTranslationDto.TranslationRowVersion)} is required for updates");
        }

        return ModelState.IsValid;
    }

    protected virtual IActionResult ConvertExceptionToActionResult(Exception exception)
    {
        // !!! IMPORTANT !!!
        // Keep this in sync with BaseController::ConvertExceptionToActionResult
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