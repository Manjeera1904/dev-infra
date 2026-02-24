using AutoMapper;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;

namespace EI.API.Service.Rest.Helpers.Controllers;

public abstract class BaseControllerWithDateRange<TDto, TEntity, TRepo>(IBaseControllerServices controllerServices)
    : BaseController<TDto, TEntity, TRepo>(controllerServices)
    where TDto : BaseDto
    where TEntity : IDatabaseEntity, IDateRange
    where TRepo : IReadWriteRepositoryWithDateRange<TEntity>
{
    protected override async Task<IActionResult> InternalPostAsync(TDto dto, TEntity entity)
    {
        var conflicts = (await _lazyRepository.Value.GetConflictingDateRanges(entity)).ToList();
        if (conflicts.Any())
        {
            return Conflict(MapAll(conflicts));
        }

        if (!ValidateEntityDates(entity))
        {
            return BadRequest(ModelState);
        }

        return await base.InternalPostAsync(dto, entity);
    }

    protected override async Task<IActionResult> InternalPutAsync(TDto dto, TEntity entity)
    {
        var conflicts = (await _lazyRepository.Value.GetConflictingDateRanges(entity)).ToList();
        if (conflicts.Any())
        {
            return Conflict(MapAll(conflicts));
        }

        if (!ValidateEntityDates(entity))
        {
            return BadRequest(ModelState);
        }

        return await base.InternalPutAsync(dto, entity);
    }

    protected virtual bool ValidateEntityDates(TEntity entity)
    {
        if (entity.StartDate == default)
        {
            ModelState.AddModelError(nameof(entity.StartDate), $"{nameof(entity.StartDate)} cannot be {DateOnly.MinValue}");
        }

        if (entity.EndDate == default)
        {
            ModelState.AddModelError(nameof(entity.EndDate), $"{nameof(entity.EndDate)} cannot be {DateOnly.MinValue}");
        }

        return ModelState.IsValid;
    }
}