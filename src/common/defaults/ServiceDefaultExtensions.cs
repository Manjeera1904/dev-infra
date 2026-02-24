using Asp.Versioning;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using EI.API.Service.Data.Helpers;
using EI.API.Service.Rest.Helpers.Auth;
using EI.API.ServiceDefaults.Filters;
using EI.API.ServiceDefaults.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;
using System.Xml.XPath;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Hosting;

public static class ServiceDefaultExtensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder,
                                                             bool useApiVersioning = true,
                                                             bool addControllers = true,
                                                             bool addEndpointsApiExplorer = true,
                                                             bool addSwaggerGen = true,
                                                             bool requireClientIdHeader = true,
                                                             Func<XPathDocument>? swaggerDocs = null,
                                                             bool useApiAuthentication = false,
                                                             string[]? permissions = null,
                                                             string? applicationName = "",
                                                             params Type[] healthChecks
                                                             )
    {
        builder.ConfigureOpenTelemetry(applicationName);

        builder.AddDefaultHealthChecks(healthChecks);

        builder.Services.AddServiceDiscovery();

        builder.Services.AddHttpContextAccessor();

        builder.Services.ConfigureHttpClientDefaults(http =>
                                                         {
                                                             // Turn on resilience by default
                                                             http.AddStandardResilienceHandler();

                                                             // Turn on service discovery by default
                                                             http.AddServiceDiscovery();
                                                         });

        if (useApiVersioning)
        {
            builder.AddApiVersioning();
        }

        if (addControllers)
        {
            builder.Services.AddControllers();
        }

        if (addEndpointsApiExplorer)
        {
            builder.Services.AddEndpointsApiExplorer();
        }

        if (addSwaggerGen)
        {
            builder.Services.AddSwaggerGen(options =>
                                               {
                                                   if (requireClientIdHeader)
                                                   {
                                                       options.OperationFilter<ClientIdFilter>();
                                                   }

                                                   if (swaggerDocs != null)
                                                   {
                                                       options.IncludeXmlComments(swaggerDocs);
                                                   }

                                                   if (useApiAuthentication)
                                                   {
                                                       // Add JWT Authentication to Swagger
                                                       options.AddSecurityDefinition("Bearer",
                                                                                     new OpenApiSecurityScheme
                                                                                     {
                                                                                         Name = "Authorization",
                                                                                         Type = SecuritySchemeType.Http,
                                                                                         Scheme = "bearer",
                                                                                         BearerFormat = "JWT",
                                                                                         In = ParameterLocation.Header,
                                                                                         Description = "Enter your valid Bearer token in the text input below."
                                                                                     });

                                                       options.AddSecurityRequirement(new OpenApiSecurityRequirement
                                                                                      {
                                                                                          {
                                                                                              new OpenApiSecurityScheme
                                                                                              {
                                                                                                  Reference = new OpenApiReference
                                                                                                              {
                                                                                                                  Type = ReferenceType.SecurityScheme,
                                                                                                                  Id = "Bearer"
                                                                                                              }
                                                                                              },
                                                                                              []
                                                                                          }
                                                                                      });
                                                   }
                                               });
        }

        if (useApiAuthentication)
        {
            builder.AddApiAuthentication();
            builder.AddApiAuthorization(permissions);
        }

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder, string? applicationName)
    {

        TelemetryService.Initialize(applicationName ?? "DefaultService");


        builder.Logging.AddOpenTelemetry(logging =>
                                             {
                                                 logging.IncludeFormattedMessage = true;
                                                 logging.IncludeScopes = true;
                                             });

        var otelBuilder = builder.Services.AddOpenTelemetry();

        if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            Console.WriteLine("Configuring Azure Monitor");
            otelBuilder
                .UseAzureMonitor(opts =>
                                     {
                                         // Use entra auth via service principal:
                                         opts.Credential = new DefaultAzureCredential();
                                     });
        }
        else
        {
            Console.Error.WriteLine("NOT Configuring Azure Monitor");
        }

        otelBuilder
            .WithMetrics(metrics =>
                             {
                                 metrics.AddAspNetCoreInstrumentation()
                                        .AddHttpClientInstrumentation()
                                        .AddRuntimeInstrumentation()
                                        // .AddOtlpExporter()
                                        ;
                             })
            .WithTracing(tracing =>
                             {
                                 if (builder.Environment.IsDevelopment())
                                 {
                                     // We want to view all traces in development
                                     tracing.SetSampler(new AlwaysOnSampler());
                                 }

                                 tracing.AddSource(applicationName ?? "DefaultService")
                                        .AddAspNetCoreInstrumentation()
                                        .AddHttpClientInstrumentation()
                                        .AddEntityFrameworkCoreInstrumentation()
                                        // .AddOtlpExporter()
                                        ;
                             })
            .WithLogging();
        Console.WriteLine("Configured otelBuilder");
        return builder;
    }

    private static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder, Type[] healthChecks)
    {
        IHealthChecksBuilder healthCheckBuilder = builder.Services.AddHealthChecks();

        // Add a default liveness check to ensure app is responsive
        healthCheckBuilder.AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        if (healthChecks.Any())
        {
            var addCheckMethods =
                (
                    from method in typeof(HealthChecksBuilderAddCheckExtensions).GetMethods()
                    where method.IsGenericMethod &&
                          method.Name == nameof(HealthChecksBuilderAddCheckExtensions.AddCheck)
                    select method
                ).ToArray();

            var genericMethod =
                (
                    from method in addCheckMethods
                    let generics = method.GetGenericArguments()
                    where generics.Length == 1 &&
                          generics[0].GetInterfaces().Contains(typeof(IHealthCheck))
                    let parameters = method.GetParameters()
                    where parameters.Length == 4 &&
                          parameters[0].ParameterType == typeof(IHealthChecksBuilder) &&
                          parameters[1].ParameterType == typeof(string) &&
                          parameters[2].ParameterType == typeof(HealthStatus?) &&
                          parameters[3].ParameterType == typeof(IEnumerable<string>)
                    select method
                ).Single();

            foreach (var healthCheck in healthChecks)
            {
                var concreteMethod = genericMethod.MakeGenericMethod(healthCheck);
                concreteMethod.Invoke(null, new object[] { healthCheckBuilder, healthCheck.Name, null!, Enumerable.Empty<string>() });

                // healthCheckBuilder.AddCheck<IHealthCheck>("asdf", null, Enumerable.Empty<string>());
            }
        }
        Console.WriteLine("Configured healthchecks");

        return builder;
    }

    private static IHostApplicationBuilder AddApiVersioning(this IHostApplicationBuilder builder)
    {
        builder.Services
               .AddApiVersioning(options =>
                                     {
                                         options.ReportApiVersions = true;
                                         options.AssumeDefaultVersionWhenUnspecified = false;
                                         options.UnsupportedApiVersionStatusCode = 404;

                                         // options.ApiVersionReader = new UrlSegmentApiVersionReader();
                                     })
               .AddMvc()
               .AddApiExplorer(options => options.GroupNameFormat = "'v'VVV");

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSwaggerGen(c =>
                                               {
                                                   c.OperationFilter<ApiVersionOperationFilter>();
                                               });
        }

        return builder;
    }

    public static WebApplication AddApplicationDefaults(this WebApplication app,
                                                        bool useHttpsRedirection = true,
                                                        bool useAuthorization = true,
                                                        bool mapControllers = true,
                                                        bool useSwaggerUiForDev = true,
                                                        bool useSwaggerUiForNonDev = false,
                                                        bool configureCors = true,
                                                        bool mapDefaultEndpoints = true,
                                                        bool mapHealthCheck = true,
                                                        bool useAuthentication = true)
    {
        // Configure the HTTP request pipeline.
        if (useSwaggerUiForNonDev || (useSwaggerUiForDev && app.Environment.IsDevelopment()))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (useHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        if (useAuthentication)
        {
            app.UseAuthentication();
        }

        if (useAuthorization)
        {
            app.UseAuthorization();
        }

        if (mapControllers)
        {
            app.MapControllers();
        }

        if (configureCors)
        {
            app.UseCors(builder =>
            {
                builder.AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowAnyOrigin()
                       .SetPreflightMaxAge(TimeSpan.FromHours(24));
            });
        }


        if (mapDefaultEndpoints)
        {
            app.MapDefaultEndpoints();
        }

        if (mapHealthCheck)
        {
            app.MapHealthChecks("/ServiceHealth");
        }

        return app;
    }

    private static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }

    private static IHostApplicationBuilder AddApiAuthentication(this IHostApplicationBuilder builder)
    {

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
                NameClaimType = ServiceConstants.Authorization.Permissions,
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub/rules-engine-facade"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        return builder;
    }

    private static IHostApplicationBuilder AddApiAuthorization(this IHostApplicationBuilder builder, string[]? permissions)
    {
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionsHandler>();

        if (permissions != null && permissions.Length > 0)
        {
            builder.Services.AddAuthorization(options =>
            {
                permissions.ToList()
                    .ForEach(permission =>
                        options.AddPolicy(permission, policy => policy.Requirements.Add(new PermissionsRequirement(permission))));
            });
        }

        return builder;
    }

    public class ApiVersionOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var apiDescription = context.ApiDescription;

            if (apiDescription.ActionDescriptor is ControllerActionDescriptor actionDescriptor)
            {
                var methodVersions = actionDescriptor.MethodInfo
                                         .GetCustomAttributes(typeof(ApiVersionAttribute), true)
                                         .OfType<ApiVersionAttribute>()
                                         .SelectMany(attr => attr.Versions)
                                         .Distinct()
                                         .Select(version => new OpenApiString(version.ToString()))
                                         .ToList();

                var controllerVersions = actionDescriptor
                                         .ControllerTypeInfo
                                         .GetCustomAttributes(typeof(ApiVersionAttribute), true)
                                         .OfType<ApiVersionAttribute>()
                                         .SelectMany(attr => attr.Versions)
                                         .Distinct()
                                         .Select(version => new OpenApiString(version.ToString()))
                                         .ToList();

                var versions = methodVersions.Union(controllerVersions)
                                             .Distinct().ToList();

                if (versions.Any())
                {
                    operation.Parameters ??= new List<OpenApiParameter>();
                    var parameter = operation.Parameters.FirstOrDefault(p => p.Name == "api-version");
                    if (parameter == null)
                    {
                        parameter = new OpenApiParameter
                        {
                            Name = "api-version",
                            In = ParameterLocation.Query,
                            Required = true,
                            Description = "API Version",
                            Schema = new OpenApiSchema
                            {
                                Type = "string",
                                Default = versions.First(),
                                Enum = versions.ToList<IOpenApiAny>()
                            }
                        };
                        operation.Parameters.Add(parameter);
                    }
                    else
                    {
                        parameter.Schema.Enum = versions.ToList<IOpenApiAny>();
                    }
                }
            }
        }
    }

}
