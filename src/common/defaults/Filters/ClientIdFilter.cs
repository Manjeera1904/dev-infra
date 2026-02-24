using EI.API.Service.Data.Helpers;
using EI.API.ServiceDefaults.Filters.Attributes;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EI.API.ServiceDefaults.Filters;

public class ClientIdFilter : IOperationFilter
{
    public static readonly string ClientIdHeader = ServiceConstants.HttpHeaders.ClientId;
    private static readonly bool IsRequired = true;
    private static readonly string Description = "Specifies the ID of the Client Scope the request is for";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasSkipAttribute = context.MethodInfo
            .GetCustomAttributes(typeof(SkipClientIdHeaderAttribute), inherit: true)
            .Any();

        if (hasSkipAttribute)
            return;

        var foundClientId = operation.Parameters.SingleOrDefault(x => x.Name.Equals(ClientIdHeader) && x.In.Equals(ParameterLocation.Header));
        if (foundClientId != null)
        {
            foundClientId.Required = IsRequired;
            foundClientId.Description = Description;
        }
        else
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = ClientIdHeader,
                In = ParameterLocation.Header,
                Description = Description,
                Required = IsRequired,
            });
        }
    }
}
