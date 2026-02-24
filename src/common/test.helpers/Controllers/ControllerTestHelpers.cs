using System.Linq.Expressions;
using System.Reflection;
using Asp.Versioning;
using EI.API.Service.Rest.Helpers.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace EI.Data.TestHelpers.Controllers;

public static class ControllerTestHelpers
{
    public static void TestHttpMethods<TApiController>(Func<MethodInfo, bool>? methodFilter = null)
    {
        var controllerMethods = typeof(TApiController).GetMethods();

        foreach (var method in controllerMethods)
        {
            if (methodFilter != null && !methodFilter(method))
                continue;

            var httpMethods = method
                .GetCustomAttributes(typeof(HttpMethodAttribute), true)
                .Cast<HttpMethodAttribute>()
                .ToList();

            if (httpMethods.Count > 0)
            {
                Assert.AreEqual(1, httpMethods.Count);

                var httpMethodsAllowed = httpMethods
                    .SelectMany(attr => attr.HttpMethods)
                    .ToList();

                Assert.AreEqual(
                    1,
                    httpMethodsAllowed.Count,
                    "Controller APIs should only support a single HTTP method"
                );

                var expectedPrefix = httpMethodsAllowed[0];

                // Special case for "Search" -- allow GET
                // (future: some API searches use POST in order to have a complex body specifying search criteria, just
                //  no current such cases in the Eclipse projects being tested):
                if (method.Name.StartsWith("SEARCH", StringComparison.InvariantCultureIgnoreCase))
                {
                    Assert.AreEqual("GET", expectedPrefix, ignoreCase: true, "Search methods must use the GET HTTP Method");
                }
                else
                {
                    Assert.IsTrue(method.Name.StartsWith(expectedPrefix, StringComparison.InvariantCultureIgnoreCase),
                                  $"Method {typeof(TApiController).Name}::{method.Name} does not match HTTP method specified: {expectedPrefix}");
                }

                // Also require the method to specify an API version
                var apiVersionAttr = method.GetCustomAttributes(typeof(ApiVersionAttribute), true);

                var methodMapToApiVersionAttrs = method.GetCustomAttributes(typeof(MapToApiVersionAttribute), true);

                var controllerApiVersionAttrs = typeof(TApiController).GetCustomAttributes(typeof(ApiVersionAttribute), true);

                var hasApiVersion = apiVersionAttr.Any() || methodMapToApiVersionAttrs.Any() || controllerApiVersionAttrs.Any();

                Assert.IsTrue(hasApiVersion, $"Method {typeof(TApiController).Name}::{method.Name} does not specify an API Version");
            }
        }
    }

    public static void TestResponseCodes<TApiController>()
    {
        // Examples:
        //   [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<UserClientRoleViewV1>))]
        //   [ProducesResponseType(StatusCodes.Status204NoContent)]

        var controllerMethods = typeof(TApiController).GetMethods();
        foreach (var method in controllerMethods)
        {
            var httpMethods = method.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>().SelectMany(m => m.HttpMethods).ToList();
            if (httpMethods.Any() && (
                                         method.ReturnType == typeof(IActionResult) ||
                                         method.ReturnType == typeof(Task<IActionResult>)
                                     ))
            {


                var responseCodes = method.GetCustomAttributes(typeof(ProducesResponseTypeAttribute), true).Cast<ProducesResponseTypeAttribute>().ToList();
                Assert.IsTrue(responseCodes.Count > 0, $"Method {typeof(TApiController).Name}::{method.Name} does not specify any response codes");
            }
        }
    }

    public static void ValidateResponseTypes<TController>(Expression<Func<TController, Task<IActionResult>>> methodExpression, IList<(int StatusCode, Type? ReturnType)> expectedResponseStatusCodes)
    {
        var methodCall = methodExpression.Body as MethodCallExpression;
        Assert.IsNotNull(methodCall);

        var method = methodCall.Method;
        if (method.IsAbstract)
        {
            var controllerType = typeof(TController);
            method = controllerType.GetMethod(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray());
        }

        Assert.IsNotNull(method);
        Assert.IsFalse(method.IsAbstract);

        var actualResponseCodes = method.GetCustomAttributes(typeof(ProducesResponseTypeAttribute), true).Cast<ProducesResponseTypeAttribute>().ToList();

        Assert.AreEqual(expectedResponseStatusCodes.Count, actualResponseCodes.Count);

        foreach (var expectedResponseStatusCode in expectedResponseStatusCodes)
        {
            var matchingAttr = actualResponseCodes.FirstOrDefault(attr => attr.StatusCode == expectedResponseStatusCode.StatusCode);
            Assert.IsNotNull(matchingAttr, $"Method {method.Name} does not specify a response code of {expectedResponseStatusCode.StatusCode}");
            Assert.AreEqual(expectedResponseStatusCode.ReturnType ?? typeof(void), matchingAttr.Type);
        }
    }

    public static void ValidateActionParameters<TApiController>()
    {
        var controllerMethods = typeof(TApiController).GetMethods();
        foreach (var method in controllerMethods)
        {
            var httpMethods = method.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>().ToList();
            if (httpMethods.Count > 0)
            {
                Assert.AreEqual(1, httpMethods.Count);

                var urlParameters = new List<(string Name, Type Type)>();

                if (!string.IsNullOrWhiteSpace(httpMethods[0].Template))
                {
                    // Need to parse the template to extract the parameter details:
                    // ex: [HttpGet("name/{username}")]
                    //     [HttpGet("Tenant/{tenantId:guid}")]
                    //     [HttpGet("Client/{clientId:guid}/DataSourceKey/{dataSourceKey}")]

                    var templateParts = httpMethods[0].Template!.Split('/').Where(part => part.StartsWith('{')).ToList();
                    foreach (var templatePart in templateParts)
                    {
                        var trimmed = templatePart.Trim('{', '}');
                        var split = trimmed.Split(':');
                        if (split.Length == 1)
                        {
                            urlParameters.Add((split[0], typeof(string)));
                        }
                        else
                        {
                            var type = split[1] switch
                            {
                                "guid" => typeof(Guid),
                                "int" => typeof(int),
                                _ => throw new NotImplementedException($"Unknown URL parameter type: {split[1]}")
                            };

                            urlParameters.Add((split[0], type));
                        }
                    }
                }

                var parameterInfos = method.GetParameters();
                foreach (var parameterInfo in parameterInfos)
                {
                    var fromUrl = urlParameters.Where(p => p.Name == parameterInfo.Name!).ToList();
                    Assert.IsTrue(fromUrl.Count <= 1);
                    if (fromUrl.Count == 1)
                    {
                        // Can't be both a URL path param, and a Query Parameter:
                        var queryAttrs = parameterInfo.GetCustomAttributes(typeof(FromQueryAttribute), true).Cast<FromQueryAttribute>().ToList();
                        Assert.AreEqual(0, queryAttrs.Count, $"Action {typeof(TApiController).Name}::{method.Name} parameter {parameterInfo.Name} can not be specified as both a URL Path part and a Query Parameter");
                    }
                }
            }
        }
    }
}
