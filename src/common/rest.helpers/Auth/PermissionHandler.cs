using EI.API.Service.Data.Helpers;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;

namespace EI.API.Service.Rest.Helpers.Auth;

public class PermissionsHandler : AuthorizationHandler<PermissionsRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionsRequirement requirement)
    {
        var claim = context.User.FindFirst(c => c.Type == ServiceConstants.Authorization.Permissions);
        if (claim == null)
            return Task.CompletedTask;

        var permissions = JsonConvert.DeserializeObject<string[]>(claim.Value);

        if (permissions != null && permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
public class PermissionsRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionsRequirement(string permission)
    {
        Permission = permission;
    }
}
