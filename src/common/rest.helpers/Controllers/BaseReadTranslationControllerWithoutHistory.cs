using Autofac;
using AutoMapper;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;

namespace EI.API.Service.Rest.Helpers.Controllers;

public abstract class BaseReadTranslationControllerWithoutHistory<TDto, TEntity, TTranslation, TRepo>(IBaseControllerServices controllerServices)
    : BaseReadTranslationController<TDto, TEntity, TTranslation, TRepo>(controllerServices)
    where TDto : BaseTranslationDto
    where TEntity : IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : class, IDatabaseTranslationsEntity
    where TRepo : IReadRepositoryWithTranslation<TEntity, TTranslation>
{
    public override Task<IActionResult> GetHistory(Guid id, string? cultureCode = null, DateTime? fromDate = null, DateTime? toDate = null)
        => Task.FromResult<IActionResult>(NotFound());
}