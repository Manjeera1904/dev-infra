# Eclipse Insights API Service Defaults

Provides extension methods that configure ASP.NET applications with the common default services
and settings, and provides ways of overriding some of those defaults.

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

// Wire up common .NET Aspire basics
builder.AddServiceDefaults(healthChecks: [typeof(HealthCheck)]);

// ( add other service-specific builder options here )

var app = builder.Build();

// Wire up common .NET Aspire basics
app.AddApplicationDefaults();

// ( add other service-specific application options here )

app.Run();
```