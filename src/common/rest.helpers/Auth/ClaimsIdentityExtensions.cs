using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using EI.API.Service.Data.Helpers;

namespace EI.API.Service.Rest.Helpers.Auth;

public static class ClaimsIdentityExtensions
{
    public static bool TryGetEclipseUserInfo(this HttpContext currentContext, [NotNullWhen(true)] out EclipseUserInfo? userInfo)
    {
        if (currentContext.User.Identity is ClaimsIdentity principal)
        {
            var claimUserid = principal.Claims.SingleOrDefault(c => c.Type == ServiceConstants.Authorization.UserId)?.Value;
            var claimAppid = principal.Claims.SingleOrDefault(c => c.Type == ServiceConstants.Authorization.AppId)?.Value;
            var claimUsername = principal.Claims.SingleOrDefault(c => c.Type == ServiceConstants.Authorization.Username)?.Value;
            var clientIds = principal.Claims.SingleOrDefault(c => c.Type == ServiceConstants.Authorization.ClientList)?.Value;
            var permissionIds = principal.Claims.SingleOrDefault(c => c.Type == ServiceConstants.Authorization.Permissions)?.Value;

            Guid? userId = null;
            Guid? applicationId = null;
            var hasUserId = Guid.TryParse(claimUserid, out var id);
            if (hasUserId)
            {
                userId = id;
            }

            var hasAppId = Guid.TryParse(claimAppid, out var appId);
            if (hasAppId)
            {
                applicationId = appId;
            }

            if (!string.IsNullOrEmpty(claimUsername) && (hasUserId || hasAppId))
            {
                HashSet<Guid>? clientIdsList = null;
                if (!string.IsNullOrEmpty(clientIds))
                {
                    try
                    {
                        clientIdsList = JsonSerializer.Deserialize<HashSet<Guid>>(clientIds);
                    }
                    catch (JsonException)
                    {
                        // Ignore - assume empty list of clients
                    }
                }

                HashSet<Guid>? permissionIdsList = null;
                if (!string.IsNullOrEmpty(permissionIds))
                {
                    try
                    {
                        permissionIdsList = JsonSerializer.Deserialize<HashSet<Guid>>(permissionIds);
                    }
                    catch (JsonException)
                    {
                        // Ignore - assume empty list of permissions
                    }
                }

                userInfo = new EclipseUserInfo(userId, applicationId, claimUsername, clientIdsList ?? new HashSet<Guid>(), permissionIdsList ?? new HashSet<Guid>());
                return true;
            }
        }

        userInfo = null;
        return false;
    }

    public static bool UserHasPermission(this HttpContext currentContext, Guid permissionId)
    {
        if (currentContext.User.Identity is ClaimsIdentity principal)
        {
            var permissionIds = principal.Claims.SingleOrDefault(c => c.Type == ServiceConstants.Authorization.Permissions)?.Value;

            HashSet<Guid>? permissionIdsList = null;
            if (!string.IsNullOrEmpty(permissionIds))
            {
                try
                {
                    permissionIdsList = JsonSerializer.Deserialize<HashSet<Guid>>(permissionIds);
                }
                catch (JsonException)
                {
                    // Ignore - assume empty list of permissions
                }
            }

            if (permissionIdsList != null && permissionIdsList.Contains(permissionId))
            {
                return true;
            }
        }

        return false;
    }
}

public record EclipseUserInfo(Guid? UserId, Guid? AppId, string Username, IReadOnlySet<Guid> ClientIds, IReadOnlySet<Guid> PermissionIds);
