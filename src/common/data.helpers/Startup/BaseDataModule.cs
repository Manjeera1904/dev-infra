using System.Reflection;
using Autofac;
using Autofac.Builder;
using Autofac.Features.Scanning;
using EI.API.Service.Data.Helpers.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Module = Autofac.Module;

namespace EI.API.Service.Data.Helpers.Startup;

public abstract class BaseDataModule<TContext> : Module
    where TContext : DbContext
{
    public static readonly string ClientIdHeader = "X-EI-ClientId";

    protected virtual bool RequireClientId => true;

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<DatabaseClientFactory>()
               .As<IDatabaseClientFactory>()
               .InstancePerLifetimeScope();

        // Register all *Repository classes in this assembly
        var reg = builder.RegisterAssemblyTypes(typeof(TContext).Assembly)
                         .Where(type => type.Name.EndsWith("Repository"))
                         .AsImplementedInterfaces();

        if (RequireClientId)
        {
            // Don't auto-register any DbContext if we're client-specific.
            // Look up the ClientId parameter of the repository and set it
            // to the value from the request header so the context can be
            // resolved by clientId later when it's needed.
            reg.InstancePerLifetimeScope()
               .WithParameter(ClientIdParameterSelector, ClientIdValueSelector);
        }
        else
        {
            // If we're not client-specific, then just register the DbContext:
            builder.RegisterType<TContext>()
                   .AsSelf()
                   .InstancePerLifetimeScope();
        }
    }

    protected virtual object? ClientIdValueSelector(ParameterInfo param, IComponentContext context)
    {
        var httpContextAccessor = context.Resolve<IHttpContextAccessor>();
        var clientIdHeader = httpContextAccessor.HttpContext?.Request.Headers[ClientIdHeader].FirstOrDefault();
        return Guid.TryParse(clientIdHeader, out var clientId) ? clientId : Guid.Empty;
    }

    protected virtual bool ClientIdParameterSelector(ParameterInfo param, IComponentContext context)
        => param.Name == "clientId" && param.ParameterType == typeof(Guid);
}
