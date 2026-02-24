using Autofac;
using Autofac.Extras.AggregateService;
using EI.API.Service.Rest.Helpers.Controllers;

namespace EI.API.Service.Rest.Helpers.Startup;

public abstract class BaseControllersModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        // Register the base controller services interface as an aggregate service
        builder.RegisterAggregateService<IBaseControllerServices>();

        // Register all extensions of that interface (named *ControllerServices) in this module's assembly
        var controllerServices = (
                                     from type in GetType().Assembly.GetTypes()
                                     where type.IsInterface && type.Name.EndsWith("ControllerServices")
                                     select type
                                 ).ToList();

        foreach (var controllerService in controllerServices)
        {
            builder.RegisterAggregateService(controllerService);
        }
    }
}
