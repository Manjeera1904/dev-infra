using Microsoft.AspNetCore.Mvc;

namespace EI.Data.TestHelpers.Controllers.Helper;

public static class ControllerTestHelpers
{
    public static TDto GetOkResult<TDto>(this IActionResult? actionResult)
        where TDto : class
    {
        Assert.IsInstanceOfType<OkObjectResult>(actionResult);
        var okResult = actionResult as OkObjectResult;
        Assert.IsNotNull(okResult);

        var result = okResult.Value as TDto;
        Assert.IsNotNull(result);

        return result;
    }

    public static IList<TDto> GetOkList<TDto>(this IActionResult? actionResult)
    {
        Assert.IsInstanceOfType<OkObjectResult>(actionResult);
        var okResult = actionResult as OkObjectResult;
        Assert.IsNotNull(okResult);

        var result = (okResult.Value as IEnumerable<TDto>)?.ToList();
        Assert.IsNotNull(result);

        Assert.AreNotEqual(0, result.Count, "Should not return OK with empty, should return NoContent");

        return result;
    }

    public static NoContentResult GetNoContent(this IActionResult? actionResult)
    {
        Assert.IsInstanceOfType<NoContentResult>(actionResult);
        var ncResult = actionResult as NoContentResult;

        Assert.IsNotNull(ncResult);
        return ncResult;
    }

    public static NotFoundResult GetNotFound(this IActionResult? actionResult)
    {
        Assert.IsInstanceOfType<NotFoundResult>(actionResult);
        var nfResult = actionResult as NotFoundResult;
        Assert.IsNotNull(nfResult);
        return nfResult;
    }

    public static (string Uri, TDto Result) GetCreatedResult<TDto>(this IActionResult? actionResult)
        where TDto : class
    {
        Assert.IsInstanceOfType<CreatedResult>(actionResult);
        var createdResult = actionResult as CreatedResult;
        Assert.IsNotNull(createdResult);

        var uri = createdResult.Location;
        Assert.IsNotNull(uri);
        Assert.IsFalse(string.IsNullOrWhiteSpace(uri));

        var result = createdResult.Value as TDto;
        Assert.IsNotNull(result);

        return (uri, result);
    }

    public static Dictionary<string, object> GetBadRequestResult(this IActionResult? actionResult)
    {
        Assert.IsInstanceOfType<BadRequestObjectResult>(actionResult);
        var badRequest = actionResult as BadRequestObjectResult;
        Assert.IsNotNull(badRequest);

        var reasons = badRequest.Value as IEnumerable<KeyValuePair<string, object>>;
        Assert.IsNotNull(reasons);
        var errors = reasons.ToList();

        return new Dictionary<string, object>(errors);
    }

    public static string GetBadRequestResultWithMessage(this IActionResult? actionResult)
    {
        Assert.IsInstanceOfType<BadRequestObjectResult>(actionResult);
        var badRequest = actionResult as BadRequestObjectResult;
        Assert.IsNotNull(badRequest);

        var reasons = badRequest.Value as string;
        Assert.IsNotNull(reasons);

        return reasons;
    }

    public static IList<TDto> GetConflictResult<TDto>(this IActionResult? actionResult)
    {
        Assert.IsInstanceOfType<ConflictObjectResult>(actionResult);
        var conflict = actionResult as ConflictObjectResult;
        Assert.IsNotNull(conflict);

        if (conflict.Value is IEnumerable<TDto> dtos)
        {
            var conflicts = dtos.ToList();
            return conflicts;
        }

        if (conflict.Value is TDto single)
        {
            return new List<TDto> { single };
        }

        Assert.Fail("Could not determine result");
        throw new Exception("Should fail ^^^");
    }

    public static UnauthorizedResult GetUnauthorized(this IActionResult? actionResult)
    {
        Assert.IsInstanceOfType<UnauthorizedResult>(actionResult);
        var unauthorized = actionResult as UnauthorizedResult;
        Assert.IsNotNull(unauthorized);
        return unauthorized;
    }

}
