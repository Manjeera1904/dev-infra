using Autofac;
using AutoMapper;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;

namespace EI.API.Service.Rest.Helpers.Controllers;

public abstract class BaseControllerWithoutHistory<TDto, TEntity, TRepo>(IBaseControllerServices controllerServices)
    : BaseController<TDto, TEntity, TRepo>(controllerServices)
    where TDto : BaseDto
    where TEntity : IDatabaseEntity
    where TRepo : IReadWriteRepository<TEntity>
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public override Task<IActionResult> GetHistory(
        Guid id,
        DateTime? fromDate = null,
        DateTime? toDate = null)
        => Task.FromResult<IActionResult>(NotFound());
}