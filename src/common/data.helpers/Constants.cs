namespace EI.API.Service.Data.Helpers;

public static class ServiceConstants
{
    public static class HttpHeaders
    {
        public const string ClientId = "X-EI-ClientId";
    }
    public static class CultureCode
    {
        public const string Default = "en-US";
    }
    public static class Authorization
    {
        public const string Permissions = "ei-permissions";
        public const string Token = "ei-token";
        public const string UserId = "ei-userid";
        public const string Username = "ei-username";
        public const string Client = "ei-client";
        public const string ClientList = "ei-client-list";
        public const string AppId = "ei-appid";

        public const string ApplicationUsername = "System";
        public const string UserTokenType = "user";
        public const string ApplicationTokenType = "app";
    }
}
